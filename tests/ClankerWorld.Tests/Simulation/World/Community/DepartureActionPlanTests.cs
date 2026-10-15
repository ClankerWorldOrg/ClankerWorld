using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HouseholdDepartureTests
{
    [Fact]
    public async Task ADepartureRetainsItsCareGroupAndExactAllowanceAcrossRollbackReplayAndReload()
    {
        using var initial = NormalPathWorld.CreateGenerated("departure-plan-café-🌲", _ => new Choices());
        var actor = initial.Society.GetHousehold(Alpha).MemberIds[0];
        var state = initial.ExportState() with
        { Agents = initial.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        var chooser = new DeparturePlanChooser(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new DeparturePlanChooser(actor, choose: true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<DepartureActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal(plan.Choice, DepartureChoice.Decode(plan.CandidateId));
        var departure = Assert.Single(world.Agents.Single(person => person.AgentId == actor).Departures!);
        Assert.Equal(plan.CareGroup, departure.CareGroup);
        Assert.Equal(plan.Allowance.Sum(item => item.Quantity), departure.AllowancePortions);
        foreach (var selected in plan.Allowance)
            Assert.Equal(selected.Quantity, world.Society.Inventory.Lots.Where(lot => lot.OwnerId == actor &&
                (lot.Id == selected.Stock.Id || lot.ProvenanceLotId == selected.Stock.Id)).Sum(lot => lot.Quantity));
        Assert.Null(world.Society.GetAgent(actor).HouseholdId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task ADepartureRefusesSpentAllowanceInsteadOfSelectingDifferentFood()
    {
        using var initial = NormalPathWorld.CreateGenerated("departure-plan-stock", _ => new Choices());
        var actor = initial.Society.GetHousehold(Alpha).MemberIds[0];
        var chooser = new DeparturePlanChooser(actor, choose: false);
        using var capture = WorldRuntime.Restore(initial.ExportState(), _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<DepartureActionPlan>(chooser.Plan);
        var selected = Assert.Single(plan.Allowance);
        var state = capture.ExportState();
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "departure-test-spent-food",
            selected.Stock.OwnerId, selected.Stock.Id, selected.Stock.Quantity, "food_eaten", capture.WorldTick + 1);
        inventory = InventoryAuthority.ConsumeReservation(inventory, "departure-test-spent-food");
        var changedState = WithInventory(state, inventory);
        using var changed = WorldRuntime.Restore(changedState);
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Equal(Alpha, changed.Society.GetAgent(actor).HouseholdId);
        Assert.Empty(changed.Agents.Single(person => person.AgentId == actor).Departures ?? []);
    }

    private sealed class DeparturePlanChooser(string actor, bool choose) : IDecisionProvider
    {
        public DepartureActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is DepartureActionPlan plan && plan.Choice.Kind == DepartureChoiceKind.Leave) : null;
            if (candidate is not null) Plan = Assert.IsType<DepartureActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
