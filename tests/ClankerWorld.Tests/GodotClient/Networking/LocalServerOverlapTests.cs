using System.Net;
using System.Security.Cryptography;
using ClankerWorld.Contracts;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.Tests;

public sealed partial class LocalServerTests
{
    [Fact]
    public async Task StartingANewChildDoesNotReuseAnEarlierListenersProof()
    {
        var directory = Directory.CreateTempSubdirectory("local-server-new-proof-");
        var assembly = typeof(Program).Assembly;
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName);
        const string Previous = "synthetic-previous-launch-value-0123456789";
        Directory.CreateDirectory(Path.GetDirectoryName(layout.SecretPath)!);
        File.WriteAllText(layout.SecretPath, Previous);
        using var handler = new ReplacedListenerHandler(Previous);
        using var http = new HttpClient(handler);
        var (port, approval) = FreePorts();
        var game = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.Equal(LocalServerStartOutcome.Reused, (await game.StartAsync("1.2.3", "abc", CancellationToken.None)).Outcome);
            handler.ReplaceListener = true;
            Assert.Equal(LocalServerStartOutcome.PortInUse, (await game.StartAsync("1.2.3", "abc", CancellationToken.None)).Outcome);
            Assert.False(handler.SentSecretAfterReplacement);
            Assert.False(game.StartedServer);
        }
        finally
        {
            // Also settle the child when running against the regression.
            if (game.StartedServerProcessId is { } id)
            {
                using var child = System.Diagnostics.Process.GetProcessById(id);
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }
            handler.AllowCleanup = true;
            await game.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    private sealed class ReplacedListenerHandler(string previousSecret) : HttpMessageHandler
    {
        public bool ReplaceListener { get; set; }
        public bool AllowCleanup { get; set; }
        private int requestsAfterReplacement;
        public bool SentSecretAfterReplacement { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ReplaceListener)
            {
                requestsAfterReplacement++;
                SentSecretAfterReplacement |= request.Headers.Contains(LocalServer.SecretHeader);
                if (requestsAfterReplacement == 1)
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "The previous listener has stopped.");
            }
            return Task.FromResult(request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/handshake" => new HttpResponseMessage(HttpStatusCode.OK),
                "/api/v1/status" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ReplaceListener
                        ? """{"version":"another-build","sourceRevision":"other"}"""
                        : """{"version":"1.2.3","sourceRevision":"abc"}"""),
                },
                "/api/v1/approval/launch-check" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new LocalServerLaunchCheckAnswer(
                        LocalServerLaunchCheck.Answer(previousSecret, request.Headers.GetValues(LocalServerLaunchCheck.ChallengeHeader).Single()))),
                },
                "/api/v1/approval/hosting" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new HostStatus(0, false, false, "unspecified", false)),
                },
                _ => new HttpResponseMessage(AllowCleanup ? HttpStatusCode.OK : HttpStatusCode.Conflict),
            });
        }
    }

    [Fact]
    public async Task CancellingOwnedStartupSettlesTheChildAndReleasesInstallationStartup()
    {
        var directory = Directory.CreateTempSubdirectory("local-server-cancelled-start-");
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName);
        var (port, approval) = FreePorts();
        using var held = new HeldLaunchProofHandler();
        using var http = new HttpClient(held);
        using var caller = new CancellationTokenSource();
        var game = new LocalServer(layout, http, port, approval);
        var starting = game.StartAsync(version, revision, caller.Token);
        try
        {
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.False(game.StartedServer);
            using var freshHttp = new HttpClient();
            var fresh = new LocalServer(layout, freshHttp, port, approval);
            try
            {
                Assert.Equal(LocalServerStartOutcome.Started, (await fresh.StartAsync(version, revision, CancellationToken.None)).Outcome);
            }
            finally { await fresh.StopAsync(CancellationToken.None); }
        }
        finally
        {
            caller.Cancel();
            held.Release.TrySetResult();
            try { await starting.WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (OperationCanceledException) when (caller.IsCancellationRequested) { }
            await game.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task AFailedOverlappingLaunchPreservesTheLiveServersSecret()
    {
        var directory = Directory.CreateTempSubdirectory("local-server-overlap-");
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName);
        var (port, approval) = FreePorts();
        using var held = new HeldLaunchProofHandler();
        using var firstHttp = new HttpClient(held);
        using var secondHttp = new HttpClient(new EmptyPortHandler());
        using var actualHttp = new HttpClient();
        var first = new LocalServer(layout, firstHttp, port, approval);
        var second = new LocalServer(layout with { FileName = Path.Combine(directory.FullName, "missing-server") },
            secondHttp, port, approval);
        var starting = first.StartAsync(version, revision, CancellationToken.None);
        byte[]? original = null;
        try
        {
            // The companion has read the real secret and confirmed it, but the
            // owning game's startup call has not completed. Model a second
            // launch's empty-port observation during the pre-listen window.
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            original = File.ReadAllBytes(layout.SecretPath);
            var failed = await second.StartAsync(version, revision, CancellationToken.None);
            Assert.False(failed.IsReady);
            Assert.Equal(SHA256.HashData(original), SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            held.Release.TrySetResult();
            Assert.Equal(LocalServerStartOutcome.Started, (await starting.WaitAsync(TimeSpan.FromSeconds(30))).Outcome);
            var fresh = new LocalServer(layout, actualHttp, port, approval);
            Assert.Equal(LocalServerStartOutcome.Reused, (await fresh.StartAsync(version, revision, CancellationToken.None)).Outcome);
        }
        finally
        {
            held.Release.TrySetResult();
            await starting.WaitAsync(TimeSpan.FromSeconds(30));
            if (original is not null) File.WriteAllBytes(layout.SecretPath, original);
            await first.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    private sealed class HeldLaunchProofHandler : HttpMessageHandler
    {
        private readonly HttpMessageInvoker inner = new(new HttpClientHandler());
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await inner.SendAsync(request, cancellationToken);
            if (request.RequestUri!.AbsolutePath == "/api/v1/approval/launch-check" && response.IsSuccessStatusCode)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return response;
        }

        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class EmptyPortHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException(HttpRequestError.ConnectionError, "The controlled startup port is not listening yet.");
    }
}
