using System.Net;
using System.Net.Http.Json;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Protocol;

namespace ClankerWorld.Tests;

public sealed class WorldCatalogDeadlineTests
{
    [Fact]
    public async Task FirstCatalogReplyBeyondOrdinaryActionDeadlinePopulatesVisibleRequest()
    {
        var clock = new ManualTimeProvider();
        using var handler = new CatalogHandler(clock, TimeSpan.FromSeconds(16));
        using var client = new HttpClient(handler);
        using var signer = new StubOwnerDeviceSigner();
        using var request = new WorldListRequest();
        var api = new OwnerWorldApi(client, clock);
        var loading = request.RefreshAsync(token => api.ListWorldsAsync(
            new("http://127.0.0.1:5188"), new("server", "world"), "device", signer, token));
        await ModelWorkHarness.WaitForSignalAsync(handler.Started.Task);
        Assert.True(request.IsLoading);
        clock.Advance(TimeSpan.FromSeconds(16));
        await ModelWorkHarness.WaitForSignalAsync(loading);
        Assert.Null(request.Failure);
        Assert.False(request.IsLoading);
        Assert.Equal("active", request.Catalog!.ActiveId);
        Assert.Equal("incompatible", Assert.Single(request.Catalog.Worlds).Compatibility);
        Assert.Equal("Cannot restore this checkpoint.", request.Catalog.Worlds[0].CompatibilityReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CatalogHonorsListDeadlineAndScreenCancellationAfterASlowChallenge(bool screenCancels) =>
        // Keep completion independent of the runner's synchronization context.
        Task.Run(() => AssertCatalogCancellationAsync(screenCancels));

    private static async Task AssertCatalogCancellationAsync(bool screenCancels)
    {
        var clock = new ManualTimeProvider();
        using var handler = new CatalogHandler(clock, Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(1));
        using var client = new HttpClient(handler);
        using var signer = new StubOwnerDeviceSigner();
        using var request = new WorldListRequest();
        using var listDeadline = new CancellationTokenSource();
        var api = new OwnerWorldApi(client, clock);
        var loading = request.RefreshAsync(async token =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, listDeadline.Token);
            return await api.ListWorldsAsync(new("http://127.0.0.1:5188"),
                new("server", "world"), "device", signer, linked.Token);
        });
        try
        {
            await ModelWorkHarness.WaitForSignalAsync(handler.ChallengeStarted.Task);
            Assert.True(request.IsLoading);
            Assert.False(loading.IsCompleted);
            Assert.False(handler.Started.Task.IsCompleted);
            Assert.False(listDeadline.IsCancellationRequested);
            clock.Advance(TimeSpan.FromSeconds(1));
            var first = await ModelWorkHarness.WaitForSignalAsync(Task.WhenAny(handler.Started.Task, loading));
            if (first == loading)
            {
                await ModelWorkHarness.WaitForSignalAsync(loading);
                Assert.Fail($"Catalog request completed before the list request began: {request.Failure}");
            }
            Assert.True(request.IsLoading);
            // The list's real 60-second deadline includes the one-second challenge.
            if (screenCancels) request.Cancel();
            else clock.Advance(TimeSpan.FromSeconds(59));
            await ModelWorkHarness.WaitForSignalAsync(loading);
        }
        finally { listDeadline.Cancel(); }
        Assert.True(handler.Cancelled);
        Assert.False(request.IsLoading);
        Assert.Null(request.Catalog);
        if (screenCancels) Assert.Null(request.Failure);
        else Assert.IsAssignableFrom<OperationCanceledException>(request.Failure);
        var receipt = await api.SetPausedAsync(new("http://127.0.0.1:5188"),
            new("server", "world"), "device", "viewed-world", true, signer, CancellationToken.None);
        Assert.True(receipt.IsPaused);
    }

    private sealed class CatalogHandler(TimeProvider clock, TimeSpan delay, TimeSpan firstChallengeDelay = default) : HttpMessageHandler
    {
        public InlineCompletionSource ChallengeStarted { get; } = new();
        public InlineCompletionSource Started { get; } = new(TaskCreationOptions.None);
        public bool Cancelled { get; private set; }
        private bool firstChallenge = true;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == OwnerPairingEndpoints.ChallengeIssue)
            {
                if (firstChallenge)
                {
                    firstChallenge = false;
                    var challengeDelay = clock.DelayAsync(firstChallengeDelay, cancellationToken);
                    ChallengeStarted.TrySetResult();
                    await challengeDelay.ConfigureAwait(false);
                }
                return Reply(new OwnerChallenge(new("server", "world"), "device", "challenge", "nonce", DateTimeOffset.UtcNow.AddMinutes(1)) { SupportedActionPayloads = [OwnerWorldActionPayload.WorldControlPayloadDomain] });
            }
            if (request.RequestUri.AbsolutePath != OwnerPairingEndpoints.OwnerWorldList)
                return Reply(new OwnerControlReceipt("pause", true, true, 1, 1, 1));
            if (delay == Timeout.InfiniteTimeSpan)
            {
                // Retain main's direct cancellation completion: no queued delay
                // continuation sits between cancellation and the held HTTP reply.
                var cancelled = new TaskCompletionSource<HttpResponseMessage>();
                using var registration = cancellationToken.Register(() =>
                {
                    Cancelled = true;
                    cancelled.TrySetCanceled(cancellationToken);
                });
                Started.TrySetResult();
                return await cancelled.Task.ConfigureAwait(false);
            }
            var replyDelay = clock.DelayAsync(delay, cancellationToken);
            Started.TrySetResult();
            try { await replyDelay.ConfigureAwait(false); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            return Reply(new WorldCatalogSnapshot("active",
                [new CatalogWorld("blocked", "Blocked world", "world", "seed", DateTimeOffset.UnixEpoch, [], null,
                    "incompatible", "Cannot restore this checkpoint.")]));
        }
        private static HttpResponseMessage Reply<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
