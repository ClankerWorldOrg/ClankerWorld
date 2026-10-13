using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HandcartRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CartRepairOrSupplyPickupKeepsItsPlanAcrossRollbackReplayAndReload(bool pickup)
    {
        var (state, actor) = await RepairFixture(pickup);
        var chooser = new RepairChooser(actor, pickup, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new RepairChooser(actor, pickup, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Same(chooser.Plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        if (pickup)
        {
            var plan = Assert.IsType<HandcartRepairInputActionPlan>(chooser.Plan);
            Assert.Equal("cargo-plan-stock", plan.Pickup.StockId);
            Assert.Equal(5, world.Society.Inventory.GetLot(plan.Pickup.StockId).Quantity);
            Assert.Single(world.ExportState().Events, item => item.Kind == "equipment_collected");
            Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
                lot.ItemKind == "wood" && ToolProgressionRules.IsTopLevelCarriedLot(lot, actor)).Sum(lot => lot.Quantity));
            Assert.Equal(1_000, world.Society.Inventory.GetLot(plan.CartId).ConditionBasisPoints);
        }
        else
        {
            var plan = Assert.IsType<HandcartRepairActionPlan>(chooser.Plan);
            Assert.Equal(3, plan.Materials.Count);
            Assert.All(plan.Materials, material => Assert.Equal(2, world.Society.Inventory.GetLot(material.StockId).Quantity));
            Assert.Equal(10_000, world.Society.Inventory.GetLot(plan.Choice.CartId).ConditionBasisPoints);
            Assert.Single(world.ExportState().Events, item => item.Kind == "handcart_repaired");
        }
        var cargo = world.Society.Inventory.GetLot("cargo-plan-inside");
        Assert.Equal((actor, "cargo-plan-cart", 4), (cargo.OwnerId, cargo.ContainerLotId, cargo.Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewRepairSuppliesDoNotReplaceThePlanAndClaimsRefuseItsSelectedStock(bool pickup)
    {
        var (state, actor) = await RepairFixture(pickup);
        var chooser = new RepairChooser(actor, pickup, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsAssignableFrom<ActionPlan>(chooser.Plan);
        var stock = pickup ? Assert.IsType<HandcartRepairInputActionPlan>(plan).Pickup :
            new PlannedEquipmentPickup(Assert.IsType<HandcartRepairActionPlan>(plan).Materials[0].StockId, actor, "wood",
                world.Agents.Single(person => person.AgentId == actor).Position, 0);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-earlier-repair-supply", "wood", stock.OwnerId, 1,
            groundPosition: pickup ? new(stock.Position.X, stock.Position.Y) : null);
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "repair-plan-other-work", stock.OwnerId, stock.StockId,
            inventory.GetLot(stock.StockId).Quantity, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
        Assert.Equal(1_000, claimed.Society.Inventory.GetLot("cargo-plan-cart").ConditionBasisPoints);
    }

    private static async Task<(WorldRuntimeState State, string Actor)> RepairFixture(bool pickup)
    {
        var (state, actor) = await CargoFixture(pickup);
        var inventory = state.Society.Society.Inventory with
        { Lots = state.Society.Society.Inventory.Lots.Select(lot => lot.Id == "cargo-plan-cart" ? lot with { ConditionBasisPoints = 1_000 } : lot).ToArray() };
        if (!pickup)
            foreach (var kind in new[] { "wood", "iron_fittings", "rope" })
                inventory = InventoryAuthority.AddLot(inventory, "repair-plan-" + kind, kind, actor, 3);
        return (state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } }, actor);
    }

    private sealed class RepairChooser(string actor, bool pickup, bool choose) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item => pickup
                ? item.ActionPlan is HandcartRepairInputActionPlan input && input.Choice.ItemKind == "wood"
                : item.ActionPlan is HandcartRepairActionPlan repair && repair.Choice.CartId == "cargo-plan-cart") : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
