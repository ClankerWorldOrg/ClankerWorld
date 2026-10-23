using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class ReachableHouseHaulingTests
{
    [Fact]
    public async Task AnOccupiedPileDoesNotHideReachableHouseholdSupplies()
    {
        using var setup = NormalPathWorld.CreateGenerated("house-haul-route-audit", _ => new HaulChooser());
        for (var tick = 0; tick < 8; tick++) Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        const string home = "household:first";
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == home && person.AgeBand == SocietyAgeBand.Adult).Id;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        var buildingTiles = state.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building)).ToHashSet();
        var piles = state.Map.FootNeighbors(house.Position).Where(point => state.Map.IsPassable(point) &&
            !buildingTiles.Contains(point) && point != house.Entrance).Take(2).ToArray();
        var blocked = piles[0];
        var reachable = piles[1];
        Assert.True(state.Map.IsPassable(blocked));
        Assert.True(state.Map.IsPassable(reachable));
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => lot.OwnerId != actor &&
            (lot.OwnerId != home || lot.StorageBuildingId is not null)).ToArray()
        };
        inventory = InventoryAuthority.AddLot(inventory, "a-blocked-haul-stock", "wood", home, 4,
            groundPosition: new(blocked.X, blocked.Y));
        inventory = InventoryAuthority.AddLot(inventory, "b-reachable-haul-stock", "wood", home, 4,
            groundPosition: new(reachable.X, reachable.Y));
        var others = state.Agents.Where(person => person.AgentId != actor).ToArray();
        var positions = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, house.Position) > 3 && !buildingTiles.Contains(point))
            .Take(others.Length - 1).Prepend(blocked).ToArray();
        var occupied = positions.ToHashSet();
        using (var routes = MovementTestWorld.Routes(state, house.Position, occupied))
        {
            Assert.Empty(routes.RouteTo(blocked, 0));
            Assert.True(routes.RouteTo(reachable, 0).Count > 0);
        }
        using (var routes = MovementTestWorld.Routes(state, reachable, occupied))
            Assert.True(routes.RouteTo(house.Position, 0).Count > 0);
        var assignments = others.Select((person, index) => (person.AgentId, Position: positions[index]))
            .ToDictionary(item => item.AgentId, item => item.Position, StringComparer.Ordinal);
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? house.Position : assignments[person.AgentId],
                FullnessBasisPoints = 10_000,
                LastDecisionContext = null,
                Project = null,
                Equipment = person.AgentId == actor ? null : person.Equipment
            }).ToArray()
        };
        var wood = inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        var chooser = new HaulChooser(true);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id => id == actor ? chooser : new HaulChooser());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id => new HaulChooser(id == actor));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 30 && world.Society.Inventory.GetLot("b-reachable-haul-stock").StorageBuildingId != house.InstanceId; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(chooser.Offered);
        var delivered = world.Society.Inventory.GetLot("b-reachable-haul-stock");
        Assert.Equal((home, house.InstanceId, 4), (delivered.OwnerId, delivered.StorageBuildingId, delivered.Quantity));
        Assert.Null(delivered.CarrierId);
        Assert.Null(delivered.GroundPosition);
        var untouched = world.Society.Inventory.GetLot("a-blocked-haul-stock");
        Assert.Equal((home, 4, new InventoryGroundPosition(blocked.X, blocked.Y)), (untouched.OwnerId, untouched.Quantity, untouched.GroundPosition));
        Assert.Null(untouched.CarrierId);
        Assert.Equal(wood, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "wood").Sum(lot => lot.Quantity));
        Assert.All(assignments, item => Assert.Equal(item.Value, world.Agents.Single(person => person.AgentId == item.Key).Position));
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), id => new HaulChooser(id == actor));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Validate();
        reload.Validate();
    }

    private sealed class HaulChooser(bool haul = false) : IDecisionProvider
    {
        public bool Offered { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Offered |= request.Observation.Candidates.Any(candidate => candidate.Id == "haul_household_stock");
            var choice = request.Observation.Candidates.FirstOrDefault(candidate => haul && candidate.Id == "haul_household_stock") ??
                request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return new DeterministicDecisionProvider().DecideAsync(request with
            { Observation = request.Observation with { Candidates = [choice] } }, cancellationToken);
        }
    }
}
