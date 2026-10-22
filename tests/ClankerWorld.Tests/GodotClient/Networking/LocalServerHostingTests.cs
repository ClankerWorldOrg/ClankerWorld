using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.Tests;

public sealed partial class LocalServerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReusedHostShutdownTimeoutPreservesALiveHostAndRecognizesAnAlreadyGoneHost(bool alreadyGone)
    {
        var directory = Directory.CreateTempSubdirectory("launcher-host-stop-timeout-");
        var (port, approval) = FreePorts();
        var (version, revision) = BuildOf();
        var layout = NativeLayout(directory.FullName) with
        { HostKind = "launcher" };
        using var http = new HttpClient();
        var owner = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.Equal(LocalServerStartOutcome.Started, (await owner.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var proofDigest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var handler = new TimeoutOnceHandler("/api/v1/approval/shutdown");
            using var delayedHttp = new HttpClient(handler);
            var clock = new ManualTimeProvider();
            var reused = new LocalServer(layout, delayedHttp, port, approval, timeProvider: clock);
            Assert.Equal(LocalServerStartOutcome.Reused, (await reused.StartAsync(version, revision, CancellationToken.None)).Outcome);
            if (alreadyGone) Assert.True(await owner.StopAsync(CancellationToken.None));
            var stopping = reused.StopAsync(CancellationToken.None);
            await ModelWorkHarness.WaitForSignalAsync(handler.Entered.Task);
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(alreadyGone, await ModelWorkHarness.WaitForSignalAsync(stopping));
            if (!alreadyGone)
            {
                Assert.Equal(proofDigest, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
                Assert.True((await http.GetAsync(new Uri(owner.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
                Assert.True(await ModelWorkHarness.WaitForSignalAsync(reused.StopAsync(CancellationToken.None)));
            }
        }
        finally
        {
            await owner.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task HostedServerKeepsModeApprovalAndDeclineBoundToThePrivateListener()
    {
        var directory = Directory.CreateTempSubdirectory("launcher-host-http-");
        var (port, approval) = FreePorts();
        var (version, revision) = BuildOf();
        var layout = NativeLayout(directory.FullName) with
        { HostKind = "launcher" };
        using var http = new HttpClient();
        var host = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.True((await host.StartAsync(version, revision, CancellationToken.None)).IsReady);
            var relaunch = new LocalServer(layout, http, port, approval);
            Assert.Equal(LocalServerStartOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
            var game = new LocalServer(layout with { HostKind = null }, http, port, approval);
            Assert.Equal(LocalServerStartOutcome.Reused, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True(game.JoinedHostedServer);
            Assert.True(await game.StopAsync(CancellationToken.None));
            using var publicList = await http.GetAsync(new Uri(host.Origin, "api/v1/approval/pairings"));
            Assert.Equal(HttpStatusCode.NotFound, publicList.StatusCode);
            using var missingProof = await http.GetAsync($"http://127.0.0.1:{approval}/api/v1/approval/pairings");
            Assert.Equal(HttpStatusCode.NotFound, missingProof.StatusCode);
            using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var firstSigner = new Signer(firstKey);
            using var secondSigner = new Signer(secondKey);
            var pairing = new OwnerPairingClient(http);
            var first = await pairing.StartPairingAsync(host.Origin, firstSigner, CancellationToken.None);
            var second = await pairing.StartPairingAsync(host.Origin, secondSigner, CancellationToken.None);
            var pending = await host.PendingPairingsAsync(CancellationToken.None);
            Assert.Equal(2, pending.Length);
            Assert.Equal(first.PairingCode, pending.Single(item => item.PairingId == first.PairingId).PairingCode);
            Assert.True(await host.DeclinePairingAsync(second.PairingId, CancellationToken.None));
            Assert.False(await host.ApprovePairingAsync(second.PairingId, second.PairingCode, CancellationToken.None));
            Assert.True(await host.ApprovePairingAsync(first.PairingId, first.PairingCode, CancellationToken.None));
            await pairing.ActivatePairingAsync(host.Origin, first, firstSigner, CancellationToken.None);
            Assert.Empty(await host.PendingPairingsAsync(CancellationToken.None));
            Assert.Equal("launcher", (await host.HostStatusAsync(CancellationToken.None)).Kind);
            Assert.True((await http.GetAsync(new Uri(host.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            Assert.DoesNotContain(File.ReadAllText(layout.SecretPath), await ReadSharedAsync(layout.LogPath), StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task AGameServerRefusesAForwardedHostNameAndCannotBeReusedAsALauncherHost()
    {
        var directory = Directory.CreateTempSubdirectory("local-game-mode-");
        var (port, approval) = FreePorts();
        var (version, revision) = BuildOf();
        var layout = NativeLayout(directory.FullName);
        using var http = new HttpClient();
        var game = new LocalServer(layout, http, port, approval);
        try
        {
            Assert.True((await game.StartAsync(version, revision, CancellationToken.None)).IsReady);
            using var forwarded = new HttpRequestMessage(HttpMethod.Get, new Uri(game.Origin, "api/v1/handshake"));
            forwarded.Headers.Host = "world.example.ts.net";
            using var refused = await http.SendAsync(forwarded);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            var host = new LocalServer(layout with { HostKind = "launcher" }, http, port, approval);
            Assert.Equal(LocalServerStartOutcome.ModeInUse, (await host.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.True((await http.GetAsync(new Uri(game.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
        }
        finally { await game.StopAsync(CancellationToken.None); directory.Delete(recursive: true); }
    }
}
