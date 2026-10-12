using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.Tests;

/// <summary>The game starting, pairing with and stopping its bundled host (#1564).</summary>
public sealed class LocalHostCompanionTests
{
    private static readonly string[] UntouchedDataFiles =
        ["saves/world.json", "settings/provider-configuration.json", "client/game-display-preferences.json"];
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DeveloperRunKeepsRealDataUntouchedAndDetailedLoggingAppliesOnlyToThatRun(bool developerMode, bool detailed)
    {
        var directory = Directory.CreateTempSubdirectory("launcher-data-");
        var launcher = LauncherLayout.ForUser(directory.FullName);
        var arguments = GameStarter.Arguments("launcher.exe", developerMode, useTestData: true, detailedLogs: detailed);
        var data = GameStarter.DataDirectory(directory.FullName, arguments);
        var untouched = developerMode ? launcher.Root : launcher.TestDataDirectory;
        var sentinels = UntouchedDataFiles
            .Select(name => Path.Combine(untouched, name)).ToArray();
        foreach (var path in sentinels)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "keep this data unchanged");
        }
        var viewer = typeof(Program).Assembly;
        var (version, revision) = BuildOf(viewer);
        var layout = new LocalHostLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [viewer.Location], Path.GetDirectoryName(viewer.Location)!, data);
        using var http = new HttpClient();
        var game = new LocalHostCompanion(layout, http, FreePort(), FreePort(),
            detailedLogs: GameStarter.UsesDetailedLogs(arguments));
        try
        {
            Assert.Equal(LocalHostOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(File.Exists(Path.Combine(data, "saves", "world.json")));
            var preferences = new ClankerWorld.GodotClient.ClientState.GameDisplayPreferencesStore(
                GameStarter.ClientDataPath(directory.FullName, arguments, "game-display-preferences.json"));
            preferences.Save(new(Theme: "dark"));
            Assert.Equal("dark", preferences.Load().Theme);
            Assert.True(await game.StopAsync(CancellationToken.None));
            var log = await ReadSharedAsync(layout.LogPath);
            Assert.Equal(developerMode && detailed, log.Contains("host_launch logging=detailed", StringComparison.Ordinal));
            Assert.DoesNotContain(File.ReadAllText(layout.SecretPath), log);
            Assert.All(sentinels, path => Assert.Equal("keep this data unchanged", File.ReadAllText(path)));
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }
    [Fact]
    public void BundledHostIsFoundBesideTheGameAndKeepsDataInTheUserFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"clankerworld-layout-{Guid.NewGuid():N}");
        try
        {
            var game = Path.Combine(root, "ClankerWorld-0.1.0-alpha.1", "ClankerWorld.exe");
            var localAppData = Path.Combine(root, "LocalAppData");
            Assert.Null(LocalHostLayout.FindBundled(game, localAppData));

            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "host"));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game)!, "host", LocalHostLayout.WindowsHostFileName), "");
            var layout = LocalHostLayout.FindBundled(game, localAppData);

            Assert.NotNull(layout);
            Assert.Equal(Path.Combine(localAppData, "ClankerWorld"), layout.DataDirectory);
            var arguments = layout.HostArguments(5188, 5189);
            Assert.Contains("--ClankerWorld:Pairing:LocalApprovalPort=5189", arguments);
            // Every saved path is in the player's folder, never in the versioned game folder.
            foreach (var path in arguments.Where(item => item.Contains("Path=", StringComparison.Ordinal)))
                Assert.StartsWith(layout.DataDirectory, path[(path.IndexOf('=', StringComparison.Ordinal) + 1)..],
                    StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BundledHostGetsNoProviderKeysOrSettingOverridesFromThePlayersEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OPENAI_API_KEY"] = "one",
            ["ANTHROPIC_API_KEY"] = "two",
            ["OLLAMA_API_KEY"] = "three",
            ["typesafe_api_key"] = "four",
            ["ClankerWorld__Runtime__DecisionProvider"] = "openai",
            ["clankerworld__pairing__statepath"] = "elsewhere",
            ["ClankerWorld:Runtime:Model"] = "model",
            ["DOTNET_ClankerWorld__Http__Port"] = "1",
            ["ASPNETCORE_CLANKERWORLD__RUNTIME__SEED"] = "seed",
            ["PATH"] = "/usr/bin",
            ["DOTNET_ROOT"] = "/dotnet",
            ["ClankerWorld_Notes"] = "kept",
            ["OPENAI_API_KEY_BACKUP"] = "kept",
        };
        LocalHostLayout.WithholdHostSettings(environment);
        Assert.Equal("ClankerWorld_Notes,DOTNET_ROOT,OPENAI_API_KEY_BACKUP,PATH",
            string.Join(',', environment.Keys.Order(StringComparer.Ordinal)));

        // The start the game uses applies this to the environment it inherits.
        const string Probe = "ClankerWorld__Tests__CompanionEnvironmentProbe";
        Environment.SetEnvironmentVariable(Probe, "set");
        try
        {
            var layout = new LocalHostLayout("host", [], "host-folder", "data");
            var start = layout.StartInfo(5188, 5189);
            Assert.False(start.Environment.ContainsKey(Probe));
            Assert.DoesNotContain(start.Environment.Keys, name =>
                LocalHostLayout.ProviderKeyVariables.Contains(name, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(Environment.GetEnvironmentVariable("PATH"), start.Environment["PATH"]);
            Assert.Equal(layout.HostArguments(5188, 5189), start.ArgumentList);
            Assert.Equal(layout.HostArguments(5188, 5189, detailedLogs: true),
                layout.StartInfo(5188, 5189, detailedLogs: true).ArgumentList);
            Assert.Equal("host-folder", start.WorkingDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Probe, null);
        }
    }

    [Fact]
    public async Task PairingWithAnotherServerKeepsThePublicRoute()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var signer = new Signer(key);

        await Assert.ThrowsAsync<HttpRequestException>(() => new OwnerPairingClient(http)
            .StartPairingAsync(new Uri("https://world.example.test/"), signer, CancellationToken.None));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://world.example.test/api/v1/pairings"), request.RequestUri);
        Assert.False(request.Headers.Contains(LocalHostCompanion.SecretHeader));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        }
    }

    private sealed class Signer(ECDsa key) : IOwnerDeviceSigner
    {
        public string PublicKeySpkiBase64 => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        public string PublicKeyFingerprint => OwnerPairingProtocol.CreatePublicKeyFingerprint(key.ExportSubjectPublicKeyInfo());
        public string SignCanonicalProof(string canonicalProof) => OwnerPairingProtocol.SignP256Sha256P1363(key, canonicalProof);
        public void Dispose() { } // The test owns the key.
    }

    [Fact]
    public async Task GameStartsPairsWithAndStopsItsOwnHost()
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-companion-{Guid.NewGuid():N}");
        var (port, approvalPort) = (FreePort(), FreePort());
        var viewer = typeof(Program).Assembly;
        var (version, revision) = BuildOf(viewer);
        var layout = new LocalHostLayout(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [viewer.Location], Path.GetDirectoryName(viewer.Location)!, data);
        using var http = new HttpClient();
        var game = new LocalHostCompanion(layout, http, port, approvalPort);
        try
        {
            Assert.Equal(LocalHostOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(File.Exists(Path.Combine(data, "saves", "world.json")));
            Assert.DoesNotContain(File.ReadAllText(layout.SecretPath), await ReadSharedAsync(layout.LogPath));

            // A relaunch of the same build reuses the running host; a different build refuses it.
            var relaunch = new LocalHostCompanion(layout, http, port, approvalPort);
            Assert.Equal(LocalHostOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var otherBuild = new LocalHostCompanion(layout, http, port, approvalPort);
            Assert.Equal(LocalHostOutcome.PortInUse, (await otherBuild.StartAsync("9.9.9", revision, CancellationToken.None)).Outcome);

            // Use up the public route's shared budget and every pending slot.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                using var filled = await http.PostAsJsonAsync(new Uri(game.Origin, "api/v1/pairings"),
                    new { publicKeySpkiBase64 = Convert.ToBase64String(other.ExportSubjectPublicKeyInfo()) });
                Assert.Equal(HttpStatusCode.OK, filled.StatusCode);
            }
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var publicStart = await http.PostAsJsonAsync(new Uri(game.Origin, "api/v1/pairings"),
                new { publicKeySpkiBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) });
            Assert.Equal(HttpStatusCode.TooManyRequests, publicStart.StatusCode);
            using var withoutSecret = await http.PostAsJsonAsync(new Uri($"http://127.0.0.1:{approvalPort}/api/v1/local/pairings"),
                new { publicKeySpkiBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) });
            Assert.Equal(HttpStatusCode.NotFound, withoutSecret.StatusCode);

            // The game still pairs with its own host, through the local route and its secret.
            using var signer = new Signer(key);
            await Assert.ThrowsAsync<InvalidOperationException>(() => otherBuild.StartPairingAsync(signer, CancellationToken.None));
            var pairing = await game.StartPairingAsync(signer, CancellationToken.None);
            Assert.False(await otherBuild.ApprovePairingAsync(pairing.PairingId, pairing.PairingCode, CancellationToken.None));
            Assert.True(await game.ApprovePairingAsync(pairing.PairingId, pairing.PairingCode, CancellationToken.None));
            // Status and activation stay on the origin the registration is pinned to.
            var pairingClient = new OwnerPairingClient(http);
            Assert.Equal(OwnerPairingState.Approved,
                (await pairingClient.GetPairingStatusAsync(game.Origin, pairing.PairingId, CancellationToken.None)).State);
            var device = await pairingClient.ActivatePairingAsync(game.Origin, pairing, signer, CancellationToken.None);
            Assert.Equal(OwnerDeviceState.Active, device.State);
            Assert.True(game.Serves(new Uri($"http://127.0.0.1:{port}/")));
            Assert.False(game.Serves(new Uri($"http://127.0.0.1:{approvalPort}/")));
            Assert.False(game.Serves(new Uri("https://world.example.test/")));

            await game.StopAsync(CancellationToken.None);
            Assert.False(game.StartedHost);
            Assert.Contains("host_shutdown outcome=saved", await ReadSharedAsync(layout.LogPath), StringComparison.Ordinal);
            await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(new Uri(game.Origin, "api/v1/handshake")));
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Fact]
    public async Task AHostThatCannotStartIsReportedNotHidden()
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-companion-missing-{Guid.NewGuid():N}");
        try
        {
            var layout = new LocalHostLayout(Path.Combine(data, "missing-host"), [], data, data);
            Directory.CreateDirectory(Path.GetDirectoryName(layout.LogPath)!);
            File.WriteAllText(layout.LogPath, "the launch that crashed");
            using var http = new HttpClient();
            var game = new LocalHostCompanion(layout, http, FreePort(), FreePort());
            var start = await game.StartAsync("0", "0", CancellationToken.None);
            Assert.Equal(LocalHostOutcome.CouldNotStart, start.Outcome);
            Assert.Contains("try again", start.PlayerMessage, StringComparison.OrdinalIgnoreCase);
            // A new launch keeps the last one's log, so a crash can still be reported.
            Assert.Equal("the launch that crashed", File.ReadAllText(layout.PreviousLogPath));
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Theory]
    [InlineData("/api/v1/handshake")]
    [InlineData("/api/v1/status")]
    [InlineData("/api/v1/local/companion")]
    public async Task RelaunchRetriesOneTimedOutProbeAndReusesItsActualCompanion(string route)
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-companion-timeout-{Guid.NewGuid():N}");
        var viewer = typeof(Program).Assembly;
        var (version, revision) = BuildOf(viewer);
        var layout = new LocalHostLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [viewer.Location], Path.GetDirectoryName(viewer.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = (FreePort(), FreePort());
        var game = new LocalHostCompanion(layout, http, port, approval);
        try
        {
            Assert.Equal(LocalHostOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var secretDigest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var handler = new TimeoutOnceHandler(route);
            using var delayedHttp = new HttpClient(handler);
            var relaunch = new LocalHostCompanion(layout, delayedHttp, port, approval);

            Assert.Equal(LocalHostOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(handler.TargetRequests >= 2);
            Assert.False(relaunch.StartedHost);
            Assert.True(game.StartedHost);
            Assert.Equal(secretDigest, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AnUnresponsiveRelaunchPreservesItsHostAndSecretOnTimeoutOrCancellation(bool cancelCaller, bool deferTimeout)
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-companion-unresponsive-{Guid.NewGuid():N}");
        var viewer = typeof(Program).Assembly;
        var (version, revision) = BuildOf(viewer);
        var layout = new LocalHostLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [viewer.Location], Path.GetDirectoryName(viewer.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = (FreePort(), FreePort());
        var game = new LocalHostCompanion(layout, http, port, approval);
        try
        {
            Assert.Equal(LocalHostOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var secretDigest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var handler = new TimeoutOnceHandler("/api/v1/handshake", holdEveryRequest: true, deferTimeout: deferTimeout);
            using var delayedHttp = new HttpClient(handler);
            var relaunch = new LocalHostCompanion(layout, delayedHttp, port, approval, TimeSpan.FromMilliseconds(100));
            using var cancellation = new CancellationTokenSource();
            var starting = relaunch.StartAsync(version, revision, cancellation.Token);
            try
            {
                if (cancelCaller)
                {
                    await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(TimeSpan.FromSeconds(30)));
                }
                else
                {
                    if (deferTimeout)
                    {
                        await handler.TimedOut.Task.WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.False(cancellation.IsCancellationRequested);
                        // Hold the actual request-timeout continuation beyond the
                        // old caller timer, without changing the host or response.
                        await Task.Delay(TimeSpan.FromSeconds(6));
                    }
                    handler.ResumeTimeout.TrySetResult();
                    Assert.Equal(LocalHostOutcome.TimedOut, (await starting.WaitAsync(TimeSpan.FromSeconds(30))).Outcome);
                }
            }
            finally
            {
                cancellation.Cancel();
                handler.ResumeTimeout.TrySetResult();
                try { await starting.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch (OperationCanceledException) { }
            }
            Assert.True(handler.TargetRequests > 0);
            Assert.False(relaunch.StartedHost);
            Assert.True(game.StartedHost);
            Assert.Equal(secretDigest, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    private sealed class TimeoutOnceHandler(string route, bool holdEveryRequest = false, bool deferTimeout = false) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker inner = new(new HttpClientHandler());
        private int targetRequests;
        public int TargetRequests => Volatile.Read(ref targetRequests);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource TimedOut { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResumeTimeout { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == route && (Interlocked.Increment(ref targetRequests) == 1 || holdEveryRequest))
            {
                // Use the companion's actual request deadline, not a timer in
                // the test or a made-up foreign-host response.
                Entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (deferTimeout)
                {
                    TimedOut.TrySetResult();
                    await ResumeTimeout.Task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
                    throw;
                }
            }
            return await inner.SendAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task RefusedShutdownAndCallerCancellationPreserveTheChildForRetry(HttpStatusCode refusal)
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-companion-refused-{Guid.NewGuid():N}");
        var viewer = typeof(Program).Assembly;
        var (version, revision) = BuildOf(viewer);
        var layout = new LocalHostLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [viewer.Location], Path.GetDirectoryName(viewer.Location)!, data);
        using var handler = new ShutdownRefusalHandler();
        using var http = new HttpClient(handler);
        var game = new LocalHostCompanion(layout, http, FreePort(), FreePort());
        try
        {
            Assert.True((await game.StartAsync(version, revision, CancellationToken.None)).IsReady);
            handler.Refusal = refusal;
            Assert.False(await game.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(game.StartedHost);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            handler.Refusal = null;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => game.StopAsync(cancellation.Token));
            Assert.True(game.StartedHost);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            Assert.True(await game.StopAsync(CancellationToken.None));
            Assert.False(game.StartedHost);
        }
        finally
        {
            handler.Refusal = null;
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Fact]
    public async Task SameBuildRequiresTheRunningCompanionToRecognizeItsSavedSecret()
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-companion-proof-{Guid.NewGuid():N}");
        var viewer = typeof(Program).Assembly;
        var (version, revision) = BuildOf(viewer);
        var layout = new LocalHostLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [viewer.Location], Path.GetDirectoryName(viewer.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = (FreePort(), FreePort());
        var game = new LocalHostCompanion(layout, http, port, approval);
        string? original = null;
        try
        {
            Assert.True((await game.StartAsync(version, revision, CancellationToken.None)).IsReady);
            original = File.ReadAllText(layout.SecretPath);
            File.WriteAllText(layout.SecretPath, new string('x', 32));
            var relaunch = new LocalHostCompanion(layout, http, port, approval);
            Assert.Equal(LocalHostOutcome.PortInUse, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.False(relaunch.StartedHost);
            await relaunch.StopAsync(CancellationToken.None);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            File.WriteAllText(layout.SecretPath, original);
            Assert.Equal(LocalHostOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
        }
        finally
        {
            if (original is not null) File.WriteAllText(layout.SecretPath, original);
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    private sealed class ShutdownRefusalHandler : HttpMessageHandler
    {
        private readonly HttpMessageInvoker inner = new(new HttpClientHandler());
        public HttpStatusCode? Refusal { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request.RequestUri!.AbsolutePath == "/api/v1/local/shutdown" && Refusal is { } status
                ? Task.FromResult(new HttpResponseMessage(status)) : inner.SendAsync(request, cancellationToken);
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    private static (string Version, string Revision) BuildOf(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var separator = informational.IndexOf('+', StringComparison.Ordinal);
        return separator < 0 ? (informational, "unknown") : (informational[..separator], informational[(separator + 1)..]);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<string> ReadSharedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
