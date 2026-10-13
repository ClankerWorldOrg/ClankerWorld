using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class SchedulerDeferredObservationTests
{
    [Theory]
    [InlineData(false, false, ActionRefusalReason.StockUnavailable)]
    [InlineData(false, true, ActionRefusalReason.StockUnavailable)]
    [InlineData(true, true, ActionRefusalReason.ActiveOrder)]
    [InlineData(true, false, ActionRefusalReason.TargetUnavailable)]
    public void ADeferredPlannedChoiceReachesItsOriginalCheckAndRefusalInsteadOfBeingDropped(
        bool stockAvailable, bool activeOrder, ActionRefusalReason expected)
    {
        var scheduler = CreateScheduler();
        var plan = new OfferedPlan(Actor, "planned_supply");
        var earlier = Observation(1) with
        {
            Candidates = [new("safe_idle", "Rest safely."), new("planned_supply", "Fetch the selected supplies.") { ActionPlan = plan }],
        };
        Assert.True(scheduler.Enqueue(Entry(earlier, "routine_tick")));
        var request = Preview(scheduler);
        var response = PlannedReply(request);
        var checkedPlan = false;
        var prepared = DeferredPlanAdmission.Prepare(request, response, new HashSet<string>(["safe_idle"], StringComparer.Ordinal), candidate =>
        {
            Assert.Same(plan, candidate);
            checkedPlan = true;
            return stockAvailable ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supplies are gone");
        }, activeOrder);
        var decision = prepared.Complete(scheduler.CompleteDeferred(request, response, null, 0, prepared.LegalCandidateIds));
        Assert.True(checkedPlan);
        Assert.NotNull(decision);
        Assert.True(decision.Admission.Accepted);
        Assert.Same(plan, decision.ActionPlan);
        Assert.Equal(expected, decision.PlanRefusal!.Reason);
        Assert.Empty(scheduler.ExportState().Queue);
        scheduler.Validate();
    }

    [Fact]
    public void PlanAdmissionDoesNotRestoreAnUnplannedMissingChoiceOrBypassTheProviderEpoch()
    {
        var scheduler = CreateScheduler();
        var earlier = Observation(1) with
        {
            Candidates = [new("safe_idle", "Rest safely."), new("planned_supply", "Fetch supplies.")],
        };
        Assert.True(scheduler.Enqueue(Entry(earlier, "routine_tick")));
        var request = Preview(scheduler);
        var legal = new HashSet<string>(["safe_idle"], StringComparer.Ordinal);
        var prepared = DeferredPlanAdmission.Prepare(request, PlannedReply(request), legal,
            _ => throw new InvalidOperationException("An unplanned choice has no plan check."), false);
        Assert.Same(legal, prepared.LegalCandidateIds);
        Assert.Null(prepared.Refusal);
        Assert.Null(prepared.Complete(scheduler.CompleteDeferred(request, PlannedReply(request), null, 0, prepared.LegalCandidateIds)));
        scheduler = CreateScheduler();
        var plan = new OfferedPlan(Actor, "planned_supply");
        earlier = earlier with { Candidates = [earlier.Candidates[0], earlier.Candidates[1] with { ActionPlan = plan }] };
        Assert.True(scheduler.Enqueue(Entry(earlier, "routine_tick")));
        request = Preview(scheduler) with { ProviderEpoch = 2 };
        prepared = DeferredPlanAdmission.Prepare(request, PlannedReply(request), legal,
            _ => ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected supplies are gone"), false);
        Assert.Null(prepared.Complete(scheduler.CompleteDeferred(request, PlannedReply(request), null, 0, prepared.LegalCandidateIds)));
        Assert.Single(scheduler.ExportState().Queue);
        scheduler.Validate();
    }

    private static CognitionDecisionResponse PlannedReply(CognitionDecisionRequest request) => new(
        request.RequestId, Actor, DecisionProviderKind.LargeLanguageModel, request.ProviderEpoch,
        request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
        "planned_supply", 1, new Dictionary<string, double> { ["planned_supply"] = 1 });

    private sealed record OfferedPlan(string ActorId, string Candidate) : ActionPlan(ActorId, Candidate)
    {
        public override string Activity => "fetch supplies";
    }
}
