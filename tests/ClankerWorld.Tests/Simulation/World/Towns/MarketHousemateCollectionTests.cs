using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class MarketHousemateCollectionTests
{
    private const string Seller = NormalPathWorld.Founder1;
    private const string Housemate = NormalPathWorld.Founder2;
    private const string Stock = "housemate-market-wood";

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, true)]
    public async Task HouseholdStockStaysWithItsBorrowerUntilTheyLeaveOrCollectItThemselves(
        bool sellerLeaves, bool sellerCollects, bool expected, bool otherHouseholdBorrows)
    {
        var state = await PaidMarketWorld.StateAsync();
        var household = PaidMarketWorld.HouseholdOf(state, Seller);
        Assert.Equal(household, PaidMarketWorld.HouseholdOf(state, Housemate));
        var market = PaidMarketWorld.Market(state);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, Stock, "wood", household, 4);
        inventory = InventoryAuthority.Relocate(inventory, "prepare-market-load", Stock, household, 4, carrierId: Seller);
        var totalWood = inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "wood").Sum(lot => lot.Quantity);
        state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), Seller,
            MarketContent.StallEntrance(market.Site, 0)) with
        { JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off };
        var stocking = MarketTestRules.Policy(Seller, "market_borrow:", "market_deposit:");
        using var setup = WorldRuntime.Restore(state, stocking.CreateProvider);
        for (var tick = 0; tick < 20 && PaidMarketWorld.Market(setup).StockReceipts.Count == 0; tick++)
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var receipt = Assert.Single(PaidMarketWorld.Market(setup).StockReceipts);
        Assert.Equal((household, "wood", 4), (receipt.OwnerId, receipt.ItemKind, receipt.Quantity));
        Assert.Single(PaidMarketWorld.Market(setup).Occupancies);
        state = setup.ExportState();
        if (sellerLeaves)
        {
            var leaving = MarketTestRules.Policy(Seller, "market_leave:");
            using var departing = WorldRuntime.Restore(state, leaving.CreateProvider);
            for (var tick = 0; tick < 12 && PaidMarketWorld.Market(departing).Occupancies.Any(item => item.EndedTick is null); tick++)
                Assert.True((await departing.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(PaidMarketWorld.Market(departing).Occupancies, item => item.EndedTick is null);
            state = departing.ExportState();
        }
        if (otherHouseholdBorrows)
        {
            var nextSeller = state.Society.Society.Agents.First(person =>
                person.HouseholdId is not null && person.HouseholdId != household &&
                person.Status == SocietyAgentStatus.Active && person.AgeBand == SocietyAgeBand.Adult).Id;
            inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
                "next-household-market-load", "wood", nextSeller, 4);
            inventory = InventoryAuthority.Relocate(inventory, "prepare-next-market-load",
                "next-household-market-load", nextSeller, 4, carrierId: nextSeller);
            state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), nextSeller,
                MarketContent.StallEntrance(market.Site, 0));
            var borrowing = MarketTestRules.Policy(nextSeller, "market_borrow:");
            using var next = WorldRuntime.Restore(state, borrowing.CreateProvider);
            for (var tick = 0; tick < 12 && !PaidMarketWorld.Market(next).Occupancies.Any(item => item.EndedTick is null); tick++)
                Assert.True((await next.AdvanceOneTickAsync()).Advanced);
            var active = Assert.Single(PaidMarketWorld.Market(next).Occupancies, item => item.EndedTick is null);
            Assert.Equal(nextSeller, active.SellerAgentId);
            Assert.NotEqual(household, active.SellerHouseholdId);
            Assert.Equal(market.Stalls.Single(item => item.SlotIndex == 0).BuildingId, active.StallBuildingId);
            state = next.ExportState();
        }
        var collector = sellerCollects ? Seller : Housemate;
        var stall = PaidMarketWorld.StallTile(state, 0);
        state = PaidMarketWorld.At(state, collector, stall);
        state = state with
        {
            Agents = state.Agents.Select(person => person with
            { LastDecisionContext = null }).ToArray()
        };
        var choices = MarketTestRules.Policy(collector, "market_collect:");
        using var world = WorldRuntime.Restore(state, choices.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            MarketTestRules.Policy(collector, "market_collect:").CreateProvider);
        world.Validate();
        for (var tick = 0; tick < 8; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var offered = choices.OfferedTo(collector).Where(candidate => candidate.Id.StartsWith("market_collect:", StringComparison.Ordinal)).ToArray();
        if (expected) Assert.NotEmpty(offered);
        else Assert.Empty(offered);
        Assert.Equal(expected ? 4 : 0, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == household &&
            lot.ItemKind == "wood" && PersonalEquipmentRules.IsCarried(lot, collector)).Sum(lot => lot.Quantity));
        Assert.Equal(expected ? 0 : 4, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == household &&
            lot.ItemKind == "wood" && lot.GroundPosition == new InventoryGroundPosition(stall.X, stall.Y)).Sum(lot => lot.Quantity));
        Assert.Equal(sellerLeaves && !otherHouseholdBorrows ? 0 : 1,
            PaidMarketWorld.Market(world).Occupancies.Count(item => item.EndedTick is null));
        Assert.Equal(totalWood, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == household && lot.ItemKind == "wood")
            .Sum(lot => lot.Quantity));
        MarketTestRules.AssertReload(world);
    }
}
