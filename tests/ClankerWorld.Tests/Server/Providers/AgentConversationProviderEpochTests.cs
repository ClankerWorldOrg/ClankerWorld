using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;

namespace ClankerWorld.Tests;

public sealed class AgentConversationProviderEpochTests
{
    [Fact]
    public async Task ReassignmentBeforeDispatchCannotChargeTheReplacementRoute()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-conversation-epoch-");
        var dispatchReady = new InlineCompletionSource(TaskCreationOptions.None);
        var assignmentChanged = new InlineCompletionSource(TaskCreationOptions.None);
        try
        {
            var store = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"), new(
                "deterministic", null, null, null, null, null, null));
            _ = store.Configure(Personal("original-model", "original-test-key"));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            var handler = new AgentConversationProviderTests.ConversationResponseHandler("A bounded reply.");
            IAgentConversationProvider provider = new ConfigurableDecisionProvider(
                store, new ProviderHttpClientFactory(handler), usageStore: usage);
            var issued = Request(provider.ProviderEpoch);
            var dispatch = Task.Run(async () =>
            {
                dispatchReady.SetResult();
                await assignmentChanged.Task;
                return await provider.SpeakAsync(issued);
            });

            await dispatchReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            _ = store.Configure(Personal("replacement-model", "replacement-test-key"));
            Assert.NotEqual(issued.ExpectedProviderEpoch, provider.ProviderEpoch);
            assignmentChanged.SetResult();

            await Assert.ThrowsAsync<ProviderConversationUnavailableException>(async () => await dispatch);
            Assert.Empty(handler.Models);
            Assert.Equal(0, usage.Capture().Attempts);
            Assert.Empty(usage.Capture().Rows);

            var response = await provider.SpeakAsync(Request(provider.ProviderEpoch));
            Assert.Equal("A bounded reply.", response.Text);
            Assert.Equal("replacement-model", Assert.Single(handler.Models));
            Assert.Equal("Bearer replacement-test-key", Assert.Single(handler.Authorizations));
            Assert.Equal(1, usage.Capture().Attempts);
            Assert.Equal(1, usage.Capture().Completed);
            Assert.Equal("replacement-model", Assert.Single(usage.Capture().Rows).Model);
        }
        finally
        {
            assignmentChanged.TrySetResult();
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task MissingExpectedEpochCannotStartAPaidConversationCall()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-conversation-missing-epoch-");
        try
        {
            var store = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"), new(
                "deterministic", null, null, null, null, null, null));
            _ = store.Configure(Personal("personal-model", "personal-test-key"));
            var usage = new ProviderUsageStore(Path.Combine(directory.FullName, "usage.json"));
            var handler = new AgentConversationProviderTests.ConversationResponseHandler("A bounded reply.");
            var provider = new ConfigurableDecisionProvider(
                store, new ProviderHttpClientFactory(handler), usageStore: usage);

            await Assert.ThrowsAsync<ProviderConversationUnavailableException>(async () =>
                await provider.SpeakAsync(Request(null)));

            Assert.Empty(handler.Models);
            Assert.Equal(0, usage.Capture().Attempts);
            Assert.Empty(usage.Capture().Rows);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static OwnerProviderConfigurationAction Personal(string model, string key) =>
        new("personal", "openai", model, key, false, "agent-a", Guid.NewGuid().ToString("N"), "Test key");

    private static AgentConversationTurnRequest Request(long? expectedEpoch) => new(
        Guid.NewGuid().ToString("N"), "conversation:epoch-test", 2, 4, 7,
        AgentConversationPurpose.PublicTurn, "agent-a", "Aster", "agent-b", "Rowan",
        "curious", "learn about the valley", [], [AgentConversationEffect.None])
    {
        ExpectedProviderEpoch = expectedEpoch,
    };

}
