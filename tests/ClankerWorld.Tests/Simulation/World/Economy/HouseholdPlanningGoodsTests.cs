using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HouseholdPlanningGoodsTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task BuildingPlansAndShortagesUseAvailableHouseholdAndMemberStock(bool borrowed, bool memberStock)
    {
        const string household = "household:first";
        var state = await PaidMarketWorld.StateAsync();
        var members = state.Society.Society.Agents.Where(person => person.HouseholdId == household).ToArray();
        var seller = members[0].Id;
        var planner = members[1].Id;
        var house = PaidMarketWorld.HouseOf(state, household);
        var stall = PaidMarketWorld.StallTile(state, 0);
        var store = state.WorldContent!.Buildings.Single(item => item.LocalId == "store-1x1");
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind != "wood" ||
                lot.OwnerId != household && members.All(person => person.Id != lot.OwnerId)).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "planning-sale-wood", "wood", household, 16,
            groundPosition: new(stall.X, stall.Y));
        inventory = InventoryAuthority.AddLot(inventory, "planning-house-stone", "stone", household, 4,
            storageBuildingId: house.InstanceId);
        if (memberStock)
            inventory = InventoryAuthority.AddLot(inventory, "planning-member-wood", "wood", planner, 16,
                storageBuildingId: house.InstanceId);
        state = PaidMarketWorld.WithInventory(state, inventory);
        if (borrowed) state = PaidMarketWorld.Borrowing(state, seller, 0);
        var policy = new MarketRulesPolicy();
        using var world = WorldRuntime.Restore(state, policy.CreateProvider);
        for (var tick = 0; tick < 4; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var choices = policy.OfferedTo(planner).ToArray();
        Assert.NotEmpty(choices);
        var plans = choices.Where(item => TownConstructionCandidateIds.TryParse(item.Id, out var selection) &&
            selection.IsBuilding && selection.DefinitionId == store.CanonicalId).ToArray();
        if (borrowed && !memberStock)
        {
            Assert.Empty(plans);
            Assert.Contains(choices, item => item.Id == "gather_building_material:wood");
        }
        else
        {
            Assert.NotEmpty(plans);
            Assert.DoesNotContain(choices, item => item.Id == "gather_building_material:wood");
        }
        var stock = world.Society.Inventory.GetLot("planning-sale-wood");
        Assert.Equal((household, 16, (string?)null, new InventoryGroundPosition(stall.X, stall.Y)),
            (stock.OwnerId, stock.Quantity, stock.CarrierId, stock.GroundPosition));
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
    }
}
