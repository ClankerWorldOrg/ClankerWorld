using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Fact]
    public async Task DescendantConditionLogsRetainTheFullColonBearingIdentityWithoutPrivateText()
    {
        var directory = Directory.CreateTempSubdirectory("descendant-logs-");
        try
        {
            using var preparing = WorldRuntime.Restore(WorldRuntimeCodec.Decode(await BirthBeforeDueTick.Value), _ => new ParentProvider("safe_idle"));
            preparing.Resume();
            Assert.True((await preparing.AdvanceOneTickAsync()).Advanced);
            var child = Assert.Single(preparing.Society.Births).ChildId;
            Assert.Contains(':', child);
            var state = preparing.ExportState();
            state = state with
            {
                Agents = state.Agents.Select(person => person.AgentId == child ? person with
                {
                    FullnessBasisPoints = 9_000,
                    Personality = "private-child-secret",
                    Survival = person.Survival! with { WarmthBasisPoints = 2_490 },
                } : person).ToArray(),
            };
            using var world = WorldRuntime.Restore(state, _ => new ParentProvider("safe_idle"));
            var presence = new OwnerClientPresenceLease(TimeSpan.FromSeconds(30));
            presence.RecordAuthenticatedReconnect("owner");
            var logger = new RecordingLogger<WorldRuntimeService>();
            using var service = new WorldRuntimeService(world, new WorldStateFile(Path.Combine(directory.FullName, "world.json")), presence, logger);
            Assert.True(await service.TryAdvanceOnceAsync());
            Assert.Contains(logger.Messages, message => message.Contains("survival_condition", StringComparison.Ordinal) &&
                message.Contains("agent=" + child + " ", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("private-child-secret", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
