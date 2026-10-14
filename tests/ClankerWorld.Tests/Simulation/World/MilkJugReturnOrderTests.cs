using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(false, "available")]
    [InlineData(true, "available")]
    [InlineData(true, "reserved")]
    [InlineData(true, "delivery")]
    public async Task CollectedMilkCanReturnToItsHouseDespiteAnAvailableStoreButProtectsCommittedCargo(bool hasStore, string boundary)
    {
        var (state, actor, home, yard) = CreateYard("milk-jug-return-audit");
        if (hasStore)
        {
            var definition = state.WorldContent!.Buildings.Single(item => item.LocalId == "store-1x1");
            var costs = state.Society.Society.Inventory;
            foreach (var cost in definition.BuildCosts)
                costs = InventoryAuthority.AddLot(costs, "return-store-cost-" + cost.ResourceId, cost.ResourceId,
                    home, cost.Amount, groundPosition: new(yard.Position.X, yard.Position.Y));
            using var building = WorldRuntime.Restore(At(state, actor, yard.Position, costs, []), _ => new AnimalChooser());
            foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, yard.Position)))
                if (building.PlaceBuilding("return-store", definition.CanonicalId, tile.Position, home).Applied) break;
            Assert.Single(building.WorldSimulation.Buildings, item => item.InstanceId == "return-store");
            state = building.ExportState();
        }
        var day = state.WorldSystems!.Config.TicksPerDay;
        // Bounded product clock, not a proof of the full daily care cycle. Collection remains native.
        var cow = new AnimalState("return-cow", "Moss", "cow", "female", -(long)AnimalRules.Definition("cow").AdultDays * day,
            yard.Position, "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day,
            ProductProgressTicks: AnimalRules.Definition("cow").ProductDays * day - 1);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "return-jug", "water_jug", home, 1,
            storageBuildingId: yard.InstanceId);
        using var collecting = WorldRuntime.Restore(At(state, actor, yard.Position, inventory, [cow]),
            id => id == actor ? new AnimalChooser("animal_order") : new AnimalChooser());
        Assert.True((await collecting.AdvanceOneTickAsync()).Advanced);
        var milkId = Assert.Single(collecting.Animals).ReadyProductLotId!;
        Assert.NotNull(milkId);
        collecting.SubmitInstruction(new("collect-return-milk", "owner", actor, OwnerInstructionKind.Order, "collect from Moss"));
        await Until(collecting, () => collecting.Animals.Single().ReadyProductLotId is null);
        inventory = collecting.Society.Inventory;
        Assert.Equal((home, actor, 2, (string?)null), (inventory.GetLot(milkId).OwnerId,
            inventory.GetLot(milkId).CarrierId, inventory.GetLot(milkId).Quantity, inventory.GetLot(milkId).ContainerLotId));
        var house = collecting.WorldSimulation.Buildings.Single(item => item.HouseholdId == home &&
            collecting.WorldContent.Buildings.Single(definition => definition.CanonicalId == item.DefinitionId).Tags.Contains("house"));
        if (boundary == "reserved")
            inventory = InventoryAuthority.Reserve(inventory, "return-held-milk", home, milkId, 2, "other_work", collecting.WorldTick + day, holdAccess: StockHoldTestState.Access(inventory, milkId));
        if (boundary == "delivery")
            inventory = InventoryAuthority.Transfer(inventory, "return-milk-promise", home, actor, milkId, 2,
                "delivery", destinationDeliveryBuildingId: "return-store", holdAccess: StockHoldTestState.Access(inventory, milkId));
        state = collecting.ExportState() with
        { Society = collecting.ExportState().Society with { Society = collecting.Society with { Inventory = StockHoldTestState.Custody(inventory) } } };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == actor ? new AnimalChooser("return_borrowed") : new AnimalChooser());
        world.SubmitInstruction(new("return-collected-milk", "owner", actor, OwnerInstructionKind.Order, "return two borrowed milk"));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            id => id == actor ? new AnimalChooser("return_borrowed") : new AnimalChooser());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 60; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            var order = world.ExportState().Instructions!.Single(item => item.IdempotencyKey == "return-collected-milk").Task!;
            if (order.Status == "finished" || boundary != "available" && tick >= 3) break;
        }
        var result = world.ExportState().Instructions!.Single(item => item.IdempotencyKey == "return-collected-milk").Task!;
        var jug = world.Society.Inventory.GetLot("return-jug");
        var milk = world.Society.Inventory.GetLot(milkId);
        var owner = boundary == "delivery" ? actor : home;
        Assert.Equal((home, 1, yard.InstanceId, (string?)null), (jug.OwnerId, jug.Quantity, jug.StorageBuildingId, jug.CarrierId));
        Assert.Equal((owner, 2, (string?)null), (milk.OwnerId, milk.Quantity, milk.ContainerLotId));
        Assert.True(milk.FreshnessBasisPoints > 0);
        if (boundary == "available")
        {
            Assert.Equal(("finished", 2), (result.Status, result.CompletedUnits));
            Assert.Equal((house.InstanceId, (string?)null), (milk.StorageBuildingId, milk.CarrierId));
        }
        else
        {
            Assert.Equal(("blocked", 0), (result.Status, result.CompletedUnits));
            Assert.Equal((inventory.GetLot(milkId).CarrierId, (string?)null), (milk.CarrierId, milk.StorageBuildingId));
            Assert.True(PersonalEquipmentRules.IsPhysicallyCarried(world.Society.Inventory, milk, actor));
            if (boundary == "reserved") Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation("return-held-milk").State);
            else Assert.Equal("return-store", milk.DeliveryBuildingId);
        }
        var final = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(final), _ => new AnimalChooser());
        Assert.Equal(final, WorldRuntimeCodec.Encode(reload.ExportState()));
        world.Validate();
    }
}
