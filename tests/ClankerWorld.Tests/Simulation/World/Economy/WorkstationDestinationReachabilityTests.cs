using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorkstationDestinationReachabilityTests
{
    [Theory]
    [InlineData("blocked")]
    [InlineData("stocked")]
    [InlineData("opened")]
    public async Task NativeWorkstationSupplySkipsAnOccupiedShopAndDeliversToTheReachableHouseAcrossReplay(string mode)
    {
        var (state, shopId) = TailorTestWorld.Create("workstation-destination-audit", 0);
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == shopId);
        var household = shop.HouseholdId!;
        var house = state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var start = new GridPoint(119, 49);
        Assert.Equal(new GridPoint(125, 55), shop.Position);
        Assert.Equal(new GridPoint(125, 56), house.Position);
        var ring = state.Map.FootNeighbors(shop.Position).Where(state.Map.IsPassable)
            .OrderBy(point => point.X == shop.Position.X + 1 && point.Y == shop.Position.Y ? 0 : 1)
            .ThenBy(point => point.Y).ThenBy(point => point.X).ToArray();
        Assert.Equal(8, ring.Length);
        var others = state.Agents.Where(person => person.AgentId != actor).ToArray();
        var away = state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, shop.Position) > 4 && point != start);
        var day = state.WorldSystems!.Config.TicksPerDay;
        var until = state.Society.Society.WorldTick + day;
        var animals = ring.Skip(3).Select((point, index) => new AnimalState("workshop-blocker:" + index, "Blocker" + index,
            "horse", "male", -7L * day, point, "workshop-blockers", CareUntilTick: until,
            WildFedUntilTick: until, WildWaterUntilTick: until)).ToArray();
        var inventory = state.Society.Society.Inventory;
        var removed = inventory.Lots.Where(lot => lot.OwnerId == actor || lot.CarrierId == actor)
            .Select(lot => lot.Id).ToHashSet(StringComparer.Ordinal);
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => !removed.Contains(lot.Id) &&
            (lot.ContainerLotId is null || !removed.Contains(lot.ContainerLotId))).ToArray()
        };
        inventory = InventoryAuthority.AddLot(inventory, "destination-fiber", "fiber", actor, 3);
        inventory = InventoryAuthority.AddLot(inventory, "destination-wood", "wood", actor, 3);
        inventory = InventoryAuthority.AddLot(inventory, "destination-axe", "wooden_axe", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "destination-pick", "wooden_pickaxe", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "tailor-fuel", "wood", household, 2, storageBuildingId: shopId);
        if (mode == "stocked")
            inventory = InventoryAuthority.AddLot(inventory, "tailor-stocked-fiber", "fiber", household, 6, storageBuildingId: shopId);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? start : mode == "opened" && person.AgentId == others[0].AgentId
                    ? away : ring[Array.FindIndex(others, other => other.AgentId == person.AgentId)],
                FullnessBasisPoints = 9_500,
                Survival = new(),
                Project = null,
                LastDecisionContext = null,
                Equipment = person.AgentId == actor ? null : person.Equipment,
                TravelCooldownTicks = 0,
                MoveWaitTicks = 0,
            }).ToArray(),
            AnimalWorld = new(true, animals, []),
        };
        state = WeatherTestFixture.WithWeather(state, WeatherKind.Clear);
        var choices = new FoodCapacityTestFixture.Choices("supply_workstation:", "haul_household_stock");
        using var world = FoodCapacityTestFixture.Restore(state, actor, choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = FoodCapacityTestFixture.Restore(WorldRuntimeCodec.Decode(bytes), actor,
            new FoodCapacityTestFixture.Choices("supply_workstation:", "haul_household_stock"));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        int StoredWood() => world.Society.Inventory.Lots.Where(lot =>
            (lot.Id == "destination-wood" || lot.ProvenanceLotId == "destination-wood") &&
            lot.StorageBuildingId == house.InstanceId && lot.OwnerId == household).Sum(lot => lot.Quantity);
        var visitedHouse = false;
        for (var tick = 0; tick < 80 && StoredWood() != 3; tick++)
        {
            var before = world.Agents.Single(person => person.AgentId == actor).Position;
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            var after = world.Agents.Single(person => person.AgentId == actor).Position;
            visitedHouse |= after == house.Position;
            if (before != after) Assert.True(state.Map.CanFootStep(before, after));
            if (mode == "blocked" && !visitedHouse)
                Assert.DoesNotContain(world.Society.Inventory.Lots, lot =>
                    (lot.Id == "destination-fiber" || lot.ProvenanceLotId == "destination-fiber") && lot.StorageBuildingId == shopId);
            Assert.InRange(PersonalEquipmentRules.CarriedQuantity(world.Society.Inventory, actor, null), 0, 8);
            Assert.All(world.Animals, animal => Assert.Equal(animals.Single(item => item.Id == animal.Id).Position, animal.Position));
            world.Validate();
        }
        var lots = world.Society.Inventory.Lots;
        Assert.Equal(3, lots.Where(lot => (lot.Id == "destination-wood" || lot.ProvenanceLotId == "destination-wood") &&
            lot.StorageBuildingId == house.InstanceId && lot.OwnerId == household).Sum(lot => lot.Quantity));
        Assert.Equal(3, lots.Where(lot => lot.Id == "destination-wood" || lot.ProvenanceLotId == "destination-wood").Sum(lot => lot.Quantity));
        Assert.Equal(3, lots.Where(lot => lot.Id == "destination-fiber" || lot.ProvenanceLotId == "destination-fiber").Sum(lot => lot.Quantity));
        if (mode != "blocked")
            Assert.Equal(mode == "opened" ? 3 : 0, lots.Where(lot => (lot.Id == "destination-fiber" || lot.ProvenanceLotId == "destination-fiber") &&
                lot.StorageBuildingId == shopId).Sum(lot => lot.Quantity));
        else
        {
            Assert.Contains("supply_workstation:wood", choices.Offers[0]);
            Assert.DoesNotContain("supply_workstation:fiber", choices.Offers[0]);
        }
        Assert.Contains(choices.Offers, offered => offered.Contains("supply_workstation:wood", StringComparer.Ordinal));
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var loaded = FoodCapacityTestFixture.Restore(WorldRuntimeCodec.Decode(final), actor,
            new FoodCapacityTestFixture.Choices("supply_workstation:", "haul_household_stock"));
        Assert.Equal(final, WorldRuntimeCodec.Encode(loaded.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await loaded.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(loaded.ExportState()));
    }
}
