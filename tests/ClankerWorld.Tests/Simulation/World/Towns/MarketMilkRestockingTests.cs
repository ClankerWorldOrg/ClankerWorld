using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class MarketMilkRestockingTests
{
    private const string Store = "market-milk-stock-store";
    private const string Milk = "market-milk-stock-milk";
    private static readonly Lazy<Task<byte[]>> Prepared = new(PrepareAsync);

    [Theory]
    [InlineData("active", false)]
    [InlineData("left", true)]
    [InlineData("house", true)]
    [InlineData("carried", false)]
    public async Task MilkRestockingProtectsActiveSaleButPermitsReleasedAndHomeStock(string boundary, bool expected)
    {
        var state = WorldRuntimeCodec.Decode(await Prepared.Value);
        var (seller, collector, household) = Actors(state);
        var market = PaidMarketWorld.Market(state);
        var source = MarketContent.StallSite(market.Site, 0);
        if (boundary == "left") state = await LeaveAsync(state, seller);
        if (boundary is "house" or "carried")
        {
            var house = PaidMarketWorld.HouseOf(state, household);
            state = PaidMarketWorld.WithInventory(state, InventoryAuthority.Relocate(state.Society.Society.Inventory,
                "milk-stock-control-" + boundary, Milk, household, 2,
                storageBuildingId: boundary == "house" ? house.InstanceId : null,
                carrierId: boundary == "carried" ? collector : null));
            source = house.Position;
        }
        state = PaidMarketWorld.At(Fresh(state), collector, source);
        var choices = MilkPolicy(collector, Store);
        using var world = WorldRuntime.Restore(state, choices.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            MilkPolicy(collector, Store).CreateProvider);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 40; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var final = world.ExportState();
        Assert.Equal(expected, choices.OfferedTo(collector).Any(item => item.Id.StartsWith("shop_stock_store", StringComparison.Ordinal) && item.DestinationId == Store));
        var milk = final.Society.Society.Inventory.GetLot(Milk);
        var expectedStorage = expected ? Store : null;
        Assert.Equal((household, 2, expectedStorage), (milk.OwnerId, milk.Quantity, milk.StorageBuildingId));
        Assert.Null(milk.ContainerLotId);
        Assert.Equal(boundary == "carried" ? collector : null, milk.CarrierId);
        Assert.Equal(expected || boundary == "carried" ? null : new InventoryGroundPosition(source.X, source.Y), milk.GroundPosition);
        Assert.Equal(2, final.Society.Society.Inventory.Lots.Where(item => item.Id == Milk).Sum(item => item.Quantity));
        Assert.Equal(Assert.Single(market.StockReceipts), Assert.Single(PaidMarketWorld.Market(final).StockReceipts));
        Assert.Equal(boundary == "left" ? 0 : 1, PaidMarketWorld.Market(final).Occupancies.Count(item => item.EndedTick is null));
        Assert.Equal(expected, final.Events.Any(item => item.Kind is "store_stock_delivered" or "household_stock_delivered" && item.Detail.StartsWith(collector + ":" + Milk + ":", StringComparison.Ordinal)));
        AssertReload(world);
    }

    [Fact]
    public async Task AnAcceptedRestockingWalkStopsWhenTheSellerBorrowsTheStallAgain()
    {
        var state = WorldRuntimeCodec.Decode(await Prepared.Value);
        var (seller, collector, _) = Actors(state);
        state = await LeaveAsync(state, seller);
        var source = MarketContent.StallSite(PaidMarketWorld.Market(state).Site, 0);
        // Leaving the stall releases the goods but does not vacate their pickup tile.
        state = PaidMarketWorld.At(state, seller, MarketContent.StallEntrance(PaidMarketWorld.Market(state).Site, 0));
        Assert.DoesNotContain(state.Agents, person => person.Position == source);
        var away = state.Map.Tiles.Select(item => item.Position).First(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, source) is >= 6 and <= 8 &&
            state.Map.IsReachableOnFoot(point, source) &&
            state.Agents.All(person => person.Position != point));
        state = PaidMarketWorld.At(state, collector, away);
        var borrowing = false;
        var choices = MilkPolicy(collector, Store);
        var milkChoice = choices.Choose;
        choices.Choose = (actor, candidates) => actor == seller && borrowing
            ? candidates.FirstOrDefault(item => item.Id.StartsWith("market_borrow:", StringComparison.Ordinal)) ??
                candidates.Single(item => item.Id == "safe_idle")
            : milkChoice(actor, candidates);
        using var world = WorldRuntime.Restore(state, choices.CreateProvider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var walking = world.ExportState();
        Assert.StartsWith("shop_stock_store", walking.Society.Cognition.Runtimes.Single(item => item.AgentId == collector).CurrentIntention!.CandidateId);
        Assert.Equal(new InventoryGroundPosition(source.X, source.Y), world.Society.Inventory.GetLot(Milk).GroundPosition);
        borrowing = true;
        _ = world.SubmitInstruction(new("milk-stall-reborrow", "owner:test", seller,
            OwnerInstructionKind.Suggestion, "Borrow the Market stall again with your spare goods."));
        for (var tick = 0; tick < 12 && !PaidMarketWorld.Market(world).Occupancies.Any(item => item.EndedTick is null); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Single(PaidMarketWorld.Market(world).Occupancies, item => item.EndedTick is null && item.SellerAgentId == seller);
        for (var tick = 0; tick < 40; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(new InventoryGroundPosition(source.X, source.Y), world.Society.Inventory.GetLot(Milk).GroundPosition);
        Assert.Null(world.Society.Inventory.GetLot(Milk).CarrierId);
        Assert.Equal(((string?)null, 2), (world.Society.Inventory.GetLot(Milk).ContainerLotId, world.Society.Inventory.GetLot(Milk).Quantity));
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "store_stock_collected" && item.Detail.StartsWith(collector + ":" + Milk + ":", StringComparison.Ordinal));
        Assert.Single(PaidMarketWorld.Market(world).StockReceipts);
        AssertReload(world);
    }

    private static async Task<byte[]> PrepareAsync()
    {
        var state = await PaidMarketWorld.StateAsync();
        var (seller, _, household) = Actors(state);
        var definition = state.WorldContent!.Buildings.Single(item => item.LocalId == "store-1x1");
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definition.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "milk-stock-store-cost-" + cost.ResourceId, cost.ResourceId, household, cost.Amount);
        using (var builder = WorldRuntime.Restore(PaidMarketWorld.WithInventory(state, inventory)))
        {
            foreach (var point in PaidMarketWorld.Town(state).BorderTiles.OrderBy(tile => state.Map.FootDistance(PaidMarketWorld.Market(state).Site, tile)))
                if (builder.PlaceBuilding(Store, definition.CanonicalId, point, household).Applied) break;
            Assert.Contains(builder.WorldSimulation.Buildings, item => item.InstanceId == Store && item.HouseholdId == household);
            state = builder.ExportState();
        }
        inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, Milk, "milk", household, 2);
        inventory = InventoryAuthority.Relocate(inventory, "milk-stock-seller-cargo", Milk, household, 2, carrierId: seller);
        var house = PaidMarketWorld.HouseOf(state, household);
        inventory = InventoryAuthority.AddLot(inventory, "zzz-milk-stock-household-reserve", "milk", household,
            2 * state.Society.Society.GetHousehold(household).MemberIds.Count, storageBuildingId: house.InstanceId);
        inventory = InventoryAuthority.AddLot(inventory, "milk-stock-borrow-stone", "stone", seller, 1);
        state = PaidMarketWorld.At(Fresh(PaidMarketWorld.WithInventory(state, inventory)), seller,
            MarketContent.StallEntrance(PaidMarketWorld.Market(state).Site, 0)) with
        { JevEnabled = false, RoutineHelper = RoutineHelperSettings.Off };
        var stallId = PaidMarketWorld.Market(state).Stalls.Single(item => item.SlotIndex == 0).BuildingId;
        var depositing = new MarketRulesPolicy
        {
            Choose = (actor, candidates) => actor == seller
                ? candidates.FirstOrDefault(item => item.Id.StartsWith("market_borrow:", StringComparison.Ordinal)) ??
                    candidates.FirstOrDefault(item => item.Id.StartsWith("market_deposit:", StringComparison.Ordinal) && item.DestinationId == stallId && item.Description.Contains(" milk at your borrowed stall;", StringComparison.Ordinal)) ??
                    candidates.Single(item => item.Id == "safe_idle")
                : candidates.Single(item => item.Id == "safe_idle"),
        };
        using var stocking = WorldRuntime.Restore(state, depositing.CreateProvider);
        for (var tick = 0; tick < 40 && !PaidMarketWorld.Market(stocking).StockReceipts.Any(item => item.LotId == Milk); tick++)
            Assert.True((await stocking.AdvanceOneTickAsync()).Advanced);
        var receipt = Assert.Single(PaidMarketWorld.Market(stocking).StockReceipts);
        Assert.Equal((seller, household, Milk, 2), (receipt.SellerAgentId, receipt.OwnerId, receipt.LotId, receipt.Quantity));
        Assert.Equal(new InventoryGroundPosition(MarketContent.StallSite(PaidMarketWorld.Market(stocking).Site, 0).X,
            MarketContent.StallSite(PaidMarketWorld.Market(stocking).Site, 0).Y), stocking.Society.Inventory.GetLot(Milk).GroundPosition);
        stocking.Validate();
        state = stocking.ExportState();
        var collector = Actors(state).Collector;
        inventory = state.Society.Society.Inventory;
        // Keep this fixture focused on milk: otherwise ordinary Store stocking
        // correctly chooses unrelated founder clothing before the tested milk.
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot =>
            lot.OwnerId != household && lot.OwnerId != collector || lot.ItemKind == "milk").ToArray()
        };
        state = PaidMarketWorld.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => state.Society.Society.GetAgent(person.AgentId).HouseholdId == household
                ? person with { Equipment = null } : person).ToArray(),
        };
        return WorldRuntimeCodec.Encode(state);
    }

    private static async Task<WorldRuntimeState> LeaveAsync(WorldRuntimeState state, string seller)
    {
        var choices = new MarketRulesPolicy
        {
            Choose = (actor, candidates) => actor == seller
                ? candidates.FirstOrDefault(item => item.Id.StartsWith("market_leave:", StringComparison.Ordinal)) ?? candidates.Single(item => item.Id == "safe_idle")
                : candidates.Single(item => item.Id == "safe_idle"),
        };
        using var world = WorldRuntime.Restore(Fresh(state), choices.CreateProvider);
        for (var tick = 0; tick < 12 && PaidMarketWorld.Market(world).Occupancies.Any(item => item.EndedTick is null); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.DoesNotContain(PaidMarketWorld.Market(world).Occupancies, item => item.EndedTick is null);
        // The seller has relinquished the stall. Keep its exact ground-stock
        // tile free for ordinary pickups, which require standing at the lot.
        return PaidMarketWorld.At(Fresh(world.ExportState()), seller,
            MarketContent.StallEntrance(PaidMarketWorld.Market(world).Site, 0));
    }

    private static (string Seller, string Collector, string Household) Actors(WorldRuntimeState state)
    {
        var seller = state.Agents[0].AgentId;
        var household = PaidMarketWorld.HouseholdOf(state, seller);
        return (seller, state.Society.Society.Agents.First(person => person.HouseholdId == household && person.Id != seller).Id, household);
    }

    private static WorldRuntimeState Fresh(WorldRuntimeState state) => state with
    {
        Agents = state.Agents.Select(person => person with
        { FullnessBasisPoints = 9_500, Survival = new SurvivalCondition(), LastDecisionContext = null, Project = null }).ToArray(),
    };

    private static MarketRulesPolicy MilkPolicy(string actor, string destination) => new()
    {
        Choose = (person, candidates) => person == actor
            ? candidates.FirstOrDefault(item => item.Id == "haul_household_stock" && item.DestinationId == destination) ??
                candidates.FirstOrDefault(item => item.Id == "shop_stock_store" && item.DestinationId == destination) ??
                candidates.Single(item => item.Id == "safe_idle")
            : candidates.Single(item => item.Id == "safe_idle"),
    };

    private static void AssertReload(WorldRuntime world)
    {
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
