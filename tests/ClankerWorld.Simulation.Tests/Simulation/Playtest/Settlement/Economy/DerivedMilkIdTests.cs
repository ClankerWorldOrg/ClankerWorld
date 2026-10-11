using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class BusinessTradeTests
{
    [Fact]
    public void FiftyPaidMilkExchangesKeepSavedLotIdsWithinTheRecallBoundary()
    {
        var (state, buyer, seller, shop) = CreateShopState("store");
        var people = new[] { seller, buyer };
        var jugs = new[] { "bounded-seller-jug", "bounded-buyer-jug" };
        var paymentIds = new[] { "bounded-seller-payment", "buyer-payment" };
        var inventory = state.Society.Society.Inventory;
        inventory = inventory with
        {
            Lots = inventory.Lots.Select(lot => lot.Id == "buyer-payment"
                ? lot with { Quantity = 50 } : lot).ToArray(),
        };
        inventory = InventoryFixture.AddLot(inventory, paymentIds[0], "wood", seller, 50);
        inventory = InventoryFixture.AddLot(inventory, jugs[0], "water_jug", seller, 1);
        inventory = InventoryFixture.AddLot(inventory, jugs[1], "water_jug", buyer, 1);
        inventory = InventoryFixture.AddLot(inventory, "bounded-milk", "milk", seller, 1, containerLotId: jugs[0]);
        var position = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == shop).Position;

        // Controlled stock; all fifty identities and payments use the real
        // inventory exchange, with its exact exclusive reservation and vessels.
        for (var round = 0; round < 50; round++)
        {
            var source = round % 2;
            var destination = 1 - source;
            var milk = Assert.Single(inventory.Lots, lot => lot.ItemKind == "milk");
            Assert.Equal((people[source], jugs[source], 1), (milk.OwnerId, milk.ContainerLotId, milk.Quantity));
            var held = "bounded-milk-held-" + round;
            inventory = InventoryFixture.Reserve(inventory, held, people[source], milk.Id, 1,
                "milk-sale:bounded-" + round, long.MaxValue);
            var beforePayment = inventory.GetLot(paymentIds[destination]).Quantity;
            inventory = InventoryFixture.ExchangeMilk(inventory, "bounded-resale-" + round, held,
                jugs[destination], people[destination], paymentIds[destination], new(position.X, position.Y));
            var transferred = Assert.Single(inventory.Lots, lot => lot.ItemKind == "milk");
            Assert.Equal((people[destination], jugs[destination], milk.Id, 1),
                (transferred.OwnerId, transferred.ContainerLotId, transferred.ProvenanceLotId, transferred.Quantity));
            Assert.Equal(beforePayment - 1, inventory.GetLot(paymentIds[destination]).Quantity);
            Assert.Equal(InventoryReservationState.Released, inventory.GetReservation(held).State);
            Assert.Equal(people[0], inventory.GetLot(jugs[0]).OwnerId);
            Assert.Equal(people[1], inventory.GetLot(jugs[1]).OwnerId);
        }

        var bytes = PrivateWorldRuntimeCodec.Encode(WithInventory(state, inventory));
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(bytes), _ => new ShopProvider("safe_idle"));
        Assert.Equal(bytes, PrivateWorldRuntimeCodec.Encode(restored.ExportState()));
        var finalMilk = Assert.Single(restored.Society.Inventory.Lots, lot => lot.ItemKind == "milk");
        Assert.InRange(finalMilk.Id.Length, 1, DerivedId.MaximumLength);
    }
}
