using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownStockActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WarehousePlanKeepsItsSelectedLotAndLoadAcrossOtherStockAndReservationChanges(bool reserveSelected)
    {
        var state = ShelterOrderTestFixture.WithClearWeather(ShelterOrderTestFixture.Prepared());
        var actor = ShelterOrderTestFixture.Actor(state);
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        state = ShelterOrderTestFixture.At(state, actor, warehouse.Position);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "town-plan-selected", "wood", actor, 8);
        state = ShelterOrderTestFixture.WithInventory(state, inventory);
        var provider = new StockChoices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => provider);
        for (var tick = 0; tick < 8 && provider.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<TownStockActionPlan>(provider.Plan);
        Assert.Equal(("town-plan-selected", warehouse.InstanceId, 4), (plan.Choice.StockId, plan.WarehouseId, plan.Quantity));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-other-town-stock", "stone", actor, 8);
        if (reserveSelected)
            inventory = InventoryAuthority.Reserve(inventory, "town-plan-reservation", actor, plan.Choice.StockId, 5,
                "other_work", checkpoint.Society.Society.WorldTick + 100);
        checkpoint = ShelterOrderTestFixture.WithInventory(checkpoint, inventory);
        using var changed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(checkpoint)), _ => provider);
        var check = changed.CheckActionPlan(plan);
        Assert.Equal(!reserveSelected, check.Allowed);
        if (reserveSelected) Assert.Equal(ActionRefusalReason.StockUnavailable, check.Refusal!.Reason);
        Assert.Equal(("town-plan-selected", 4), (plan.Choice.StockId, plan.Quantity));
        changed.Validate();
    }

    [Fact]
    public async Task SelectedWarehouseLoadUsesTheSamePlanAcrossRollbackAndReplay()
    {
        var state = ShelterOrderTestFixture.WithClearWeather(ShelterOrderTestFixture.Prepared());
        var actor = ShelterOrderTestFixture.Actor(state);
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        state = ShelterOrderTestFixture.At(state, actor, warehouse.Position);
        state = ShelterOrderTestFixture.WithInventory(state,
            InventoryAuthority.AddLot(state.Society.Society.Inventory, "town-plan-load", "wood", actor, 8));
        var provider = new StockChoices(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new StockChoices(actor, choose: true));
        var decisions = new List<ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult>();
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "town_resources_stored"); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            decisions.AddRange(result.Decisions);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var plan = Assert.IsType<TownStockActionPlan>(provider.Plan);
        Assert.Contains(decisions, decision => decision.AgentId == actor && ReferenceEquals(decision.ActionPlan, plan));
        Assert.Single(world.ExportState().Events, item => item.Kind == "town_resources_stored");
        Assert.Equal(4, world.Society.Inventory.GetLot("town-plan-load").Quantity);
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == warehouse.TownId &&
            lot.ItemKind == "wood" && lot.StorageBuildingId == warehouse.InstanceId).Sum(lot => lot.Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private sealed class StockChoices(string actor, bool choose) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "store_town_resources") : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
