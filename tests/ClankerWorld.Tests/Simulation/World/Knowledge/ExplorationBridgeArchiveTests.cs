using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;
using Xunit.Abstractions;

namespace ClankerWorld.Tests;

public sealed class ExplorationBridgeArchiveTests(ITestOutputHelper output)
{
    private const string WorkshopId = "archived-scout-road-workshop";
    private static readonly GridPoint Start = new(0, 101);
    private static readonly GridPoint FirstWater = new(0, 102);
    private static readonly GridPoint SecondWater = new(0, 103);
    private static readonly GridPoint WorkshopSite = new(3, 102);

    [Fact]
    public async Task ABridgePredatingTheOutingCannotExcuseCrossAxisArchivedEdges()
    {
        using var world = await BuildBridgeAfterActualScoutingAndNaturalDeath();
        var state = world.ExportState();
        var deceased = Assert.Single(state.DeceasedAgents!);
        var exploration = Assert.IsType<ExplorationState>(deceased.LastPhysical.Exploration);
        var bridge = Assert.Single(state.Bridges!, item => item.Id == "bridge-0-102-ew-2");
        Assert.True(exploration.LastOutingTick > 0);
        // Unlike the real later bridge, this forged timestamp says that its
        // axis already constrained every step in the recorded outing.
        var forged = state with { Bridges = [bridge with { BuiltTick = exploration.LastOutingTick - 1 }] };
        var error = Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(forged));
        Assert.Contains("local exploration record", error.Message, StringComparison.Ordinal);
        error = Assert.Throws<InvalidDataException>(() => Restore(forged));
        Assert.Contains("local exploration record", error.Message, StringComparison.Ordinal);
    }

    private async Task<WorldRuntime> BuildBridgeAfterActualScoutingAndNaturalDeath()
    {
        using var scouting = ExplorationBridgeSaveTests.CreateScoutingWorld();
        var actor = scouting.Agents[0].AgentId;
        for (var tick = 0; tick < 400 && Scout(scouting, actor).Exploration!.OutingPath.Count < 3; tick++)
        {
            var previous = Scout(scouting, actor).Position;
            Assert.True((await scouting.AdvanceOneTickAsync()).Advanced);
            var current = Scout(scouting, actor).Position;
            if (previous != current) Assert.True(scouting.ExportState().Map.CanFootStep(previous, current));
        }
        var walking = scouting.ExportState();
        var exploration = Assert.IsType<ExplorationState>(Scout(scouting, actor).Exploration);
        Assert.Equal([Start, FirstWater, SecondWater], exploration.OutingPath);
        Assert.True(walking.Map.CanFootStep(Start, FirstWater));
        Assert.True(walking.Map.CanFootStep(FirstWater, SecondWater));
        Assert.Contains(walking.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == FirstWater);
        var aged = AtNaturalLifeBoundary(walking, actor);
        var world = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(aged)));
        try
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var dead = world.Society.GetAgent(actor);
            Assert.Equal(SocietyAgentStatus.Dead, dead.Status);
            Assert.Equal(SocietyDeathCause.NaturalAge, dead.DeathCause);
            var deceased = Assert.Single(world.ExportState().DeceasedAgents!);
            Assert.Equal(actor, deceased.AgentId);
            Assert.Equal(dead.DeathTick, deceased.DeathTick);
            Assert.Equal(SecondWater, deceased.LastPhysical.Position);
            var archivedPath = Assert.IsType<ExplorationState>(deceased.LastPhysical.Exploration);
            Assert.Equal(exploration.OutingPath, archivedPath.OutingPath);
            Assert.Equal(exploration.VisitedTiles, archivedPath.VisitedTiles);
            Assert.Equal(exploration.OutingDiscoveries, archivedPath.OutingDiscoveries);
            Assert.Equal(exploration.LastOutingTick, archivedPath.LastOutingTick);
            Assert.Equal(exploration.Returning, archivedPath.Returning);
            Assert.DoesNotContain(world.Agents, person => person.AgentId == actor);
            var archivedBytes = WorldRuntimeCodec.Encode(world.ExportState());
            using (var validArchive = Restore(WorldRuntimeCodec.Decode(archivedBytes)))
                Assert.Equal(archivedBytes, WorldRuntimeCodec.Encode(validArchive.ExportState()));

            // A later committed world tick makes this unambiguously post-death.
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            var before = world.ExportState();
            var woodBefore = before.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity);
            var workshop = world.WorldContent.Buildings.Single(item => item.LocalId == "workshop");
            Assert.Equal(10, Assert.Single(workshop.BuildCosts).Amount);
            var placement = world.PlaceBuilding(WorkshopId, workshop.CanonicalId, WorkshopSite);
            Assert.True(placement.Applied, placement.Failure);
            var bridge = Assert.Single(world.Bridges, item => item.Id == "bridge-0-102-ew-2");
            Assert.Equal("bridge-0-102-ew-2", bridge.Id);
            Assert.Equal(BridgeTriggers.Road, bridge.Trigger);
            Assert.True(bridge.BuiltTick > deceased.DeathTick);
            Assert.True(bridge.BuiltTick > exploration.LastOutingTick);
            var after = world.ExportState();
            Assert.Equal(woodBefore - 10, after.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
            Assert.Equal(JsonSerializer.Serialize(deceased), JsonSerializer.Serialize(Assert.Single(after.DeceasedAgents!)));
            Assert.Equal(before.Knowledge!.Facts, after.Knowledge!.Facts);
            Assert.False(after.Map.CanFootStep(Start, FirstWater));
            Assert.False(after.Map.CanFootStep(FirstWater, SecondWater));
            output.WriteLine($"Real scout started {exploration.LastOutingTick}, walked {walking.Society.Society.WorldTick}, natural death {deceased.DeathTick}, paid bridge {bridge.BuiltTick}; exact wood10 consumed; archived history unchanged.");
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }

    private static WorldRuntimeState AtNaturalLifeBoundary(WorldRuntimeState state, string actor)
    {
        var society = state.Society.Society;
        var nextLifeTick = society.Config.TicksPerLifecycleAge - 1;
        var delta = nextLifeTick - society.LifeTickAt(society.WorldTick);
        var days = society.Config.DayLifecycle!.MaximumDay - 1;
        return state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    LifeClock = new SocietyLifeClock(society.LifeClock?.Rate ?? 1, society.WorldTick, nextLifeTick),
                    Agents = society.Agents.Select(person => person.Id == actor ? person with
                    {
                        BirthLifeTick = nextLifeTick + 1 - (days + 1) * society.Config.TicksPerLifecycleAge,
                        AgeBand = SocietyAgeBand.Elder,
                        LastLifecycleYearChecked = days,
                    } : person with { BirthLifeTick = (person.BirthLifeTick ?? person.BirthTick) + delta }).ToArray(),
                },
            },
        };
    }

    private static AgentState Scout(WorldRuntime world, string actor) =>
        world.Agents.Single(person => person.AgentId == actor);

    private static WorldRuntime Restore(WorldRuntimeState state) => WorldRuntime.Restore(state, Provider);
    private static IDecisionProvider Provider(string actor) => new IdleProvider();

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var choice = request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
