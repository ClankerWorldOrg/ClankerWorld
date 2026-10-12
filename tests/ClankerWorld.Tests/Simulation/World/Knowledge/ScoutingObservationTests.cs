using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class OrchardObservationTests
{
    [Theory]
    [InlineData("outward")]
    [InlineData("return")]
    [InlineData("first_visit")]
    [InlineData("unchanged")]
    [InlineData("blocked_return")]
    public async Task ScoutingObservesChangedSitesOnActualArrival(string scenario)
    {
        var (state, actor, target, blocked) = await ObserveEmptyTile((initial, site) =>
            initial.Map.FootNeighbors(site).Any(point => initial.Map.IsBuildable(point) && !BlockedTiles(initial).Contains(point) &&
                initial.Map.FootNeighbors(point).OrderBy(next => next.Y).ThenBy(next => next.X).First() == site &&
                initial.Agents.All(person => person.Position != point)));
        var old = Assert.Single(state.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == target);
        var origin = state.Map.FootNeighbors(target).First(point => state.Map.IsBuildable(point) && !blocked.Contains(point) &&
            state.Map.FootNeighbors(point).OrderBy(next => next.Y).ThenBy(next => next.X).First() == target &&
            state.Agents.All(person => person.AgentId == actor || person.Position != point));
        using (var moving = Restore(state, actor))
        {
            await Move(moving, actor, origin, "leave-scout-site");
            state = moving.ExportState();
        }
        AgentKnowledgeArtifact? written = null;
        if (scenario != "first_visit")
        {
            var writer = new WritingProvider(actor, "knowledge_write:field_map");
            using var writing = RestoreWriting(WithPaper(state, actor), writer);
            RequestWriting(writing, actor, "map-before-orchard");
            await Until(writing, () => writing.ExportState().Knowledge!.Artifacts.Count == 1);
            state = writing.ExportState();
            written = Assert.Single(state.Knowledge!.Artifacts);
            Assert.Empty(Assert.Single(written.Facts, fact => fact.Position == target).ResourceKinds);
            Assert.DoesNotContain(state.Society.Society.Inventory.Lots, lot => lot.Id == "orchard-record-paper");
        }
        var planter = state.Agents.First(person => person.AgentId != actor).AgentId;
        var parked = state.Agents.Single(person => person.AgentId == planter).Position;
        if (scenario != "unchanged")
        {
            state = FarmFieldTests.WithInventory(state, InventoryAuthority.Transfer(state.Society.Society.Inventory,
                "give-scout-orchard-seed", actor, planter, "orchard-observation-seed", 1, "give planting seed")) with
            {
                Agents = state.Agents.Select(person => person.AgentId == planter
                    ? person with { Position = target, TravelCooldownTicks = 0 } : person).ToArray(),
            };
            using var planting = Restore(state, actor);
            var planted = planting.PlantTree(planter, TreeGrowthRules.Orchard, "orchard-observation-seed", target);
            Assert.True(planted.Planted, planted.Message);
            state = planting.ExportState();
            Assert.Empty(Assert.Single(state.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == target).ResourceKinds);
        }
        // Controlled valid outing boundary: the old fact above came from actual
        // travel, and another actor paid for the new sapling. No prior scouting
        // of these surrounding tiles or planter's return journey is claimed.
        var visited = state.Map.FootNeighbors(origin).Append(origin)
            .Where(point => scenario != "first_visit" || point != target).ToArray();
        state = state with
        {
            Knowledge = scenario == "first_visit" ? state.Knowledge! with
            {
                Facts = state.Knowledge.Facts.Where(fact => fact.OwnerId != actor || fact.Position != target).ToArray(),
            } : state.Knowledge,
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                TravelCooldownTicks = 0,
                LastDecisionContext = null,
                Exploration = new ExplorationState(visited,
                    scenario is "return" or "blocked_return" ? [target, origin] : [origin],
                    state.Society.Society.WorldTick, scenario is "return" or "blocked_return", []),
            } : person.AgentId == planter ? person with
            {
                Position = scenario == "blocked_return" ? target : parked,
            } : person).ToArray(),
        };
        IDecisionProvider Provider(string id) => new ScoutObservationProvider(id == actor);
        using var world = WorldRuntime.Restore(state, Provider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before), Provider);
        for (var tick = 0; tick < 10 && world.Agents.Single(person => person.AgentId == actor).Position != target; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            if (scenario != "first_visit" && world.Agents.Single(person => person.AgentId == actor).Position != target)
                Assert.Equal(FactKey(old), FactKey(Assert.Single(world.ExportState().Knowledge!.Facts,
                    fact => fact.OwnerId == actor && fact.Position == target)));
        }
        var final = world.ExportState();
        AssertReload(final);
        var observed = Assert.Single(final.Knowledge!.Facts, fact => fact.OwnerId == actor && fact.Position == target);
        var position = final.Agents.Single(person => person.AgentId == actor).Position;
        output.WriteLine($"scenario={scenario}; actual_position={position}; target={target}; resources={string.Join(',', observed.ResourceKinds)}");
        if (scenario == "blocked_return") Assert.NotEqual(target, position);
        else Assert.Equal(target, position);
        if (scenario is "unchanged" or "blocked_return") Assert.Equal(FactKey(old), FactKey(observed));
        else
        {
            Assert.Contains("fruit", observed.ResourceKinds);
            Assert.Equal(actor, observed.DiscovererId);
            Assert.Equal("firsthand", observed.Acquisition);
            Assert.True(observed.LearnedTick > old.LearnedTick);
            Assert.Equal(old.Id, observed.Id);
        }
        if (written is not null)
        {
            Assert.Equal(written.Facts.Select(FactKey), Assert.Single(final.Knowledge.Artifacts).Facts.Select(FactKey));
            if (scenario is "outward" or "return")
                Assert.Equal(FactKey(old), FactKey(Assert.Single(final.Knowledge.EarlierFacts)));
            else Assert.Empty(final.Knowledge.EarlierFacts);
        }
    }

    private sealed class ScoutObservationProvider(bool scout) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var choice = request.Observation.Candidates.FirstOrDefault(item => scout && item.Id == "explore")
                ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [choice] },
            }, cancellationToken);
        }
    }

    /// <summary>The tiles ObserveEmptyTile treats as blocked: buildings, Roads, resources and camp objects.</summary>
    private static HashSet<GridPoint> BlockedTiles(WorldRuntimeState state) => state.WorldSimulation!.Buildings
        .SelectMany(building => WorldContentSimulationRules.Footprint(
            state.WorldContent!.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building))
        .Concat(state.RoadTiles ?? []).Concat(state.Map.Resources.Select(resource => resource.Position))
        .Concat(state.Map.MapObjects.Select(item => item.Position)).ToHashSet();
}
