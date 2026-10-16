using System.Net;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Providers;

namespace ClankerWorld.Tests;

public sealed class InvalidModelDecisionTests
{
    [Theory]
    [InlineData("never_offered", 1, "candidate_not_legal")]
    [InlineData("safe_idle", 1.2, "malformed_response")]
    [InlineData("safe_idle", 1, "provider_decision")]
    public async Task DeferredModelAnswerTerminatesOneMeteredDecision(string candidate, double confidence, string outcome)
    {
        var directory = Directory.CreateTempSubdirectory("invalid-hosted-");
        try
        {
            var configuration = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new ProviderConfigurationSeed("deterministic", null, null, null, null, null, null));
            configuration.Configure(new("personal", "openai", "test-model", "test-only-key", false, "founder-scout"));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            using var handler = new AnswerHandler(candidate, confidence);
            var router = new ConfigurableDecisionProvider(configuration, new ClientFactory(handler), usageStore: usage);
            // Keep peers from taking shared tools and changing the hosted
            // agent's choices while this single metered reply is pending.
            using var world = new WorldRuntime("invalid-hosted-answer", id =>
                id == "founder-scout" ? router : new ActionCoverageRecorder(chooseIdle: true));
            CognitionAdmissionResult? admission = null;
            var started = false;
            // Bound world progress and wait for the registered wrapper between ticks.
            for (var tick = 0; tick < 20 && admission is null; tick++)
            {
                var result = await world.TickModelWorkAsync();
                started |= result.Events.Any(item => item.Kind == "hosted_decision_started" && item.Detail == "founder-scout");
                admission = result.Decisions.FirstOrDefault(item => item.AgentId == "founder-scout")?.Admission;
                await ModelWorkHarness.For(world).WaitForDecisionAsync("founder-scout");
            }
            Assert.True(started);
            Assert.NotNull(admission);
            Assert.True(admission.Accepted);
            Assert.Equal(outcome, admission.Outcome);
            Assert.Equal(outcome != "provider_decision", admission.FellBack);
            Assert.Equal("safe_idle", admission.Intention!.CandidateId);
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(1, usage.Capture().Attempts);
            Assert.DoesNotContain(world.ExportState().Society.Cognition.Queue, item => item.AgentId == "founder-scout");
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class AnswerHandler(string candidate, double confidence) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var answer = JsonSerializer.Serialize(new
            {
                selected_candidate_id = candidate,
                confidence,
                probabilities = new Dictionary<string, double> { [candidate] = 1 },
            });
            var body = JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = answer } } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
