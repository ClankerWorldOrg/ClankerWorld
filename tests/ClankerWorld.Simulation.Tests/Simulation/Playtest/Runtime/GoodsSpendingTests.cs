using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;

namespace ClankerWorld.Simulation.Tests;

public sealed partial class PrivateWorldCollectionOrderTests
{
    [Fact]
    public async Task SpendingExcludesPaidStallStockWhileSellingKeepsItsUnreservedQuantity()
    {
        var state = await PaidMarketWorld.StateAsync();
        const string owner = "household:camp-alpha";
        var actor = state.Society.Society.Inhabitants.First(person => person.HouseholdId == owner).Id;
        var stall = PaidMarketWorld.StallTile(state, 0);
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "spend-stall-stock", "flour", owner, 3,
            groundPosition: new(stall.X, stall.Y));
        inventory = InventoryFixture.Reserve(inventory, "spend-stall-held", owner, "spend-stall-stock", 1, "work", long.MaxValue);
        state = PaidMarketWorld.Borrowing(PaidMarketWorld.WithInventory(state, inventory), actor, 0);
        using var world = Restore(state);
        var request = new GoodsRequest(GoodsUse.Spend, actor, GoodsOwners.One(owner), GoodsKinds.One("flour"), Explain: true);
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        Assert.Contains(("spend-stall-stock", GoodsReason.MarketStall), world.FindGoods(request).Excluded);
        Assert.Null(world.RecheckGoods(request, "spend-stall-stock"));
        var selling = world.RecheckGoods(request with { Use = GoodsUse.Sell }, "spend-stall-stock");
        Assert.NotNull(selling);
        Assert.Equal(2, selling.Quantity);
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Theory]
    [InlineData(GoodsUse.Spend)]
    [InlineData(GoodsUse.Sell)]
    public void SpendingQueryKeepsSplitQuantitiesAndRechecksReservations(GoodsUse use)
    {
        var state = Prepared();
        var actor = Actor(state);
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "spend-a", "gold_ore", actor, 2);
        inventory = InventoryFixture.AddLot(inventory, "spend-b", "gold_ore", actor, 3);
        inventory = InventoryFixture.Reserve(inventory, "spend-held", actor, "spend-b", 2, "work", long.MaxValue);
        inventory = InventoryFixture.AddLot(inventory, "spend-broken", "gold_ore", actor, 1, conditionBasisPoints: 0);
        inventory = InventoryFixture.AddLot(inventory, "spend-spoiled", "gold_ore", actor, 1, freshnessBasisPoints: 0);
        using var world = Restore(WithInventory(state, inventory));
        var request = new GoodsRequest(use, actor, GoodsOwners.One(actor), GoodsKinds.One("gold_ore"), Explain: true);
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var answer = world.FindGoods(request);
        Assert.Equal(new[] { ("spend-a", 2), ("spend-b", 1) }, answer.Matches.Select(match => (match.Lot.Id, match.Quantity)));
        Assert.Equal(3, answer.Total);
        Assert.Equal("spend-a", answer.First!.Lot.Id);
        Assert.Contains(("spend-broken", GoodsReason.Damaged), answer.Excluded);
        Assert.Contains(("spend-spoiled", GoodsReason.Spoiled), answer.Excluded);
        Assert.Equal(answer.Matches, world.FindGoods(request with { Explain = false }).Matches);
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        inventory = InventoryFixture.Reserve(inventory, "spend-rest", actor, "spend-a", 2, "work", long.MaxValue);
        using var changed = Restore(WithInventory(state, inventory));
        Assert.Null(changed.RecheckGoods(request, "spend-a"));
        Assert.Equal(1, changed.FindGoods(request).Total);
        using var loaded = Restore(PrivateWorldRuntimeCodec.Decode(before));
        Assert.Equal(answer.Matches, loaded.FindGoods(request).Matches);
    }

    [Fact]
    public void PersonalStorageQueryKeepsDamagedPropertyButRefusesReservedVesselFamilies()
    {
        var state = Prepared();
        var actor = Actor(state);
        var inventory = InventoryFixture.AddLot(state.Society.Society.Inventory, "store-broken", "stone", actor, 2,
            conditionBasisPoints: 0);
        inventory = InventoryFixture.AddLot(inventory, "store-pot", "storage_pot", actor, 1);
        inventory = InventoryFixture.AddLot(inventory, "store-food", "fruit", actor, 2, containerLotId: "store-pot");
        inventory = InventoryFixture.Reserve(inventory, "store-food-held", actor, "store-food", 1, "work", long.MaxValue);
        using var world = Restore(WithInventory(state, inventory));
        var request = new GoodsRequest(GoodsUse.StorePersonal, actor, GoodsOwners.One(actor),
            new GoodsKinds(["stone", "storage_pot", "fruit"]), Explain: true);
        var before = PrivateWorldRuntimeCodec.Encode(world.ExportState());
        var answer = world.FindGoods(request);
        Assert.Equal("store-broken", Assert.Single(answer.Matches).Lot.Id);
        Assert.Equal(2, answer.Total);
        Assert.Contains(("store-pot", GoodsReason.Reservation), answer.Excluded);
        Assert.Contains(("store-food", GoodsReason.Place), answer.Excluded);
        Assert.Null(world.RecheckGoods(request with { Use = GoodsUse.Spend }, "store-broken"));
        Assert.Equal(before, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        inventory = InventoryFixture.ReleaseReservation(inventory, "store-food-held");
        using var released = Restore(WithInventory(state, inventory));
        var vessel = released.RecheckGoods(request, "store-pot");
        Assert.NotNull(vessel);
        Assert.Equal(3, vessel.MoveUnits);
    }
}
