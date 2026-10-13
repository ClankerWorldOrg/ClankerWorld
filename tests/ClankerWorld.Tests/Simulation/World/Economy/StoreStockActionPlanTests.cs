using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ShopTradeTests
{
    [Theory]
    [InlineData("reserved", ActionRefusalReason.StockUnavailable)]
    [InlineData("full", ActionRefusalReason.Capacity)]
    public async Task StorePlanKeepsItsSelectedLotAndLoadAndRefusesChangedStockOrShelfRoom(string change, ActionRefusalReason reason)
    {
        var (state, actor, household, _, storeId) = CreateStoreStockFixture();
        var store = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == storeId);
        state = WithInventory(state, InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor),
            "store-plan-berries", "berries", actor, 7)) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = store.Position } : person).ToArray(),
        };
        var choices = new StorePlanChoices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<StoreStockActionPlan>(choices.Plan);
        Assert.Equal((storeId, "store-plan-berries", 4, true), (plan.StoreId, plan.StockId, plan.Quantity, plan.DirectDelivery));
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-store-berries", "berries", actor, 1);
        using var added = WorldRuntime.Restore(WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = change == "reserved"
            ? InventoryAuthority.Reserve(inventory, "store-plan-other-work", actor, plan.StockId, 5, "other_work", inventory.WorldTick + 100)
            : InventoryAuthority.AddLot(inventory, "store-plan-new-shelf", "berries", household, 5, storageBuildingId: storeId);
        using var changed = WorldRuntime.Restore(WithInventory(checkpoint, inventory));
        Assert.Equal(reason, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal((storeId, "store-plan-berries", 4), (plan.StoreId, plan.StockId, plan.Quantity));
        changed.Validate();
    }

    [Fact]
    public async Task StorePlanCarriesTheSameLotThroughDispatchRollbackReplayAndStrictReload()
    {
        var (state, actor, household, _, storeId) = CreateStoreStockFixture();
        var store = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == storeId);
        state = WithInventory(state, InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor),
            "store-plan-carried-berries", "berries", actor, 8)) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = store.Position } : person).ToArray(),
        };
        var choices = new StorePlanChoices(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new StorePlanChoices(actor, choose: true));
        var carriedPlan = false;
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "store_stock_delivered"); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            carriedPlan |= result.Decisions.Any(decision => decision.AgentId == actor && ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(carriedPlan);
        var plan = Assert.IsType<StoreStockActionPlan>(choices.Plan);
        Assert.Equal($"{actor}:{plan.StockId}:4:{storeId}", Assert.Single(world.ExportState().Events, item => item.Kind == "store_stock_delivered").Detail);
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == household && lot.StorageBuildingId == storeId &&
            (lot.Id == plan.StockId || lot.ProvenanceLotId == plan.StockId)).Sum(lot => lot.Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private sealed class StorePlanChoices(string actor, bool choose) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "shop_stock_store") : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
