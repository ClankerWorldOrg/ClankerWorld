using System.Net;
using System.Text.Json;
using ClankerWorld.Server.Providers;

namespace ClankerWorld.Tests;

public sealed class OpenAiFailureTests
{
    [Theory]
    [InlineData(401, "invalid_api_key", null, "invalid_key", "Replace it")]
    [InlineData(401, null, null, "authentication_failed", "allowed IP")]
    [InlineData(403, null, null, "access_denied", "permissions")]
    [InlineData(404, "model_not_found", null, "model_not_available", "model name")]
    [InlineData(408, null, null, "timed_out", "didn't answer in time")]
    [InlineData(504, null, null, "timed_out", "didn't answer in time")]
    [InlineData(429, "credit_balance_exhausted", "insufficient_quota", "no_credit", "billing balance")]
    [InlineData(429, "project_spend_limit_exceeded", "insufficient_quota", "project_spend_limit", "project reached")]
    [InlineData(429, "organization_spend_limit_exceeded", "insufficient_quota", "organization_spend_limit", "organization reached")]
    [InlineData(429, "organization_usage_limit_exceeded", "insufficient_quota", "provider_usage_limit", "Ask your provider")]
    [InlineData(429, "insufficient_quota", null, "quota_exceeded", "billing, credits")]
    [InlineData(429, null, "insufficient_quota", "quota_exceeded", "billing, credits")]
    [InlineData(429, "rate_limit_exceeded", null, "rate_limited", "Wait before retrying")]
    [InlineData(429, "slow_down", "rate_limit_error", "rate_limited", "Wait before retrying")]
    [InlineData(500, "invalid_api_key", null, "service_unavailable", "temporarily unavailable")]
    [InlineData(503, "server_is_overloaded", "service_unavailable_error", "service_unavailable", "Wait and try again")]
    public async Task ListsAndModelTestsExplainTheSameFailureWithoutLeakingOrRetrying(
        int status, string? code, string? type, string outcome, string guidance)
    {
        var body = JsonSerializer.Serialize(new { error = new { code, type, message = "private-provider-text fake-api-secret" } });
        await CheckAsync(status, body, outcome, guidance);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"error\":{\"code\":42,\"type\":[]}}")]
    [InlineData("{\"error\":\"private-provider-text\"}")]
    public async Task UnreadableErrorsStillExplainTheHttpStatus(string body) =>
        await CheckAsync(403, body, "access_denied", "permissions");

    [Fact]
    public async Task OversizedErrorStillExplainsTheHttpStatus() =>
        await CheckAsync(403, new string('x', 300_000), "access_denied", "permissions");

    private static async Task CheckAsync(int status, string body, string outcome, string guidance)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-openai-errors-");
        try
        {
            var configuration = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new ProviderConfigurationSeed("deterministic", null, null, null, null, null, null));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            using var handler = new Handler((HttpStatusCode)status, body);
            var factory = new ProviderHttpClientFactory(handler);
            var catalog = new ProviderModelCatalog(configuration, factory);
            var listed = await catalog.ListAsync(new("openai", ApiKey: "fake-api-secret"), CancellationToken.None);
            Assert.Contains(guidance, listed.Error, StringComparison.Ordinal);
            Assert.All(listed.Models, item => Assert.True(item.Available));
            Assert.Equal(0, usage.Capture().Attempts);
            var result = await new ProviderSetupCheckService(configuration, usage, factory)
                .CheckAsync(new("openai", "fake-model", ApiKey: "fake-api-secret"), CancellationToken.None);
            Assert.Equal(outcome, result.Outcome);
            Assert.Contains(guidance, result.Message, StringComparison.Ordinal);
            Assert.Contains("still counts as a model call", result.Message, StringComparison.Ordinal);
            Assert.False(result.IsReady);
            Assert.Equal(1, usage.Capture().Attempts);
            Assert.Equal(1, usage.Capture().Failed);
            var publicJson = JsonSerializer.Serialize(new { listed, result });
            Assert.DoesNotContain("fake-api-secret", publicJson, StringComparison.Ordinal);
            Assert.DoesNotContain("private-provider-text", publicJson, StringComparison.Ordinal);
            Assert.DoesNotContain(body, publicJson, StringComparison.Ordinal);
            // Failures are not cached, and neither route silently retries.
            _ = await catalog.ListAsync(new("openai", ApiKey: "fake-api-secret"), CancellationToken.None);
            Assert.Equal(3, handler.Requests);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
