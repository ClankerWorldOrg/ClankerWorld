using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Fact]
    public async Task DescendantConditionLogsRetainTheFullColonBearingIdentityWithoutPrivateText()
    {
        var directory = Directory.CreateTempSubdirectory("descendant-logs-");
        try
        {
            var state = await PreparedState();
            var first = state.Agents[0].AgentId;
            using var preparing = WorldRuntime.Restore(state, actor => new ParentProvider(actor == first ? "parent_propose:" : "parent_accept:"));
            for (var tick = 0; tick < 605 && preparing.Society.Births.Count == 0; tick++) await preparing.AdvanceOneTickAsync();
            var child = Assert.Single(preparing.Society.Births).ChildId;
            Assert.Contains(':', child);
            state = preparing.ExportState();
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
