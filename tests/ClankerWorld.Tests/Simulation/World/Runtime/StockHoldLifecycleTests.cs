using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class StockHoldLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowerAndDeliveryPromisesSaveStrictlyAndARefusedTickKeepsThem(bool delivering)
    {
        using var generated = NormalPathWorld.CreateGenerated("stock-hold-custody", _ => new GoodsQueryTests.Idle());
        var state = generated.ExportState();
        var society = state.Society.Society;
        var actor = society.Agents.First(person => person.HouseholdId is not null).Id;
        var owner = society.GetAgent(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == owner &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        var jug = new string('j', 128);
        var water = new string('w', 128);
        var inventory = InventoryAuthority.AddLot(society.Inventory, jug, "water_jug", owner, 1, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, water, "fresh_water", owner, 1,
            storageBuildingId: house.InstanceId, containerLotId: jug);
        inventory = delivering ? InventoryAuthority.Transfer(inventory, "pickup", owner, actor, jug, 1, "household_stock_picked_up",
            destinationDeliveryBuildingId: house.InstanceId) : InventoryAuthority.Relocate(inventory, "borrow", jug, owner, 1, carrierId: actor);
        var expected = new[] { delivering ? new InventoryHold(InventoryHoldPurpose.Delivery, house.InstanceId) :
            new InventoryHold(InventoryHoldPurpose.Borrowed, actor) };
        var stockOwner = delivering ? actor : owner;
        Assert.Equal(expected, inventory.GetLot(jug).Holds);
        Assert.Equal(expected, inventory.GetLot(water).Holds);
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Reserve(inventory, "drink", stockOwner, water, 1, "drink", 100));
        Assert.Throws<InvalidOperationException>(() => InventoryAuthority.Relocate(inventory, "steal", jug, stockOwner, 1));
        state = state with { Society = state.Society with { Society = society with { Inventory = inventory } } };
        var bytes = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new GoodsQueryTests.Idle());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        var access = InventoryHoldRules.DeliveryFor(actor, house.InstanceId);
        var arrived = InventoryAuthority.Relocate(inventory, "arrive", jug, stockOwner, 1,
            storageBuildingId: house.InstanceId, holdAccess: access);
        var replay = InventoryAuthority.Relocate(world.Society.Inventory, "arrive", jug, stockOwner, 1,
            storageBuildingId: house.InstanceId, holdAccess: access);
        Assert.Equal(InventoryCheckpointCodec.Encode(arrived), InventoryCheckpointCodec.Encode(replay));
        Assert.Null(arrived.GetLot(jug).Holds);
        Assert.Null(arrived.GetLot(water).Holds);
        var missing = state with
        {
            Society = state.Society with
            {
                Society = society with
                {
                    Inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == jug ? lot with { Holds = null } : lot).ToArray() }
                }
            }
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntimeCodec.Encode(missing));
    }
}
