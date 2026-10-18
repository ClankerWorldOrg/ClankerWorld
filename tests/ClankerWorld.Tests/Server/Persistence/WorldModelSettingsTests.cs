using System.Text.Json;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldModelSettingsTests
{
    [Fact]
    public void ApprovalBindsTargetModelKeyAndThinkingWithoutIncludingCredentialBytes()
    {
        var directory = Directory.CreateTempSubdirectory("model-preview-");
        try
        {
            using var world = new WorldRuntime("model-preview");
            world.Pause();
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));
            var keyId = Guid.NewGuid().ToString("N");
            const string fakeCredential = "offline-test-credential-never-sent";
            providers.CreateCredentialSlot(new(keyId, "openai", "Test planning key", fakeCredential));
            var assignment = new AgentProviderAssignment(world.Agents[0].AgentId, "planning", "openai", "test-model", keyId, Thinking: "low");
            var target = world.ExportState();
            var preview = WorldModelSettings.Preview("save:chosen", target, [assignment], world, providers);
            Assert.True(preview.RequiresConfirmation);
            Assert.Contains(preview.Details, line => line.Contains("Test planning key", StringComparison.Ordinal) &&
                line.Contains("test-model", StringComparison.Ordinal) && line.Contains("thinking low", StringComparison.Ordinal));
            Assert.DoesNotContain(fakeCredential, JsonSerializer.Serialize(preview));
            WorldModelSettings.RequireApproval(preview, preview.Approval);
            foreach (var changed in new[]
            {
                WorldModelSettings.Preview("save:another", target, [assignment], world, providers),
                WorldModelSettings.Preview("save:chosen", target, [assignment with { Model = "other-model" }], world, providers),
                WorldModelSettings.Preview("save:chosen", target, [assignment with { CredentialSlotId = null }], world, providers),
                WorldModelSettings.Preview("save:chosen", target, [assignment with { Thinking = "high" }], world, providers),
                WorldModelSettings.Preview("save:chosen", target with { RoutineHelper = RoutineHelperSettings.Off }, [assignment], world, providers),
            })
                Assert.Throws<ModelSettingsConfirmationRequiredException>(() => WorldModelSettings.RequireApproval(changed, preview.Approval));
            var unchanged = WorldModelSettings.Preview("save:unchanged", target, [], world, providers);
            Assert.False(unchanged.RequiresConfirmation);
            WorldModelSettings.RequireApproval(unchanged, null);
        }
        finally { directory.Delete(true); }
    }
}
