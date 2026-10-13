using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ChildActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedStockAndQuantityStayFixedWhenOtherStockBecomesAvailable(bool reserveSelected)
    {
        var (state, child, _, source) = await ChildHouseholdHelpingTests.Prepared("food");
        var household = state.Society.Society.GetAgent(child).HouseholdId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "child-plan-stock", "wood", household, 4,
            groundPosition: new(source.Position.X, source.Position.Y));
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        var provider = new PlanRecorder("child_carry:child-plan-stock");
        using var world = WorldRuntime.Restore(state, id => id == child ? provider : new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 8 && provider.Plan is null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ChildActionPlan>(provider.Plan);
        Assert.Equal(("child-plan-stock", 4), (plan.StockId, plan.Quantity));
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());

        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-other-child-stock", "wood", household, 4,
            groundPosition: new(source.Position.X, source.Position.Y));
        if (reserveSelected)
            inventory = InventoryAuthority.Reserve(inventory, "child-plan-reservation", household, plan.StockId!, 1,
                "other_work", checkpoint.Society.Society.WorldTick + 100);
        checkpoint = checkpoint with { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } };
        using var changed = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(checkpoint)),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var check = changed.CheckActionPlan(plan);
        Assert.Equal(!reserveSelected, check.Allowed);
        if (reserveSelected)
        {
            Assert.Equal(ActionRefusalReason.StockUnavailable, Assert.IsType<ActionRefusal>(check.Refusal).Reason);
            Assert.NotEmpty(check.Refusal.Explanation);
        }
        Assert.Equal(("child-plan-stock", 4), (plan.StockId, plan.Quantity));
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        changed.Validate();
    }

    [Fact]
    public void PlansNeverEnterCandidateJsonOrChangeModelText()
    {
        var candidate = new CognitionCandidate("child_help_food", "Carry one spare food serving back to the household store.", 26);
        var planned = candidate with
        {
            ActionPlan = new ChildActionPlan("child", new(ChildActionKind.HelpFood), new(1, 2), 0,
                Array.AsReadOnly(new[] { new GridPoint(1, 2) }), StockId: "private-stock", Quantity: 1),
        };
        Assert.Equal(JsonSerializer.Serialize(candidate), JsonSerializer.Serialize(planned));
        Assert.Null(JsonSerializer.Deserialize<CognitionCandidate>(JsonSerializer.Serialize(planned))!.ActionPlan);
    }

    private sealed class PlanRecorder(string wanted) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Plan ??= request.Observation.Candidates.SingleOrDefault(candidate => candidate.Id == wanted)?.ActionPlan;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == "safe_idle" ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
