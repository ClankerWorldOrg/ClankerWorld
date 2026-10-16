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
    [InlineData("{", "The server could not accept this edit. Refresh the world, then try again.")]
    [InlineData("{}", "The server could not accept this edit. Refresh the world, then try again.")]
    public async Task ConflictIsACompletedRefusalWithTheServersReason(string body, string expected)
    {
        using var http = new HttpClient(new ReplyHandler(HttpStatusCode.Conflict, body));
        var pairing = new OwnerPairingClient(http);
        var failure = await Assert.ThrowsAsync<OwnerWorldEditRefusedException>(() => Submit(pairing));
        Assert.Equal(expected, failure.Message);
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

    private sealed class ReplyHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
