using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class StoredFuelRoutingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HouseholdUsesLocalFuelWhenStoredWoodCannotBeReached(bool blockStorage)
    {
        var options = new GeographyOptions("island-fuel-review-0", WorldSizePreset.Small);
        using var initial = new WorldRuntime(options.Seed, startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        initial.InitializeFirstTownContent();
        initial.AcceptFirstTownLayout(new GridPoint(136, 14));
        var setupMap = initial.ExportState().Map;
        var storagePosition = initial.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;
        var localTree = setupMap.Resources.Single(resource => resource.Id == "wild-128-16").Position;
        var actorPosition = setupMap.Tiles.First(tile => setupMap.IsBuildable(tile.Position) &&
            setupMap.FootDistance(tile.Position, storagePosition) > 1 && setupMap.FootDistance(tile.Position, localTree) <= 1 &&
            !setupMap.Resources.Any(resource => resource.Position == tile.Position)).Position;
        var positions = new[] { actorPosition, new GridPoint(131, 9), new GridPoint(132, 9), new GridPoint(133, 9) };
        var ids = Enumerable.Range(1, 4).Select(index => $"founder:{index:D32}").ToArray();
        for (var index = 0; index < 4; index++) initial.PlaceFounder(ids[index], positions[index]);
        initial.StartWorld();
        var state = blockStorage ? StorageRoutingTestFixture.BlockAccess(initial, storagePosition, 1) : initial.ExportState();
        var household = state.Society.Society.GetAgent(ids[0]).HouseholdId!;
        var society = state.Society.Society;
        var storedWood = society.Inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "wood")
            .Sum(lot => lot.Quantity);
        Assert.True(storedWood > 0);
        state = state with
        {
            Society = state.Society with { Society = society },
            Survival = new SurvivalState(0, []),
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 9_000,
                Survival = new SurvivalCondition(WarmthBasisPoints: 4_000),
            }).ToArray(),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Snow);
        var local = state.Map.Resources.Single(resource => resource.Id == "wild-128-16");
        Assert.True(state.Map.IsReachableOnFoot(positions[0], local.Position));
        Assert.False(state.Map.IsReachableFromStartingSiteOnFoot(local.Position));
        var provider = new HeatProvider(ids[0]);
        var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), _ => provider);
        try
        {
            for (var tick = 0; tick < 24; tick++)
            {
                Assert.True((await world.AdvanceOneTickAsync()).Advanced);
                if (tick == 5)
                {
                    var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
                    world.Dispose();
                    world = WorldRuntime.Restore(saved, _ => provider);
                }
            }
            if (!world.ExportState().Survival!.Fires.Any(fire => fire.BuildingId == "first-town-house-a"))
            {
                // A changed layout can leave only the final hearth approach after
                // the original budget. Permit a short, physically bounded tail.
                var arrival = world.ExportState();
                var actor = arrival.Agents.Single(person => person.AgentId == ids[0]).Position;
                var hearth = arrival.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a").Position;
                var distance = arrival.Map.FootDistance(actor, hearth);
                Assert.InRange(distance, 0, 4);
                Assert.True(arrival.Map.IsReachableOnFoot(actor, hearth));
                for (var tick = 0; tick < distance + 3 &&
                    !world.ExportState().Survival!.Fires.Any(fire => fire.BuildingId == "first-town-house-a"); tick++)
                    Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            }
            var result = world.ExportState();
            Assert.Equal(!blockStorage, provider.WasToolOffered);
            if (blockStorage)
            {
                Assert.True(result.Events.Any(item => item.Kind == "material_gathered" && item.Detail.StartsWith(ids[0] + ":", StringComparison.Ordinal)),
                    $"tick={world.WorldTick}; fires={result.Survival!.Fires.Count}; no_route={result.Events.Count(item => item.Kind == "movement_blocked" && item.Detail.StartsWith(ids[0] + ":no_route", StringComparison.Ordinal))}; warmth={result.Agents.Single(person => person.AgentId == ids[0]).Survival!.WarmthBasisPoints}; stored wood={storedWood}");
                Assert.Equal(storedWood, result.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "wood").Sum(lot => lot.Quantity));
            }
            Assert.Contains(result.Survival!.Fires, fire => fire.BuildingId == "first-town-house-a");
            Assert.DoesNotContain(result.Events, item => item.Kind == "movement_blocked" && item.Detail.StartsWith(ids[0] + ":no_route", StringComparison.Ordinal));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(result)));
            Assert.NotEmpty(restored.ExportState().Survival!.Fires);
        }
        finally { world.Dispose(); }
    }

    private sealed class HeatProvider(string actor) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public bool WasToolOffered { get; private set; }
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == actor &&
                request.Observation.Candidates.Any(candidate => candidate.Id == "collect_wooden_axe"))
                WasToolOffered = true;
            var selected = request.Observation.AgentId == actor
                ? request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "tend_fire") : null;
            selected ??= request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with { Candidates = [selected] },
            }, cancellationToken);
        }
    }
}
