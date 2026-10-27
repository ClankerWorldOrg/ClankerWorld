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
        await using var fixture = new NativeServerFixture("launcher-host-stop-timeout-", "launcher");
        var (version, revision) = BuildOf();
        Assert.Equal(LocalServerStartOutcome.Started, (await fixture.Server.StartAsync(version, revision, CancellationToken.None)).Outcome);
        var proofDigest = SHA256.HashData(File.ReadAllBytes(fixture.Layout.SecretPath));
        using var handler = new TimeoutOnceHandler("/api/v1/approval/shutdown");
        using var delayedHttp = new HttpClient(handler);
        var clock = new ManualTimeProvider();
        var reused = new LocalServer(fixture.Layout, delayedHttp, fixture.Port, fixture.Approval, timeProvider: clock);
        Assert.Equal(LocalServerStartOutcome.Reused, (await reused.StartAsync(version, revision, CancellationToken.None)).Outcome);
        if (alreadyGone) Assert.True(await fixture.Server.StopAsync(CancellationToken.None));
        var stopping = reused.StopAsync(CancellationToken.None);
        await ModelWorkHarness.WaitForSignalAsync(handler.Entered.Task);
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(alreadyGone, await ModelWorkHarness.WaitForSignalAsync(stopping));
        if (!alreadyGone)
        {
            Assert.Equal(proofDigest, SHA256.HashData(File.ReadAllBytes(fixture.Layout.SecretPath)));
            Assert.True((await fixture.Http.GetAsync(new Uri(fixture.Server.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
            Assert.True(await ModelWorkHarness.WaitForSignalAsync(reused.StopAsync(CancellationToken.None)));
        }
    }

    [Fact]
    public async Task HostedServerKeepsModeApprovalAndDeclineBoundToThePrivateListener()
    {
        await using var fixture = new NativeServerFixture("launcher-host-http-", "launcher");
        var (version, revision) = BuildOf();
        Assert.True((await fixture.Server.StartAsync(version, revision, CancellationToken.None)).IsReady);
        var relaunch = new LocalServer(fixture.Layout, fixture.Http, fixture.Port, fixture.Approval);
        Assert.Equal(LocalServerStartOutcome.Reused, (await relaunch.StartAsync(version, revision, CancellationToken.None)).Outcome);
        var game = new LocalServer(fixture.Layout with { HostKind = null }, fixture.Http, fixture.Port, fixture.Approval);
        Assert.Equal(LocalServerStartOutcome.Reused, (await game.StartAsync(version, revision, CancellationToken.None)).Outcome);
        Assert.True(game.JoinedHostedServer);
        Assert.True(await game.StopAsync(CancellationToken.None));
        using var publicList = await fixture.Http.GetAsync(new Uri(fixture.Server.Origin, "api/v1/approval/pairings"));
        Assert.Equal(HttpStatusCode.NotFound, publicList.StatusCode);
        using var missingProof = await fixture.Http.GetAsync($"http://127.0.0.1:{fixture.Approval}/api/v1/approval/pairings");
        Assert.Equal(HttpStatusCode.NotFound, missingProof.StatusCode);
        using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var firstSigner = new Signer(firstKey);
        using var secondSigner = new Signer(secondKey);
        var pairing = new OwnerPairingClient(fixture.Http);
        var first = await pairing.StartPairingAsync(fixture.Server.Origin, firstSigner, CancellationToken.None);
        var second = await pairing.StartPairingAsync(fixture.Server.Origin, secondSigner, CancellationToken.None);
        var pending = await fixture.Server.PendingPairingsAsync(CancellationToken.None);
        Assert.Equal(2, pending.Length);
        Assert.Equal(first.PairingCode, pending.Single(item => item.PairingId == first.PairingId).PairingCode);
        Assert.True(await fixture.Server.DeclinePairingAsync(second.PairingId, CancellationToken.None));
        Assert.False(await fixture.Server.ApprovePairingAsync(second.PairingId, second.PairingCode, CancellationToken.None));
        Assert.True(await fixture.Server.ApprovePairingAsync(first.PairingId, first.PairingCode, CancellationToken.None));
        await pairing.ActivatePairingAsync(fixture.Server.Origin, first, firstSigner, CancellationToken.None);
        Assert.Empty(await fixture.Server.PendingPairingsAsync(CancellationToken.None));
        Assert.Equal("launcher", (await fixture.Server.HostStatusAsync(CancellationToken.None)).Kind);
        Assert.True((await fixture.Http.GetAsync(new Uri(fixture.Server.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
        Assert.DoesNotContain(File.ReadAllText(fixture.Layout.SecretPath), await ReadSharedAsync(fixture.Layout.LogPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AGameServerRefusesAForwardedHostNameAndCannotBeReusedAsALauncherHost(bool timeoutOnce)
    {
        await using var fixture = new NativeServerFixture("local-game-mode-");
        var (version, revision) = BuildOf();
        Assert.True((await fixture.Server.StartAsync(version, revision, CancellationToken.None)).IsReady);
        using var forwarded = new HttpRequestMessage(HttpMethod.Get, new Uri(fixture.Server.Origin, "api/v1/handshake"));
        forwarded.Headers.Host = "world.example.ts.net";
        using var refused = await fixture.Http.SendAsync(forwarded);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        using var handler = new TimeoutOnceHandler("/api/v1/approval/hosting");
        using var modeHttp = timeoutOnce ? new HttpClient(handler) : new HttpClient();
        var clock = new RetryClock();
        var host = new LocalServer(fixture.Layout with { HostKind = "launcher" }, modeHttp, fixture.Port, fixture.Approval, timeProvider: timeoutOnce ? clock : null);
        Assert.Equal(LocalServerStartOutcome.ModeInUse, (timeoutOnce ? await clock.RetryAsync(host, handler, "/api/v1/approval/hosting")
            : await host.StartAsync(version, revision, CancellationToken.None)).Outcome);
        if (timeoutOnce) Assert.True(handler.TargetRequests >= 2);
        Assert.False(host.StartedServer);
        Assert.True(await host.StopAsync(CancellationToken.None));
        Assert.True((await fixture.Http.GetAsync(new Uri(fixture.Server.Origin, "api/v1/handshake"))).IsSuccessStatusCode);
    }
}
