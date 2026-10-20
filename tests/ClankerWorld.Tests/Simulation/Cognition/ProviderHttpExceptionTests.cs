using System.Net;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

public sealed class ProviderHttpExceptionTests
{
    [Fact]
    public async Task ExceptionAndRuntimeOutcomeNeverRetainRemoteText()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":{"code":"credit_balance_exhausted","message":"fake-key private-thought","type":"insufficient_quota"}}"""),
        };
        var failure = await ProviderHttpException.ReadAsync(response, CancellationToken.None);
        Assert.Equal("no_credit", failure.Failure);
        Assert.Equal(HttpStatusCode.TooManyRequests, failure.StatusCode);
        Assert.DoesNotContain("fake-key", failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-thought", failure.ToString(), StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
        Assert.Equal("model_unavailable", CognitionProviderFailures.FromException(failure, CancellationToken.None));
    }

    [Fact]
    public async Task CancellationWhileReadingAnErrorIsPreserved()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ProviderHttpException.ReadAsync(response, cancelled.Token));
    }
}
