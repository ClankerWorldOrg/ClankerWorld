using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HouseholdDepartureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DepartureAllowanceRespectsAStallHoldAddedAfterItsChoice(bool retainedChoice)
    {
        const string seller = "founder:00000000000000000000000000000001";
        const string actor = "founder:00000000000000000000000000000002";
        const string food = "000-departure-stall-food";
        var state = await PaidMarketWorld.StateAsync();
        var household = PaidMarketWorld.HouseholdOf(state, actor);
        Assert.Equal(household, PaidMarketWorld.HouseholdOf(state, seller));
        var stall = PaidMarketWorld.StallTile(state, 0);
        state = PaidMarketWorld.WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory,
            food, "food", household, 4, groundPosition: new(stall.X, stall.Y)));
        var chooser = new DeparturePlanChooser(actor, choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<DepartureActionPlan>(chooser.Plan);
        Assert.Equal(food, Assert.Single(plan.Allowance).Stock.Id);
        Assert.Equal(2, Assert.Single(plan.Allowance).Quantity);

        state = PaidMarketWorld.Borrowing(capture.ExportState(), seller, 0);
        using var held = WorldRuntime.Restore(state);
        var stock = held.Society.Inventory.GetLot(food);
        Assert.Contains(stock.Holds!, hold => hold.Purpose == InventoryHoldPurpose.MarketStall);
        if (retainedChoice)
        {
            var bytes = WorldRuntimeCodec.Encode(held.ExportState());
            Assert.Equal(ActionRefusalReason.StockUnavailable, held.CheckActionPlan(plan).Refusal?.Reason);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(held.ExportState()));
            Assert.Equal(household, held.Society.GetAgent(actor).HouseholdId);
        }
        else
        {
            Assert.True(held.DisplaceAdult(actor));
            Assert.Equal(stock, held.Society.Inventory.GetLot(food));
            held.Validate();
        }

        // Ending the borrowing releases the same physical stock. The original
        // choice can use it again, and native departure transfers two portions.
        var releasedState = PaidMarketWorld.WithMarket(state, market => market with
        {
            Occupancies = market.Occupancies.Select(item => item with
            { EndedTick = state.Society.Society.WorldTick, ReleaseReason = "The seller gave up the stall." }).ToArray(),
        });
        using var released = WorldRuntime.Restore(releasedState);
        Assert.True(released.CheckActionPlan(plan).Allowed);
        Assert.True(released.DisplaceAdult(actor));
        var departure = Assert.Single(released.Agents.Single(person => person.AgentId == actor).Departures!);
        Assert.Equal(2, departure.AllowancePortions);
        Assert.Equal(2, released.Society.Inventory.GetLot(food).Quantity);
        var allowance = released.Society.Inventory.GetLot(Assert.Single(departure.AllowanceLotIds));
        Assert.Equal((actor, 2, stock.GroundPosition), (allowance.OwnerId, allowance.Quantity, allowance.GroundPosition));
        var saved = WorldRuntimeCodec.Encode(released.ExportState());
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }
}
