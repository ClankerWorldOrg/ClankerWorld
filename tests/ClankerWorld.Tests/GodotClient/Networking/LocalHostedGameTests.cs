using System.Security.Cryptography;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.Tests;

public sealed partial class LocalServerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostPcGameReusesItsHostAndLeavingKeepsOtherDeviceConnected(bool stopWhenGameQuits)
    {
        var directory = Directory.CreateTempSubdirectory("host-pc-game-");
        var (port, approval) = FreePorts();
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName)
        { HostKind = "launcher", StopWhenGameQuits = stopWhenGameQuits };
        using var http = new HttpClient();
        var host = new LocalServer(layout, http, port, approval);
        var game = new LocalServer(layout with
        {
            HostKind = null,
            StopWhenGameQuits = false,
            FileName = Path.Combine(directory.FullName, "must-not-start-a-second-server"),
        }, http, port, approval, joinHostedWorld: true);
        try
        {
            Assert.Equal(LocalServerStartOutcome.Started, (await host.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var processId = host.StartedServerProcessId;
            var proof = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var remoteKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var remoteSigner = new Signer(remoteKey);
            var pairingClient = new OwnerPairingClient(http);
            var remotePairing = await pairingClient.StartPairingAsync(host.Origin, remoteSigner, CancellationToken.None);
            Assert.True(await host.ApprovePairingAsync(remotePairing.PairingId, remotePairing.PairingCode, CancellationToken.None));
            var remote = await pairingClient.ActivatePairingAsync(host.Origin, remotePairing, remoteSigner, CancellationToken.None);
            var api = new OwnerWorldApi(http);
            await api.ReconnectAsync(host.Origin, remotePairing.Authority, remote.DeviceId, 0, null, null, null,
                remoteSigner, CancellationToken.None);
            Assert.Equal(LocalServerStartOutcome.Reused, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.False(game.StartedServer);
            Assert.True(game.JoinedHostedServer);
            Assert.Equal(processId, host.StartedServerProcessId);
            using var localKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var localSigner = new Signer(localKey);
            // The game's existing private automatic setup needs no comparison-code UI.
            var localPairing = await game.StartPairingAsync(localSigner, CancellationToken.None);
            Assert.True(await game.ApprovePairingAsync(localPairing.PairingId, localPairing.PairingCode, CancellationToken.None));
            var local = await pairingClient.ActivatePairingAsync(game.Origin, localPairing, localSigner, CancellationToken.None);
            Assert.Equal(OwnerDeviceState.Active, local.State);
            await api.ReconnectAsync(game.Origin, localPairing.Authority, local.DeviceId, 0, null, null, null,
                localSigner, CancellationToken.None);
            Assert.True(await game.StopAsync(CancellationToken.None));
            await game.DisposeAsync();
            Assert.False(host.StartedProcessHasExited);
            Assert.Equal(processId, host.StartedServerProcessId);
            Assert.Equal(proof, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            var status = await host.HostStatusAsync(CancellationToken.None);
            Assert.Equal(stopWhenGameQuits, status.StopWhenGameQuits);
            await api.ReconnectAsync(host.Origin, remotePairing.Authority, remote.DeviceId, 0, null, null, null,
                remoteSigner, CancellationToken.None);
            Assert.True(await host.StopAsync(CancellationToken.None));
            Assert.False(File.Exists(layout.SecretPath));
        }
        finally
        {
            await game.DisposeAsync();
            await host.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task HostedGameIntentDoesNotStartAReplacementWhenNoHostIsRunning()
    {
        var directory = Directory.CreateTempSubdirectory("host-pc-absent-");
        try
        {
            var (port, approval) = FreePorts();
            var layout = new LocalServerLayout(Path.Combine(directory.FullName, "must-not-start"), [], directory.FullName, directory.FullName);
            using var http = new HttpClient();
            await using var game = new LocalServer(layout, http, port, approval, joinHostedWorld: true);
            Assert.Equal(LocalServerStartOutcome.HostedWorldUnavailable,
                (await game.StartAsync("1.0.0", new string('a', 40), CancellationToken.None)).Outcome);
            Assert.False(game.StartedServer);
            Assert.False(File.Exists(layout.SecretPath));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task CancelledHostedModeReadLeavesTheHostAndItsProofAvailable()
    {
        var directory = Directory.CreateTempSubdirectory("host-pc-cancel-");
        var (port, approval) = FreePorts();
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName)
        { HostKind = "launcher" };
        using var http = new HttpClient();
        var host = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.True((await host.StartAsync(version, revision, CancellationToken.None)).IsReady);
            var proof = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var handler = new TimeoutOnceHandler("/api/v1/approval/hosting");
            using var delayedHttp = new HttpClient(handler);
            var game = new LocalServer(layout with { HostKind = null }, delayedHttp, port, approval, joinHostedWorld: true);
            using var cancel = new CancellationTokenSource();
            var starting = game.StartAsync(version, revision, cancel.Token);
            await ModelWorkHarness.WaitForSignalAsync(handler.Entered.Task);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
            await game.DisposeAsync();
            Assert.False(host.StartedProcessHasExited);
            Assert.Equal(proof, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            Assert.Equal("launcher", (await host.HostStatusAsync(CancellationToken.None)).Kind);
        }
        finally { await host.StopAsync(CancellationToken.None); directory.Delete(recursive: true); }
    }
}
