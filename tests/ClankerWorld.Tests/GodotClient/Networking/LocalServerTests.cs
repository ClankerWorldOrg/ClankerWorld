using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.Diagnostics;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.Tests;

/// <summary>The game starting, pairing with and stopping its local server (#1564).</summary>
public sealed partial class LocalServerTests
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
        var serverAssembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(serverAssembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [serverAssembly.Location], Path.GetDirectoryName(serverAssembly.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = FreePorts();
        var game = new LocalServer(layout, http, port, approval,
            detailedLogs: GameStarter.UsesDetailedLogs(arguments));
        try
        {
            Assert.Equal(LocalServerStartOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(File.Exists(Path.Combine(data, "saves", "world.json")));
            var preferences = new ClankerWorld.GodotClient.ClientState.GameDisplayPreferencesStore(
                GameStarter.ClientDataPath(directory.FullName, arguments, "game-display-preferences.json"));
            preferences.Save(new(Theme: "dark"));
            Assert.Equal("dark", preferences.Load().Theme);
            var launchSecret = File.ReadAllText(layout.SecretPath);
            Assert.True(await game.StopAsync(CancellationToken.None));
            // A clean stop removes the secret it no longer needs.
            Assert.False(File.Exists(layout.SecretPath));
            var log = await ReadSharedAsync(layout.LogPath);
            Assert.Equal(developerMode && detailed, log.Contains("server_launch logging=detailed", StringComparison.Ordinal));
            Assert.DoesNotContain(launchSecret, log);
            Assert.All(sentinels, path => Assert.Equal("keep this data unchanged", File.ReadAllText(path)));
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }
    [Fact]
    public void BundledServerIsFoundBesideTheGameAndKeepsDataInTheUserFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"clankerworld-layout-{Guid.NewGuid():N}");
        try
        {
            var game = Path.Combine(root, "ClankerWorld-0.1.0-alpha.1", "ClankerWorld.exe");
            var localAppData = Path.Combine(root, "LocalAppData");
            Assert.Null(LocalServerLayout.FindBundled(game, localAppData));

            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(game)!, "server"));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game)!, "server", "ClankerWorld.Server.exe"), "");
            Assert.Null(LocalServerLayout.FindBundled(game, localAppData));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(game)!, "package.json"),
                """{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}""");
            var layout = LocalServerLayout.FindBundled(game, localAppData);

            Assert.NotNull(layout);
            Assert.Null(LocalServerLayout.FindBundled(Path.Combine(Path.GetDirectoryName(game)!, "AnotherGame.exe"), localAppData));
            Assert.Equal(Path.Combine(Path.GetDirectoryName(game)!, "server", "ClankerWorld.Server.exe"), layout.FileName);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(game)!, "server"), layout.ServerDirectory);
            Assert.Equal(Path.Combine(localAppData, "ClankerWorld"), layout.DataDirectory);
            var arguments = layout.ServerArguments(5188, 5189);
            Assert.Contains("--ClankerWorld:Pairing:ApprovalPort=5189", arguments);
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
    public void BundledServerGetsNoProviderKeysOrSettingOverridesFromThePlayersEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OPENAI_API_KEY"] = "one",
            ["ANTHROPIC_API_KEY"] = "two",
            ["OLLAMA_API_KEY"] = "three",
            ["typesafe_api_key"] = "four",
            ["ClankerWorld__Providers__DecisionProvider"] = "openai",
            ["clankerworld__pairing__statepath"] = "elsewhere",
            ["ClankerWorld:Providers:Model"] = "model",
            ["DOTNET_ClankerWorld__Http__Port"] = "1",
            ["ASPNETCORE_CLANKERWORLD__RUNTIME__SEED"] = "seed",
            ["PATH"] = "/usr/bin",
            ["DOTNET_ROOT"] = "/dotnet",
            ["ClankerWorld_Notes"] = "kept",
            ["OPENAI_API_KEY_BACKUP"] = "kept",
        };
        LocalServerLayout.WithholdServerSettings(environment);
        Assert.Equal("ClankerWorld_Notes,DOTNET_ROOT,OPENAI_API_KEY_BACKUP,PATH",
            string.Join(',', environment.Keys.Order(StringComparer.Ordinal)));

        // The start the game uses applies this to the environment it inherits.
        const string Probe = "ClankerWorld__Tests__LocalServerEnvironmentProbe";
        Environment.SetEnvironmentVariable(Probe, "set");
        try
        {
            var layout = new LocalServerLayout("host", [], "host-folder", "data");
            var start = layout.StartInfo(5188, 5189);
            Assert.False(start.Environment.ContainsKey(Probe));
            Assert.DoesNotContain(start.Environment.Keys, name =>
                LocalServerLayout.ProviderKeyVariables.Contains(name, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(Environment.GetEnvironmentVariable("PATH"), start.Environment["PATH"]);
            Assert.Equal(layout.ServerArguments(5188, 5189), start.ArgumentList);
            Assert.Equal(layout.ServerArguments(5188, 5189, detailedLogs: true),
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
        Assert.False(request.Headers.Contains(LocalServer.SecretHeader));
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
    public async Task GameStartsPairsWithAndStopsItsOwnServer()
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-{Guid.NewGuid():N}");
        var (port, approvalPort) = FreePorts();
        var serverAssembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(serverAssembly);
        var layout = new LocalServerLayout(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [serverAssembly.Location], Path.GetDirectoryName(serverAssembly.Location)!, data);
        using var http = new HttpClient();
        var game = new LocalServer(layout, http, port, approvalPort);
        try
        {
            Assert.Equal(LocalServerStartOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(File.Exists(Path.Combine(data, "saves", "world.json")));
            Assert.DoesNotContain(File.ReadAllText(layout.SecretPath), await ReadSharedAsync(layout.LogPath));

            // A relaunch of the same build reuses the running host; a different build refuses it.
            var relaunch = new LocalServer(layout, http, port, approvalPort);
            Assert.Equal(LocalServerStartOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var otherBuild = new LocalServer(layout, http, port, approvalPort);
            Assert.Equal(LocalServerStartOutcome.PortInUse, (await otherBuild.StartAsync("9.9.9", revision, CancellationToken.None)).Outcome);

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
            using var withoutSecret = await http.PostAsJsonAsync(new Uri($"http://127.0.0.1:{approvalPort}/api/v1/approval/pairings"),
                new { publicKeySpkiBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) });
            Assert.Equal(HttpStatusCode.NotFound, withoutSecret.StatusCode);

            // The game still pairs with its own local server, through the local route and its secret.
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
            Assert.False(game.StartedServer);
            Assert.False(File.Exists(layout.SecretPath));
            Assert.Contains("server_shutdown outcome=saved", await ReadSharedAsync(layout.LogPath), StringComparison.Ordinal);
            await Assert.ThrowsAsync<HttpRequestException>(() => http.GetAsync(new Uri(game.Origin, "api/v1/handshake")));
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Fact]
    public async Task GameStopsItsOwnServerWhileAnIncompatibleSaveWaitsForRecovery()
    {
        var directory = Directory.CreateTempSubdirectory("local-recovery-exit-");
        var (port, approvalPort) = FreePorts();
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName);
        Directory.CreateDirectory(layout.SaveDirectory);
        var path = Path.Combine(layout.SaveDirectory, "world.json");
        var refused = System.Text.Encoding.UTF8.GetBytes("""{"state":{"schemaVersion":1}}""");
        File.WriteAllBytes(path, refused);
        using var http = new HttpClient();
        var game = new LocalServer(layout, http, port, approvalPort);
        try
        {
            Assert.Equal(LocalServerStartOutcome.Started,
                (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var processId = game.StartedServerProcessId!.Value;
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            Assert.True(await game.StopAsync(CancellationToken.None));
            Assert.True(process.HasExited);
            Assert.Equal(0, process.ExitCode);
            Assert.False(game.StartedServer);
            Assert.False(File.Exists(layout.SecretPath));
            Assert.Equal(refused, File.ReadAllBytes(path));
            var log = await ReadSharedAsync(layout.LogPath);
            Assert.Contains("startup_recovery outcome=waiting_for_owner", log, StringComparison.Ordinal);
            Assert.Contains("local_server outcome=shutdown_requested", log, StringComparison.Ordinal);
            Assert.DoesNotContain("server_shutdown outcome=saved", log, StringComparison.Ordinal);
        }
        finally
        {
            await game.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task AServerThatCannotStartIsReportedNotHidden()
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-missing-{Guid.NewGuid():N}");
        try
        {
            var layout = new LocalServerLayout(Path.Combine(data, "missing-host"), [], data, data);
            Directory.CreateDirectory(Path.GetDirectoryName(layout.LogPath)!);
            File.WriteAllText(layout.LogPath, "the launch that crashed");
            Directory.CreateDirectory(Path.GetDirectoryName(layout.SecretPath)!);
            File.WriteAllText(layout.SecretPath, "synthetic-previous-launch-value");
            var previousSecretDigest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var http = new HttpClient();
            var (port, approval) = FreePorts();
            var game = new LocalServer(layout, http, port, approval);
            var start = await game.StartAsync("0", "0", CancellationToken.None);
            Assert.Equal(LocalServerStartOutcome.CouldNotStart, start.Outcome);
            Assert.Contains("try again", start.PlayerMessage, StringComparison.OrdinalIgnoreCase);
            // A new launch keeps the last one's log, so a crash can still be reported.
            Assert.Equal("the launch that crashed", File.ReadAllText(layout.PreviousLogPath));
            Assert.Equal(previousSecretDigest, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Theory]
    [InlineData("/api/v1/handshake")]
    [InlineData("/api/v1/status")]
    [InlineData("/api/v1/approval/launch-check")]
    public async Task RelaunchRetriesOneTimedOutProbeAndReusesItsActualServer(string route)
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-timeout-{Guid.NewGuid():N}");
        var serverAssembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(serverAssembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [serverAssembly.Location], Path.GetDirectoryName(serverAssembly.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = FreePorts();
        var game = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.Equal(LocalServerStartOutcome.Started, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var secretDigest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var handler = new TimeoutOnceHandler(route);
            using var delayedHttp = new HttpClient(handler);
            var relaunch = new LocalServer(layout, delayedHttp, port, approval);

            Assert.Equal(LocalServerStartOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(handler.TargetRequests >= 2);
            Assert.False(relaunch.StartedServer);
            Assert.True(game.StartedServer);
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
    public async Task AnUnresponsiveRelaunchPreservesItsServerAndSecretOnTimeoutOrCancellation(bool cancelCaller, bool deferTimeout)
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-unresponsive-{Guid.NewGuid():N}");
        var serverAssembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(serverAssembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [serverAssembly.Location], Path.GetDirectoryName(serverAssembly.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = FreePorts();
        var game = new LocalServer(layout, http, port, approval);
        try
        {
            var initial = await game.StartAsync(version, revision, CancellationToken.None);
            Assert.True(initial.Outcome == LocalServerStartOutcome.Started,
                initial.Outcome == LocalServerStartOutcome.Started ? null :
                $"Initial launch: {initial.Outcome} ({initial.Detail}); public={port}; approval={approval}\n" +
                (File.Exists(layout.LogPath)
                    ? ProblemReport.Redact(await ReadSharedAsync(layout.LogPath), game.SecretsToHide())
                    : "No child log was created."));
            var secretDigest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var handler = new TimeoutOnceHandler("/api/v1/handshake", holdEveryRequest: true, deferTimeout: deferTimeout);
            using var delayedHttp = new HttpClient(handler);
            var clock = new ManualTimeProvider();
            var relaunch = new LocalServer(layout, delayedHttp, port, approval, TimeSpan.FromMilliseconds(100), timeProvider: clock);
            using var cancellation = new CancellationTokenSource();
            var starting = relaunch.StartAsync(version, revision, cancellation.Token);
            try
            {
                if (cancelCaller)
                {
                    await ModelWorkHarness.WaitForSignalAsync(handler.Entered.Task);
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModelWorkHarness.WaitForSignalAsync(starting));
                }
                else
                {
                    await ModelWorkHarness.WaitForSignalAsync(handler.Entered.Task);
                    clock.Advance(TimeSpan.FromSeconds(2));
                    if (deferTimeout)
                    {
                        await ModelWorkHarness.WaitForSignalAsync(handler.TimedOut.Task);
                        Assert.False(cancellation.IsCancellationRequested);
                        // Hold the actual request-timeout continuation beyond the
                        // old caller timer, without changing the host or response.
                        clock.Advance(TimeSpan.FromSeconds(6));
                    }
                    handler.ResumeTimeout.TrySetResult();
                    Assert.Equal(LocalServerStartOutcome.TimedOut, (await ModelWorkHarness.WaitForSignalAsync(starting)).Outcome);
                }
            }
            finally
            {
                cancellation.Cancel();
                handler.ResumeTimeout.TrySetResult();
                try { await ModelWorkHarness.WaitForSignalAsync(starting); }
                catch (OperationCanceledException) { }
            }
            Assert.True(handler.TargetRequests > 0);
            Assert.False(relaunch.StartedServer);
            Assert.True(game.StartedServer);
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
        public InlineCompletionSource Entered { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource TimedOut { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource ResumeTimeout { get; } = new(TaskCreationOptions.None);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == route && (Interlocked.Increment(ref targetRequests) == 1 || holdEveryRequest))
            {
                // Use the local server's actual request deadline, not a timer in
                // the test or a made-up foreign-host response.
                Entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (deferTimeout)
                {
                    TimedOut.TrySetResult();
                    await ModelWorkHarness.WaitForSignalAsync(ResumeTimeout.Task);
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
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-refused-{Guid.NewGuid():N}");
        var serverAssembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(serverAssembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [serverAssembly.Location], Path.GetDirectoryName(serverAssembly.Location)!, data);
        using var handler = new ShutdownRefusalHandler();
        using var http = new HttpClient(handler);
        var (port, approval) = FreePorts();
        var game = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.True((await game.StartAsync(version, revision, CancellationToken.None)).IsReady);
            handler.Refusal = refusal;
            Assert.False(await ModelWorkHarness.WaitForSignalAsync(game.StopAsync(CancellationToken.None)));
            Assert.True(game.StartedServer);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            handler.Refusal = null;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => game.StopAsync(cancellation.Token));
            Assert.True(game.StartedServer);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            Assert.True(await ModelWorkHarness.WaitForSignalAsync(game.StopAsync(CancellationToken.None)));
            Assert.False(game.StartedServer);
        }
        finally
        {
            handler.Refusal = null;
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Fact]
    public async Task SameBuildRequiresTheRunningServerToRecognizeItsSavedSecret()
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-proof-{Guid.NewGuid():N}");
        var serverAssembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(serverAssembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [serverAssembly.Location], Path.GetDirectoryName(serverAssembly.Location)!, data);
        using var http = new HttpClient();
        var (port, approval) = FreePorts();
        var game = new LocalServer(layout, http, port, approval);
        string? original = null;
        try
        {
            Assert.True((await game.StartAsync(version, revision, CancellationToken.None)).IsReady);
            original = File.ReadAllText(layout.SecretPath);
            File.WriteAllText(layout.SecretPath, new string('x', 32));
            var relaunch = new LocalServer(layout, http, port, approval);
            Assert.Equal(LocalServerStartOutcome.PortInUse, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.False(relaunch.StartedServer);
            await relaunch.StopAsync(CancellationToken.None);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            File.WriteAllText(layout.SecretPath, original);
            Assert.Equal(LocalServerStartOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
        }
        finally
        {
            if (original is not null) File.WriteAllText(layout.SecretPath, original);
            await game.StopAsync(CancellationToken.None);
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{"answer":"not-the-answer"}""")]
    [InlineData("not json")]
    public async Task AListenerThatCannotAnswerTheLaunchCheckIsNotReusedAndNeverReceivesTheSecret(string? body)
    {
        var data = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-unproven-{Guid.NewGuid():N}");
        try
        {
            var layout = new LocalServerLayout(Path.Combine(data, "missing-host"), [], data, data);
            Directory.CreateDirectory(Path.GetDirectoryName(layout.SecretPath)!);
            const string Saved = "synthetic-saved-launch-value-0123456789";
            File.WriteAllText(layout.SecretPath, Saved);
            using var handler = new SameBuildListener(body);
            using var http = new HttpClient(handler);
            var game = new LocalServer(layout, http, 5188, 5189);

            var start = await game.StartAsync("1.2.3", "abc", CancellationToken.None);
            Assert.Equal(LocalServerStartOutcome.PortInUse, start.Outcome);
            Assert.False(game.StartedServer);
            await game.StopAsync(CancellationToken.None);

            var check = Assert.Single(handler.Requests, request => request.RequestUri!.AbsolutePath == "/api/v1/approval/launch-check");
            Assert.True(LocalServerLaunchCheck.IsChallenge(check.Headers.GetValues(LocalServerLaunchCheck.ChallengeHeader).Single()));
            Assert.DoesNotContain(handler.Requests, request => request.Headers.Contains(LocalServer.SecretHeader) ||
                request.Headers.Any(header => header.Value.Any(value => value.Contains(Saved, StringComparison.Ordinal))));
            Assert.DoesNotContain(handler.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("/shutdown", StringComparison.Ordinal));
            Assert.Equal(Saved, File.ReadAllText(layout.SecretPath));
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
        }
    }

    /// <summary>Claims the expected build on the game's ports and answers launch checks with <c>body</c>.</summary>
    private sealed class SameBuildListener(string? body) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/handshake" => new HttpResponseMessage(HttpStatusCode.OK),
                "/api/v1/status" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"version":"1.2.3","sourceRevision":"abc"}""",
                        System.Text.Encoding.UTF8, "application/json"),
                },
                "/api/v1/approval/launch-check" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body ?? string.Empty, System.Text.Encoding.UTF8, "application/json"),
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ShutdownRefusalHandler : HttpMessageHandler
    {
        private readonly HttpMessageInvoker inner = new(new HttpClientHandler());
        public HttpStatusCode? Refusal { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request.RequestUri!.AbsolutePath == "/api/v1/approval/shutdown" && Refusal is { } status
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

    private static (int Public, int Approval) FreePorts()
    {
        // Keep the first port bound until the second is chosen. Closing both
        // independently lets the OS return the same ephemeral port twice.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return (((IPEndPoint)listener.LocalEndpoint).Port, FreePort());
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
