using System.Net;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;
using Microsoft.AspNetCore.Http;

namespace ClankerWorld.Tests;

/// <summary>The approval listener, and the launch secret of a local server (#1563).</summary>
public sealed class ApprovalListenerTests
{
    private const int ApprovalPort = 5189;
    private static readonly string Secret = new('s', ApprovalListenerOptions.MinimumLaunchSecretLength);

    [Fact]
    public void OnlyTheGameThatStartedTheServerCanApproveOrStopIt()
    {
        var localServer = new ApprovalListenerOptions(ApprovalPort, Secret);
        Assert.True(localServer.IsApprovalRequest(Request(ApprovalPort, Secret)));
        Assert.True(localServer.IsLocalServerShutdownRequest(Request(ApprovalPort, Secret)));
        // Any other program on the PC can reach the loopback listener, but not without the secret.
        Assert.False(localServer.IsApprovalRequest(Request(ApprovalPort, null)));
        Assert.False(localServer.IsApprovalRequest(Request(ApprovalPort, Secret[..^1] + "x")));
        Assert.False(localServer.IsLocalServerShutdownRequest(Request(ApprovalPort, "short")));
        Assert.False(localServer.IsApprovalRequest(Request(5188, Secret)));
        Assert.False(localServer.IsApprovalRequest(Request(ApprovalPort, Secret, IPAddress.Parse("192.0.2.4"))));
    }

    [Fact]
    public void ARemoteServerKeepsItsApprovalListenerAndCannotBeStoppedOverHttp()
    {
        var server = new ApprovalListenerOptions(ApprovalPort);
        Assert.True(server.IsApprovalRequest(Request(ApprovalPort, null)));
        Assert.False(server.IsLocalServerShutdownRequest(Request(ApprovalPort, null)));
        Assert.False(server.IsLocalServerShutdownRequest(Request(ApprovalPort, Secret)));
        Assert.False(new ApprovalListenerOptions(0).IsApprovalRequest(Request(0, null)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1:5189")]
    [InlineData("[::1]")]
    [InlineData("[::1]:5189")]
    [InlineData("localhost")]
    [InlineData("LocalHost:5189")]
    public void ApprovalListenerAcceptsALiteralLoopbackHostName(string host)
    {
        Assert.True(new ApprovalListenerOptions(ApprovalPort).IsApprovalRequest(Request(ApprovalPort, null, host: host)));
        var localServer = new ApprovalListenerOptions(ApprovalPort, Secret);
        Assert.True(localServer.IsApprovalRequest(Request(ApprovalPort, Secret, host: host)));
        Assert.True(localServer.IsLocalServerShutdownRequest(Request(ApprovalPort, Secret, host: host)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.test")]
    [InlineData("example.test:5189")]
    [InlineData("localhost.example.test:5189")]
    [InlineData("127.0.0.1.example.test")]
    [InlineData("localhost.")]
    [InlineData("127.1:5189")]
    [InlineData("[::ffff:127.0.0.1]:5189")]
    [InlineData("0.0.0.0:5189")]
    public void ApprovalListenerRefusesAnyOtherHostNameEvenWithTheSecret(string host)
    {
        Assert.False(new ApprovalListenerOptions(ApprovalPort).IsApprovalRequest(Request(ApprovalPort, null, host: host)));
        var localServer = new ApprovalListenerOptions(ApprovalPort, Secret);
        Assert.False(localServer.IsApprovalRequest(Request(ApprovalPort, Secret, host: host)));
        Assert.False(localServer.IsLocalServerShutdownRequest(Request(ApprovalPort, Secret, host: host)));
    }

    [Fact]
    public void LaunchSecretFileMustHoldOneLongValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"clankerworld-local-server-{Guid.NewGuid():N}.secret");
        try
        {
            File.WriteAllText(path, Secret + "\r\n");
            Assert.Equal(Secret, ApprovalListenerOptions.ReadLaunchSecret(path));
            File.WriteAllText(path, "too-short");
            Assert.Throws<InvalidOperationException>(() => ApprovalListenerOptions.ReadLaunchSecret(path));
            File.WriteAllText(path, Secret + " " + Secret);
            Assert.Throws<InvalidOperationException>(() => ApprovalListenerOptions.ReadLaunchSecret(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StoppingTheServerSavesTheLatestTickAndCancelsModelWork()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"clankerworld-shutdown-save-{Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(directory, "world.json");
            var logger = new RecordingLogger<WorldRuntimeService>();
            var provider = new WaitingModelProvider();
            using var runtime = new WorldRuntime("shutdown-save", id =>
                id == "founder-scout" ? provider : new DeterministicDecisionProvider());
            using var service = new WorldRuntimeService(runtime, new WorldStateFile(path),
                new OwnerClientPresenceLease(TimeSpan.FromMinutes(1)), logger);
            // Ticks committed in memory but not yet written, as when the host stops mid-loop.
            Assert.True((await runtime.TickModelWorkAsync()).Advanced);
            await provider.Started.Task;
            var tick = runtime.WorldTick;

            Assert.Equal(ShutdownSaveOutcome.Saved, service.SaveBeforeShutdown(pauseWorld: true));
            Assert.True(provider.Cancellation.IsCancellationRequested);

            using var reloaded = new WorldStateFile(path).LoadOrCreate("shutdown-save");
            Assert.Equal(tick, reloaded.WorldTick);
            Assert.True(reloaded.Society.IsPaused);
            Assert.False((await reloaded.TickModelWorkAsync()).Advanced);
            Assert.Equal(tick, reloaded.WorldTick);
            Assert.Contains(logger.Messages, message => message.Contains("server_shutdown outcome=saved", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class WaitingModelProvider : IDecisionProvider
    {
        public InlineCompletionSource Started { get; } = new(TaskCreationOptions.None);
        public CancellationToken Cancellation { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Jev;
        public long ProviderEpoch => 1;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Cancellation = cancellationToken;
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The hosted call must be cancelled.");
        }
    }

    // The game's HttpClient names the approval listener as 127.0.0.1 and its port.
    private static DefaultHttpContext Request(int localPort, string? secret, IPAddress? remote = null,
        string host = "127.0.0.1:5189")
    {
        var context = new DefaultHttpContext();
        context.Connection.LocalPort = localPort;
        context.Connection.RemoteIpAddress = remote ?? IPAddress.Loopback;
        context.Request.Host = new HostString(host);
        if (secret is not null) context.Request.Headers[ApprovalListenerOptions.LaunchSecretHeader] = secret;
        return context;
    }
}
