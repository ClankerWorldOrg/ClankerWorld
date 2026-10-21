using System.Net;
using System.Text;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.Contracts;

namespace ClankerWorld.Tests;

public sealed class WorldEditRefusalTests
{
    [Theory]
    [InlineData("{\"code\":\"terrain_refused\",\"detail\":\"Peaks can't hold a building.\"}", "Peaks can't hold a building.")]
    [InlineData("{\"error\":\"Pause the world before editing.\"}", "Pause the world before editing.")]
    [InlineData("{\"errors\":{\"action\":[\"Choose tiles inside the map.\"]}}", "Choose tiles inside the map.")]
    [InlineData("{\"detail\":\" \",\"error\":\"Choose an empty tile.\"}", "Choose an empty tile.")]
    [InlineData("{\"errors\":{\"action\":null}}", "The server could not accept this edit. Refresh the world, then try again.")]
    [InlineData("{", "The server could not accept this edit. Refresh the world, then try again.")]
    [InlineData("{}", "The server could not accept this edit. Refresh the world, then try again.")]
    public async Task ConflictIsACompletedRefusalWithTheServersReason(string body, string expected)
    {
        using var http = new HttpClient(new ReplyHandler(HttpStatusCode.Conflict, body));
        var pairing = new OwnerPairingClient(http);
        var failure = await Assert.ThrowsAsync<OwnerWorldEditRefusedException>(() => Submit(pairing));
        Assert.Equal(expected, failure.Message);
    }

    [Fact]
    public async Task AReceivedRefusalStaysDefinitiveWhenItsBodyCannotBeRead()
    {
        using var http = new HttpClient(new ReplyHandler(HttpStatusCode.Conflict, "", new UnreadableContent()));
        var failure = await Assert.ThrowsAsync<OwnerWorldEditRefusedException>(() => Submit(new OwnerPairingClient(http)));
        Assert.Equal("The server could not accept this edit. Refresh the world, then try again.", failure.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task OtherDefinitiveRefusalsAlsoKeepTheServersReason(HttpStatusCode status)
    {
        using var http = new HttpClient(new ReplyHandler(status, "{\"detail\":\"Refresh the world before editing.\"}"));
        var failure = await Assert.ThrowsAsync<OwnerWorldEditRefusedException>(() => Submit(new OwnerPairingClient(http)));
        Assert.Equal("Refresh the world before editing.", failure.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task UnconfirmedRepliesKeepTheirHttpFailure(HttpStatusCode status)
    {
        using var http = new HttpClient(new ReplyHandler(status, "{}"));
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => Submit(new OwnerPairingClient(http)));
        Assert.Equal(status, failure.StatusCode);
    }

    private static Task<OwnerAuthoringBatchReceipt> Submit(OwnerPairingClient pairing) =>
        pairing.PostSignedActionAsync<OwnerAuthoringBatchAction, OwnerAuthoringBatchReceipt>(
            new("http://127.0.0.1:5188"), OwnerPairingEndpoints.OwnerAuthoring,
            new("device", "challenge", "nonce", "binding", "proof", "signature", "request",
                new("batch", [new("set_terrain", null, "meadow", null, 1, 1, null)])), CancellationToken.None);

    private sealed class UnreadableContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new IOException("Controlled refused-response read failure."));
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class ReplyHandler(HttpStatusCode status, string body, HttpContent? content = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = content ?? new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
