using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ProviderConfigurationStoreTests
{
    [Theory]
    [InlineData("jev-1.14.0")]
    [InlineData("jev-1.13.0")]
    public async Task SavedTypeSafeKeyRoutesTheSavedHelperModelAfterReload(string model)
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-typesafe-helper-");
        try
        {
            var path = Path.Combine(directory.FullName, "providers.json");
            var store = new ProviderConfigurationStore(path, EmptySeed());
            var slotId = Guid.NewGuid().ToString("N");
            const string secret = "synthetic-typesafe-named-key";
            store.CreateCredentialSlot(new(slotId, "jev", "TypeSafe account", secret));
            store = new ProviderConfigurationStore(path, EmptySeed());
            Assert.Equal(secret, Assert.Single(store.CaptureRuntimeConfiguration().CredentialSlots!).ApiKey);
            Assert.DoesNotContain(secret, System.Text.Json.JsonSerializer.Serialize(store.CaptureStatus()), StringComparison.Ordinal);
            if (OperatingSystem.IsWindows()) Assert.DoesNotContain(secret, File.ReadAllText(path), StringComparison.Ordinal);
            else Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));

            // A different installation key must not replace the explicitly selected one.
            store.Configure(new("routine", "jev", "different-default-model", "synthetic-default-key", false));
            var helper = new RoutineHelperSettings("jev", model, slotId);
            var policy = new WorldJevPolicy();
            policy.Initialize(true, 1, helper);
            var handler = new ProviderResponseHandler();
            var log = new RecordingLogger<ConfigurableDecisionProvider>();
            var router = new ConfigurableDecisionProvider(store, new ProviderHttpClientFactory(handler), log, jevPolicy: policy);
            var request = Request(router.ProviderEpoch);
            var response = await router.DecideAsync(request);
            Assert.Equal(DecisionProviderKind.Jev, response.Provider);
            Assert.Equal("safe_idle", response.SelectedCandidateId);
            Assert.Equal(PlayerDecisionProviders.JevEndpoint, handler.LastUri);
            Assert.Equal("Bearer " + secret, handler.LastAuthorization);
            Assert.Equal(model, handler.LastModel);
            Assert.DoesNotContain(log.Messages, message => message.Contains(secret, StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => store.DeleteCredentialSlot(slotId, helper));
            // TypeSafe remains a helper; it cannot become a personal model.
            Assert.Throws<ArgumentException>(() => store.Configure(new("personal", "jev", model, null, false, "agent-test", slotId)));

            // Missing named keys fail before HTTP, even with a usable installation key.
            store.DeleteCredentialSlot(slotId, RoutineHelperSettings.Off);
            await Assert.ThrowsAsync<CognitionProviderUnavailableException>(async () =>
                await router.DecideAsync(Request(router.ProviderEpoch)));
            Assert.Equal(1, handler.RequestCount);
        }
        finally { directory.Delete(recursive: true); }
    }
}
