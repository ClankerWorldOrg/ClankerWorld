using System.Net;
using System.Net.Http.Json;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Protocol;

namespace ClankerWorld.Tests;

public sealed class OwnerActionBodyDeadlineTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public Task StalledBodyReleasesGateAndNextPauseSucceeds(bool challengeBody, bool callerCancels) =>
        // The response read and gate must observe cancellation without waiting for the test
        // runner's synchronization context, which other parallel tests can keep busy.
        Task.Run(() => AssertStalledBodyReleasesGateAsync(challengeBody, callerCancels));

    private static async Task AssertStalledBodyReleasesGateAsync(bool challengeBody, bool callerCancels)
    {
        var clock = new ManualTimeProvider();
        using var handler = new HeldBodyHandler(challengeBody);
        using var client = new HttpClient(handler)
        {
            Timeout = callerCancels ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(500),
        };
        using var signer = new Signer();
        using var cancellation = new CancellationTokenSource();
        var api = new OwnerWorldApi(client, clock);
        var gate = new OwnerActionGate();
        var first = gate.RunAsync(async () =>
            await api.SetPausedAsync(new("http://127.0.0.1:5188"), new("server", "world"), "device", true, signer, cancellation.Token));
        await handler.Body.Started.Task;
        if (callerCancels) cancellation.Cancel();
        else clock.Advance(TimeSpan.FromMilliseconds(500));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.True(handler.Body.ReadToken.IsCancellationRequested);
            Assert.Equal(callerCancels, cancellation.IsCancellationRequested);
        }
        finally { cancellation.Cancel(); }
        Assert.True(handler.Body.Cancelled);
        OwnerControlReceipt? receipt = null;
        Assert.True(await gate.RunAsync(async () => receipt = await api.SetPausedAsync(
            new("http://127.0.0.1:5188"), new("server", "world"), "device", true, signer, CancellationToken.None), waitForTurn: true));
        Assert.True(receipt!.IsPaused);
        Assert.Equal(challengeBody ? 1 : 2, handler.PauseRequests);
    }

    private sealed class HeldBodyHandler(bool challengeBody) : HttpMessageHandler
    {
        public HeldBody Body { get; } = new();
        private bool stalled;
        public int PauseRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var challenge = request.RequestUri!.AbsolutePath == OwnerPairingEndpoints.ChallengeIssue;
            if (!challenge) PauseRequests++;
            HttpContent content;
            if (!stalled && challenge == challengeBody)
            {
                stalled = true;
                content = new StreamContent(Body);
                content.Headers.ContentType = new("application/json");
            }
            else content = challenge
                ? JsonContent.Create(new OwnerChallenge(new("server", "world"), "device", "challenge", "nonce", DateTimeOffset.UtcNow.AddMinutes(1)))
                : JsonContent.Create(new OwnerControlReceipt("pause", true, true, 1, 1, 1));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class HeldBody : Stream
    {
        public InlineCompletionSource Started { get; } = new(TaskCreationOptions.None);
        public bool Cancelled { get; private set; }
        public CancellationToken ReadToken { get; private set; }
        // Complete the read from cancellation itself, without a second queued
        // fixture continuation competing with the outer diagnostic deadline.
        private readonly InlineCompletionSource<int> read = new();
        private CancellationTokenRegistration cancellation;
        private bool began;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!began)
            {
                began = true;
                buffer.Span[0] = (byte)'{';
                return ValueTask.FromResult(1);
            }
            ReadToken = cancellationToken;
            cancellation = cancellationToken.Register(() =>
            {
                Cancelled = true;
                read.TrySetCanceled(cancellationToken);
            });
            Started.TrySetResult();
            return new(read.Task);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        protected override void Dispose(bool disposing)
        {
            if (disposing) cancellation.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class Signer : IOwnerDeviceSigner
    {
        public string PublicKeySpkiBase64 => "fixture-key";
        public string PublicKeyFingerprint => "fixture-fingerprint";
        public string SignCanonicalProof(string canonicalProof) => "fixture-signature";
        public void Dispose() { }
    }
}
