using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Tests;

public sealed partial class WorldDeliveryOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrderedPartialDeliveryTracksTheActualBoundedLotAcrossStrictReload(bool contained)
    {
        var state = Prepared();
        var actor = Actor(state);
        var source = SourceNear(state, Home(state, Farmhouse).Position);
        state = At(state, actor, source);
        var sourceId = DerivedId.Create(new string('g', DerivedId.MaximumLength));
        var potId = DerivedId.Create("delivery-grain-pot");
        var inventory = state.Society.Society.Inventory;
        if (contained)
            inventory = InventoryAuthority.AddLot(inventory, potId, "storage_pot", Alpha, 1,
                groundPosition: new(source.X, source.Y));
        inventory = InventoryAuthority.AddLot(inventory, sourceId, "grain", Alpha, 8,
            groundPosition: contained ? null : new(source.X, source.Y),
            containerLotId: contained ? potId : null);
        using var world = Restore(WithInventory(state, inventory));
        var receipt = Submit(world, actor, "bounded-grain-delivery", "haul two grain to my Farmhouse");

        await Tick(world);

        var shipment = Assert.Single(world.Society.Inventory.Lots,
            lot => lot.ProvenanceLotId == sourceId && lot.OwnerId == actor);
        Assert.StartsWith("derived:sha256:", shipment.Id);
        Assert.Equal(shipment.Id, Order(world, receipt).DeliveryLotId);
        Assert.Equal((0, 2, Farmhouse),
            (Order(world, receipt).CompletedUnits, Order(world, receipt).DeliveryQuantity, shipment.DeliveryBuildingId));
        Assert.Equal(2, shipment.Quantity);
        Assert.Null(shipment.ContainerLotId);
        Assert.Equal(0, Stored(world, Farmhouse, "grain"));
        AssertSourceRemains();

        using var replay = Reload(world);
        await FinishTogether(world, replay, receipt);

        Assert.Equal(("finished", 2), (Order(world, receipt).Status, Order(world, receipt).CompletedUnits));
        Assert.Null(Order(world, receipt).DeliveryLotId);
        var delivered = world.Society.Inventory.GetLot(shipment.Id);
        Assert.Equal((Alpha, Farmhouse, 2, sourceId),
            (delivered.OwnerId, delivered.StorageBuildingId, delivered.Quantity, delivered.ProvenanceLotId));
        Assert.Null(delivered.DeliveryBuildingId);
        Assert.Equal(2, Stored(world, Farmhouse, "grain"));
        AssertSourceRemains();
        using var completedReload = Reload(world);
        await TickTogether(world, completedReload);
        Assert.Equal(2, Order(world, receipt).CompletedUnits);
        Assert.Single(world.ExportState().Events, item => item.Kind == "owner_stock_delivered");

        void AssertSourceRemains()
        {
            var remaining = world.Society.Inventory.GetLot(sourceId);
            Assert.Equal((Alpha, 6, contained ? potId : null),
                (remaining.OwnerId, remaining.Quantity, remaining.ContainerLotId));
            var carrier = contained ? world.Society.Inventory.GetLot(potId) : remaining;
            Assert.Equal(Alpha, carrier.OwnerId);
            Assert.Equal(new InventoryGroundPosition(source.X, source.Y), carrier.GroundPosition);
        }
    }
}
