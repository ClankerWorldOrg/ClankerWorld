using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class RestaurantMarketDemandTests
{
    private const string Alpha = "household:first";
    private const string Beta = "household:second";
    private const string SaleFlour = "restaurant-market-sale-flour";

    [Theory]
    [InlineData("borrowed")]
    [InlineData("missing")]
    [InlineData("left")]
    public async Task RestaurantPurchasesIgnoreFlourOnAnActivelyBorrowedStallUntilItsSellerLeaves(string mode)
    {
        var state = await PaidMarketWorld.StateAsync();
        var house = PaidMarketWorld.HouseOf(state, Beta);
        var restaurantDefinition = state.WorldContent!.Buildings.Single(definition => definition.LocalId == "restaurant-1x2");
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in restaurantDefinition.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "restaurant-market-build-" + cost.ResourceId, cost.ResourceId,
                Beta, cost.Amount, storageBuildingId: house.InstanceId);
        using (var placing = WorldRuntime.Restore(PaidMarketWorld.WithInventory(state, inventory),
                   _ => new ActionCoverageRecorder(chooseIdle: true)))
        {
            _ = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsReachableFromStartingSiteOnFoot(point))
                .OrderBy(point => state.Map.FootDistance(house.Position, point)).First(point =>
                    placing.PlaceBuilding("restaurant-market", restaurantDefinition.CanonicalId, point, Beta).Applied);
            placing.Validate();
            state = placing.ExportState();
        }
        var beta = state.Society.Society.Agents.Where(person => person.HouseholdId == Beta &&
            person.Status == SocietyAgentStatus.Active && person.AgeBand == SocietyAgeBand.Adult).Select(person => person.Id).ToArray();
        var seller = beta[0];
        var buyer = beta[1];
        var miller = state.Society.Society.Agents.First(person => person.HouseholdId == Alpha &&
            person.Status == SocietyAgentStatus.Active && person.AgeBand == SocietyAgeBand.Adult).Id;
        var farmhouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-farmhouse");
        inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind != "flour").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "restaurant-market-onsite", "flour", Beta, 2, storageBuildingId: "restaurant-market");
        inventory = InventoryAuthority.AddLot(inventory, "restaurant-market-farm-stock", "flour", Alpha, 4, storageBuildingId: farmhouse.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "restaurant-market-payment", "berries", buyer, 4);
        inventory = InventoryAuthority.AddLot(inventory, SaleFlour, "flour", Beta, 4);
        inventory = InventoryAuthority.Relocate(inventory, "restaurant-market-sale-load", SaleFlour, Beta, 4, carrierId: seller);
        var market = PaidMarketWorld.Market(state);
        state = PaidMarketWorld.At(PaidMarketWorld.WithInventory(state, inventory), seller,
            MarketContent.StallEntrance(market.Site, 0)) with
        { JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off };
        var stocking = Policy(seller, "market_borrow:", "market_deposit:");
        using (var depositing = WorldRuntime.Restore(state, stocking.CreateProvider))
        {
            for (var tick = 0; tick < 20 && PaidMarketWorld.Market(depositing).StockReceipts.Count == 0; tick++)
                Assert.True((await depositing.AdvanceOneTickAsync()).Advanced);
            Assert.True(PaidMarketWorld.Market(depositing).StockReceipts.Count > 0,
                "Offered: " + string.Join(", ", stocking.OfferedTo(seller).Where(candidate => candidate.Id.StartsWith("market_", StringComparison.Ordinal))
                    .Select(candidate => candidate.Id).Distinct()));
            var deposit = Assert.Single(PaidMarketWorld.Market(depositing).StockReceipts);
            Assert.Equal((Beta, "flour", 4), (deposit.OwnerId, deposit.ItemKind, deposit.Quantity));
            Assert.Single(PaidMarketWorld.Market(depositing).Occupancies, occupancy => occupancy.EndedTick is null);
            state = depositing.ExportState();
        }
        if (mode == "left")
        {
            var leaving = Policy(seller, "market_leave:");
            using var departing = WorldRuntime.Restore(state, leaving.CreateProvider);
            for (var tick = 0; tick < 12 && PaidMarketWorld.Market(departing).Occupancies.Any(item => item.EndedTick is null); tick++)
                Assert.True((await departing.AdvanceOneTickAsync()).Advanced);
            Assert.DoesNotContain(PaidMarketWorld.Market(departing).Occupancies, occupancy => occupancy.EndedTick is null);
            state = PaidMarketWorld.Waiting(departing.ExportState(), seller);
        }
        if (mode == "missing")
            state = PaidMarketWorld.WithInventory(state, state.Society.Society.Inventory with
            { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.Id != SaleFlour).ToArray() });
        state = PaidMarketWorld.At(PaidMarketWorld.At(state, buyer, farmhouse.Position), miller,
            MovementTestWorld.OpenGround(state, miller, farmhouse.Position, 1));
        state = state with
        {
            Agents = state.Agents.Select(person => person with
            {
                FullnessBasisPoints = 10_000,
                Project = null,
                LastDecisionContext = null,
                Survival = new SurvivalCondition()
            }).ToArray()
        };
        // Record actual offered choices; do not collect or consume the sale stock.
        var choices = new MarketRulesPolicy(DecisionProviderKind.Deterministic)
        {
            Choose = (actor, candidates) => actor == buyer ? candidates.FirstOrDefault(candidate =>
                    candidate.Id.StartsWith("shop_continue:", StringComparison.Ordinal)) ??
                candidates.FirstOrDefault(candidate => candidate.Id == "shop_buy:" + farmhouse.InstanceId)
                : actor == miller ? candidates.FirstOrDefault(candidate => candidate.Id.StartsWith("shop_continue:", StringComparison.Ordinal)) : null
        };
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)), choices.CreateProvider);
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 24; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var purchases = world.ShopTrades.Where(trade => trade.BuyerId == buyer && trade.GoodsKind == "flour").ToArray();
        Assert.Equal(mode == "left" ? 0 : 2, purchases.Length);
        foreach (var trade in purchases)
        {
            var offer = world.Society.Inventory.GetOffer(trade.OfferId);
            Assert.Equal(DirectBarterState.Settled, offer.State);
            Assert.Equal((1, 1, "berries", Alpha), (offer.FirstQuantity, offer.SecondQuantity, trade.PaymentKind, trade.SellerHouseholdId));
        }
        Assert.Equal(mode == "left" ? 4 : 2, world.Society.Inventory.GetLot("restaurant-market-payment").Quantity);
        Assert.Equal(mode != "left", choices.OfferedTo(buyer).Any(candidate => candidate.Id == "shop_buy:" + farmhouse.InstanceId));
        Assert.Equal(mode == "left", choices.OfferedTo(buyer).Any(candidate => candidate.Id.StartsWith("market_collect:", StringComparison.Ordinal)));
        Assert.Equal(mode == "left" ? 0 : 1, PaidMarketWorld.Market(world).Occupancies.Count(occupancy => occupancy.EndedTick is null));
        if (mode != "missing")
        {
            var sale = world.Society.Inventory.GetLot(SaleFlour);
            var expected = state.Society.Society.Inventory.GetLot(SaleFlour);
            Assert.Equal(expected with { LastProcessedTick = sale.LastProcessedTick }, sale);
        }
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), choices.CreateProvider);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        restored.Validate();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static MarketRulesPolicy Policy(string actor, params string[] prefixes) => new()
    {
        Choose = (id, candidates) => id == actor ? prefixes.Select(prefix => candidates.FirstOrDefault(candidate =>
            candidate.Id.StartsWith(prefix, StringComparison.Ordinal))).FirstOrDefault(candidate => candidate is not null) : null
    };
}
