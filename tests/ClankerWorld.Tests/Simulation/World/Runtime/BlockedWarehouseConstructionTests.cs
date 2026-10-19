using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldConstructionOrderTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ABlockedWarehouseCannotHideReachableConstructionWood(bool townStock, bool openApproach)
    {
        var setup = await Baseline.Value;
        setup = setup with { Site = setup.WarehouseSite, CandidateId = setup.WarehouseCandidateId };
        var state = Decode(setup);
        var actor = setup.Actor;
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var footprint = state.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
            state.WorldContent!.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building)).ToHashSet();
        // One occupant blocks the actual entrance. The remaining residents and
        // animals stay clear so removing that occupant really opens the route.
        var approaches = new[] { warehouse.Entrance!.Value }.Concat(state.Map.Tiles.Select(tile => tile.Position)
            .Where(point => state.Map.IsBuildable(point) && !footprint.Contains(point) &&
                state.Map.FootDistance(point, warehouse.Position) > 3 && state.Map.FootDistance(point, setup.Site) > 3))
            .Take(8).ToArray();
        Assert.Equal(8, approaches.Length);
        Assert.DoesNotContain(setup.Site, approaches);
        // Blocked-door cases need only one harvest's worth missing to prove
        // gathering still works. Keep the full shortage in the open-door control
        // so it still collects all ten communal wood through the actual entrance.
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "construction-drain", Alpha,
            "construction-wood", openApproach ? 10 : 2, "fixture_work", long.MaxValue);
        inventory = InventoryAuthority.ConsumeReservation(inventory, "construction-drain");
        inventory = InventoryAuthority.AddLot(inventory, "construction-gather-axe", "crude_wooden_axe", actor, 1);
        if (townStock) inventory = InventoryAuthority.AddLot(inventory, "blocked-town-wood", "wood", warehouse.TownId!, 10,
            storageBuildingId: warehouse.InstanceId);
        var blockers = state.Agents.Where(person => person.AgentId != actor).ToArray();
        var positions = blockers.Select((person, index) => (person.AgentId, Position: approaches[index]))
            .ToDictionary(item => item.AgentId, item => item.Position, StringComparer.Ordinal);
        if (openApproach) positions[blockers[0].AgentId] = state.Map.FootNeighbors(setup.Site).First(point =>
            point != warehouse.Position && !approaches.Contains(point) && point != setup.Site);
        var day = state.WorldSystems!.Config.TicksPerDay;
        var horses = approaches.Skip(3).Select((point, index) => new AnimalState("warehouse-blocker-" + index, "Ash",
            "horse", "female", -(long)AnimalRules.Definition("horse").AdultDays * day,
            point, "warehouse-blockers", CareUntilTick: state.Society.Society.WorldTick + day,
            WildFedUntilTick: state.Society.Society.WorldTick + day, WildWaterUntilTick: state.Society.Society.WorldTick + day)).ToArray();
        state = WithInventory(state, inventory) with
        {
            AnimalWorld = new(true, horses, []),
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? setup.Site : positions[person.AgentId],
                FullnessBasisPoints = 10_000,
                LastDecisionContext = null,
                Project = null
            }).ToArray()
        };
        var occupied = positions.Values.Concat(horses.Select(animal => animal.Position)).ToHashSet();
        using (var routes = MovementTestWorld.Routes(state, setup.Site, occupied))
            Assert.Equal(openApproach, routes.RouteTo(warehouse.Position, 0).Count > 0);
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = Restore(WorldRuntimeCodec.Decode(bytes));
        var receipt = Submit(world, actor, "reachable-clinic", BuildClinic(setup.Site));
        using var replay = Reload(world);
        bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 100 && Order(world, receipt).Status != "finished"; tick++)
            await TickTogether(world, replay);
        Assert.Equal(("finished", 1), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits));
        var clinic = Assert.Single(world.WorldSimulation.Buildings, building => building.DefinitionId == setup.DefinitionId);
        Assert.Equal((Alpha, setup.Site), (clinic.HouseholdId, clinic.Position));
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "construction-stone");
        var proof = Assert.Single(world.WorldSimulation.ConstructionReceipts!);
        var payment = proof.InputReservationIds.Select(world.Society.Inventory.GetReservation).ToArray();
        Assert.All(payment, input => Assert.Equal((Alpha, InventoryReservationState.Completed), (input.OwnerId, input.State)));
        Assert.Equal(10, payment.Where(input => input.LotId != "construction-stone").Sum(input => input.Quantity));
        Assert.Equal(townStock && !openApproach ? 10 : 0, world.Society.Inventory.Lots
            .Where(lot => lot.OwnerId == warehouse.TownId && lot.ItemKind == "wood" && lot.StorageBuildingId == warehouse.InstanceId)
            .Sum(lot => lot.Quantity));
        var events = world.ExportState().Events;
        Assert.Equal(!townStock || !openApproach, events.Any(item => item.Kind == "material_gathered"));
        Assert.Equal(townStock && openApproach, events.Any(item => item.Kind == "town_resource_collected"));
        Assert.All(positions, item => Assert.Equal(item.Value, Person(world, item.Key).Position));
        Assert.All(horses, animal => Assert.Equal(animal.Position, world.Animals.Single(item => item.Id == animal.Id).Position));
        using var reload = Reload(world);
        reload.Validate();
        world.Validate();
    }
}
