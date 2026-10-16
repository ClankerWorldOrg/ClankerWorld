using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HouseholdDepartureGoodsTests
{
    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 2)]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 0)]
    public async Task DepartureAllowanceLeavesActiveSaleStockAndUsesOnlyFreeHomePortions(
        bool displaced, bool borrowed, int homePortions)
    {
        const string household = "household:first";
        const string saleId = "a-departure-sale-food";
        const string homeId = "z-departure-home-food";
        var state = await PaidMarketWorld.StateAsync();
        var members = state.Society.Society.GetHousehold(household).MemberIds;
        var seller = members[0];
        var leaver = members[1];
        var house = PaidMarketWorld.HouseOf(state, household);
        var stall = PaidMarketWorld.StallTile(state, 0);
        // Retain the fixture's lot and reservation history while isolating usable food.
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Select(lot => lot.OwnerId == household &&
                FoodRules.IsEdible(lot.ItemKind) ? lot with { FreshnessBasisPoints = 0 } : lot).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, saleId, "bread", household, 4,
            groundPosition: new(stall.X, stall.Y));
        if (homePortions > 0)
        {
            inventory = InventoryAuthority.AddLot(inventory, homeId, "bread", household, homePortions + 2,
                storageBuildingId: house.InstanceId);
            inventory = InventoryAuthority.Reserve(inventory, "departure-home-reservation", household, homeId,
                2, "other-work", long.MaxValue);
        }
        state = PaidMarketWorld.WithInventory(state, inventory);
        if (borrowed) state = PaidMarketWorld.Borrowing(state, seller, 0);
        var policy = new MarketRulesPolicy(DecisionProviderKind.Deterministic)
        {
            Choose = (actor, candidates) => actor == leaver
                ? candidates.FirstOrDefault(candidate => candidate.Id == "household_leave") : null,
        };
        var before = WorldRuntimeCodec.Encode(state);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before), policy.CreateProvider);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(before), policy.CreateProvider);
        if (displaced)
        {
            Assert.True(world.DisplaceAdult(leaver));
            Assert.True(replay.DisplaceAdult(leaver));
        }
        for (var tick = 0; tick < 8 && world.Society.GetAgent(leaver).HouseholdId is not null; tick++)
            await AdvancePair();
        Assert.Null(world.Society.GetAgent(leaver).HouseholdId);
        var departure = Assert.Single(world.Agents.Single(person => person.AgentId == leaver).Departures!);
        Assert.Equal(borrowed ? homePortions : 2, departure.AllowancePortions);
        var sale = world.Society.Inventory.GetLot(saleId);
        Assert.Equal((household, borrowed ? 4 : 2, new InventoryGroundPosition(stall.X, stall.Y), (string?)null),
            (sale.OwnerId, sale.Quantity, sale.GroundPosition, sale.CarrierId));
        foreach (var id in departure.AllowanceLotIds)
        {
            var portion = world.Society.Inventory.GetLot(id);
            Assert.Equal(leaver, portion.OwnerId);
            Assert.Null(portion.CarrierId);
            Assert.Equal(borrowed ? house.InstanceId : null, portion.StorageBuildingId);
            Assert.Equal(borrowed ? null : new InventoryGroundPosition(stall.X, stall.Y), portion.GroundPosition);
        }
        if (homePortions > 0)
        {
            Assert.Equal(2, world.Society.Inventory.GetLot(homeId).Quantity);
            Assert.Equal(InventoryReservationState.Reserved,
                world.Society.Inventory.GetReservation("departure-home-reservation").State);
        }
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.Id == saleId || lot.ProvenanceLotId == saleId)
            .Sum(lot => lot.Quantity));
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), policy.CreateProvider);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.False(world.DisplaceAdult(leaver));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        for (var tick = 0; tick < 4; tick++) await AdvancePair();
        Assert.Single(world.Agents.Single(person => person.AgentId == leaver).Departures!);

        async Task AdvancePair()
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            world.Validate();
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
    }
}
