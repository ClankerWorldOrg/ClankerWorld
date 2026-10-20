using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class HouseholdStockActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpareFoodPlanKeepsItsOriginalLotAndQuantityWhenOtherFoodBecomesAvailable(bool reserveSelected)
    {
        var state = ShelterOrderTestFixture.WithClearWeather(ShelterOrderTestFixture.Prepared());
        var actor = ShelterOrderTestFixture.Actor(state);
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        state = ShelterOrderTestFixture.At(state, actor, house.Position);
        state = state with { Agents = state.Agents.Select(person => person.AgentId == actor ? person with { FullnessBasisPoints = 9_000 } : person).ToArray() };
        state = ShelterOrderTestFixture.WithInventory(state,
            InventoryAuthority.AddLot(state.Society.Society.Inventory, "house-plan-food", "food", actor, 5));
        var provider = new HouseholdChoices(actor, "store_household_food", choose: false);
        using var world = WorldRuntime.Restore(state, _ => provider);
        for (var tick = 0; tick < 8 && provider.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HouseholdStockActionPlan>(provider.Plan);
        Assert.Equal((household, "house-plan-food", 4), (plan.HouseholdId, plan.StockId, plan.Quantity));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-other-house-food", "berries", actor, 2);
        if (reserveSelected)
            inventory = InventoryAuthority.Reserve(inventory, "house-plan-food-hold", actor, plan.StockId, 2,
                "other_work", checkpoint.Society.Society.WorldTick + 100);
        using var changed = WorldRuntime.Restore(ShelterOrderTestFixture.WithInventory(checkpoint, inventory), _ => provider);
        Assert.Equal(!reserveSelected, changed.CheckActionPlan(plan).Allowed);
        if (reserveSelected) Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(("house-plan-food", 4), (plan.StockId, plan.Quantity));
        changed.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryUsesTheSameSelectedShipmentAcrossRollbackReplayAndStrictReload(bool longStockIdentity)
    {
        var state = ShelterOrderTestFixture.WithClearWeather(ShelterOrderTestFixture.Prepared());
        var actor = ShelterOrderTestFixture.Actor(state);
        const string house = "first-town-house-a";
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var pile = state.Map.MapObjects.FirstOrDefault(item => item.Id == "storage")?.Position ??
            state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse").Position;
        state = ShelterOrderTestFixture.At(state, actor, pile);
        var stockId = longStockIdentity ? "house-plan-" + new string('x', 117) : "house-plan-unusable";
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, stockId, "wood", actor, 4,
            freshnessBasisPoints: 0);
        state = ShelterOrderTestFixture.WithInventory(state, InventoryAuthority.AssignDelivery(inventory, stockId, house));
        var provider = new HouseholdChoices(actor, "recover_household_delivery", choose: true);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ =>
            new HouseholdChoices(actor, "recover_household_delivery", choose: true));
        var decisions = new List<ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult>();
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "household_delivery_recovered"); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            decisions.AddRange(result.Decisions);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        var plan = Assert.IsType<HouseholdStockActionPlan>(provider.Plan);
        Assert.Contains(decisions, decision => decision.AgentId == actor && ReferenceEquals(decision.ActionPlan, plan));
        Assert.Single(world.ExportState().Events, item => item.Kind == "household_delivery_recovered");
        var recovered = world.Society.Inventory.GetLot(plan.StockId);
        Assert.Equal((household, 4, 0, (string?)null, new InventoryGroundPosition(pile.X, pile.Y)),
            (recovered.OwnerId, recovered.Quantity, recovered.FreshnessBasisPoints, recovered.DeliveryBuildingId, recovered.GroundPosition));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private sealed class HouseholdChoices(string actor, string wanted, bool choose) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == wanted) : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
