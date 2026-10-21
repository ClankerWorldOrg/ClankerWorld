using System.Net;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.Tests;

public sealed class OwnerPairingClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconnectBoundsStalledTransportAndHonorsOwnerCancellation(bool ownerCancels)
    {
        var clock = new ManualTimeProvider();
        using var handler = new StalledHandler();
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new OwnerWorldApi(client, clock);
        using var signer = new StubSigner();
        using var cancellation = new CancellationTokenSource();
        var first = api.ReconnectAsync(new Uri("http://127.0.0.1:5188"), new("server", "world"),
            "device", 0, null, null, null, signer, cancellation.Token);
        try
        {
            await ModelWorkHarness.WaitForSignalAsync(handler.Started.Task);
            clock.Advance(TimeSpan.FromMilliseconds(3999));
            Assert.False(first.IsCompleted);
            Assert.False(handler.RequestToken.IsCancellationRequested);
            if (ownerCancels) cancellation.Cancel();
            else clock.Advance(TimeSpan.FromMilliseconds(1));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModelWorkHarness.WaitForSignalAsync(first));
            Assert.True(handler.RequestToken.IsCancellationRequested);
            Assert.Equal(ownerCancels, cancellation.IsCancellationRequested);
        }
        finally { cancellation.Cancel(); }
        Assert.True(handler.WasCancelled);
        // A cancelled poll does not poison the shared client used by pause and later polls.
        using var response = await client.GetAsync("http://127.0.0.1:5188/next");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class StalledHandler : HttpMessageHandler
    {
        public InlineCompletionSource Started { get; } = new();
        public bool WasCancelled { get; private set; }
        public CancellationToken RequestToken { get; private set; }
        private readonly InlineCompletionSource<HttpResponseMessage> stalled = new();
        private CancellationTokenRegistration cancellation;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/next") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            RequestToken = cancellationToken;
            // Cancellation completes transport inline, even when the runner's context is busy.
            cancellation = cancellationToken.Register(() =>
            {
                WasCancelled = true;
                stalled.TrySetCanceled(cancellationToken);
            });
            Started.TrySetResult();
            return stalled.Task;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) cancellation.Dispose();
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task PairingRefusesPlaintextNonLoopbackTransportBeforeItCanSendAKey()
    {
        using var client = new HttpClient(new FailingHandler());
        var pairingClient = new OwnerPairingClient(client);
        using var signer = new StubSigner();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => pairingClient.StartPairingAsync(
            new Uri("http://example.test:5188"),
            signer,
            CancellationToken.None));

        Assert.Contains("requires HTTPS", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubSigner : IOwnerDeviceSigner
    {
        public string PublicKeySpkiBase64 => "not-used-before-transport-validation";

        public string PublicKeyFingerprint => "not-used-before-transport-validation";

        public string SignCanonicalProof(string canonicalProof) => "test-signature";

        public void Dispose()
        {
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("Transport must not be reached for an insecure remote URI.");
    }
}
