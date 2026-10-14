using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownMembershipTests
{
    [Fact]
    public async Task SalvagePlanRefusesChangedVesselContentsInsteadOfTakingAnUnplannedLoad()
    {
        var actor = Founders[0];
        var state = WithTowns(Generated("town-salvage-plan-family"), Founders, []);
        var position = Board(state, Second);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "planned-salvage-pot", "storage_pot", Second, 1,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "planned-salvage-food", "food", Second, 1,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.PutIntoContainer(inventory, "planned-salvage-fill", Second,
            "planned-salvage-pot", "planned-salvage-food", 1);
        var model = new ScriptedModel();
        using var world = Reopen(Calm(At(WithInventory(state, inventory), position, actor)), model);
        for (var tick = 0; tick < 8 && model.ObservationsOf(actor).Length == 0; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.Single(model.ObservationsOf(actor).SelectMany(observation => observation.Candidates)
            .Select(candidate => candidate.ActionPlan).OfType<TownStockActionPlan>(),
            item => item.Choice.StockId == "planned-salvage-pot");
        Assert.Equal(2, plan.Family!.Count);
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "extra-salvage-food", "food", Second, 1,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.PutIntoContainer(inventory, "extra-salvage-fill", Second,
            "planned-salvage-pot", "extra-salvage-food", 1);
        using var changed = Reopen(WithInventory(checkpoint, inventory), model);
        var check = changed.CheckActionPlan(plan);
        Assert.False(check.Allowed);
        Assert.Equal(ActionRefusalReason.StockUnavailable, check.Refusal!.Reason);
        Assert.Contains("contents have changed", check.Refusal.Explanation, StringComparison.Ordinal);
        Assert.Equal(2, plan.Family.Count);
        AssertRoundTrip(changed);
    }
}
