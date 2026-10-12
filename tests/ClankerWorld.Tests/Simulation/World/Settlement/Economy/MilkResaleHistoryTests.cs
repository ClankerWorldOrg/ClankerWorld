using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class BusinessTradeTests
{
    [Fact]
    public void OrdinaryMilkKeepsItsIdentityThroughTwentyBartersAndNativeReloads()
    {
        using var initial = NormalPathWorld.CreateGenerated("ordinary-milk-resale", _ => new ShopProvider());
        var state = initial.ExportState();
        var parties = state.Agents.Take(2).Select(person => person.AgentId).ToArray();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
            "ordinary-resale-milk", "milk", parties[0], 1);
        inventory = InventoryAuthority.AddLot(inventory, "ordinary-resale-payment", "stone", parties[1], 1);
        for (var sale = 0; sale < 20; sale++)
        {
            var seller = parties[sale % 2];
            var buyer = parties[1 - sale % 2];
            var offer = "ordinary-milk-sale-" + sale;
            inventory = InventoryAuthority.CreateDirectBarterOffer(inventory, new(offer, 1,
                seller, buyer, "ordinary-resale-milk", 1, "ordinary-resale-payment", 1, inventory.WorldTick + 120));
            inventory = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer, 1, seller);
            Assert.Equal(DirectBarterState.Open, inventory.GetOffer(offer).State);
            inventory = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer, 1, buyer);
            Assert.Equal(DirectBarterState.Settled, inventory.GetOffer(offer).State);
            var milk = Assert.Single(inventory.Lots, lot => lot.ItemKind == "milk");
            Assert.Equal(("ordinary-resale-milk", buyer, 1), (milk.Id, milk.OwnerId, milk.Quantity));
            Assert.Null(milk.ContainerLotId);
            Assert.Null(milk.ProvenanceLotId);
            Assert.Equal(seller, inventory.GetLot("ordinary-resale-payment").OwnerId);
            state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
            using var world = WorldRuntime.Restore(state);
            world.Validate();
            var bytes = WorldRuntimeCodec.Encode(world.ExportState());
            using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(reload.ExportState()));
            state = reload.ExportState();
            inventory = state.Society.Society.Inventory;
        }
        Assert.Equal(20, inventory.Offers.Count(offer => offer.State == DirectBarterState.Settled));
    }
}
