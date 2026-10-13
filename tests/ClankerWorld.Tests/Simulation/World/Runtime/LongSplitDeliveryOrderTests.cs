using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class WorldDeliveryOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrderedPartialDeliveryTracksTheActualBoundedLotAcrossStrictReload(bool container)
    {
        var state = Prepared();
        var actor = Actor(state);
        var source = SourceNear(state, Home(state, House).Position);
        state = At(state, actor, source);
        var longLot = DerivedId.Create(new string('g', DerivedId.MaximumLength));
        var inventory = state.Society.Society.Inventory;
        if (container)
            inventory = InventoryAuthority.AddLot(inventory, "long-grain-pot", "storage_pot", Alpha, 1,
                groundPosition: new(source.X, source.Y));
        inventory = InventoryAuthority.AddLot(inventory, longLot, "grain", Alpha, 8,
            containerLotId: container ? "long-grain-pot" : null,
            groundPosition: container ? null : new(source.X, source.Y));
        using var world = Restore(WithInventory(state, inventory));
        var receipt = Submit(world, actor, "long-split-delivery", "haul two grain to my Farmhouse");
        await Tick(world);
        var order = Order(world, receipt);
        Assert.NotNull(order.DeliveryLotId);
        Assert.Contains(world.Society.Inventory.Lots, lot => lot.Id == order.DeliveryLotId && lot.Quantity == 2);
        Assert.InRange(order.DeliveryLotId.Length, 1, DerivedId.MaximumLength);
        using var replay = Reload(world);
        await FinishTogether(world, replay, receipt);
        Assert.Equal(2, Stored(world, Farmhouse, "grain"));
        Assert.Equal(2, Order(world, receipt).CompletedUnits);
        Assert.Equal(6, world.Society.Inventory.GetLot(longLot).Quantity);
    }
}
