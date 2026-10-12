using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;
using ClankerWorld.Server.Saves;

namespace ClankerWorld.Tests;

public sealed partial class SettlementParenthoodTests
{
    [Fact]
    public async Task DescendantConditionLogsRetainTheFullColonBearingIdentityWithoutPrivateText()
    {
        var directory = Directory.CreateTempSubdirectory("descendant-logs-");
        try
        {
            var state = await PreparedState();
            var first = state.Inhabitants[0].InhabitantId;
            using var preparing = WorldRuntime.Restore(state, actor => new ParentProvider(actor == first ? "parent_propose:" : "parent_accept:"));
            for (var tick = 0; tick < 605 && preparing.Society.Births.Count == 0; tick++) await preparing.AdvanceOneTickAsync();
            var child = Assert.Single(preparing.Society.Births).ChildId;
            Assert.Contains(':', child);
            state = preparing.ExportState();
            state = state with
            {
                Inhabitants = state.Inhabitants.Select(person => person.InhabitantId == child ? person with
                {
                    HungerBasisPoints = 9_000,
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
