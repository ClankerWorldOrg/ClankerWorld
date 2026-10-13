using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed record DeferredPlanAdmission(IReadOnlySet<string> LegalCandidateIds, ActionRefusal? Refusal)
{
    internal static DeferredPlanAdmission Prepare(CognitionDecisionRequest request, CognitionDecisionResponse? response,
        IReadOnlySet<string> legalCandidates, Func<ActionPlan, ActionCheck> check, bool activeOrder)
    {
        if (response is null || request.Observation.Candidates.SingleOrDefault(candidate =>
                candidate.Id == response.SelectedCandidateId)?.ActionPlan is not { } plan)
            return new(legalCandidates, null);
        var refusal = check(plan).Refusal;
        if (legalCandidates.Contains(response.SelectedCandidateId)) return new(legalCandidates, refusal);
        // The original offered plan must reach its check/refusal. It never
        // executes when the host's current context has excluded its identity.
        refusal ??= activeOrder
            ? new(ActionRefusalReason.ActiveOrder, "their current order comes first")
            : new(ActionRefusalReason.TargetUnavailable, "this activity is no longer available");
        var admitted = new HashSet<string>(legalCandidates, StringComparer.Ordinal) { response.SelectedCandidateId };
        return new(admitted, refusal);
    }

    internal SocietyCognitionDispatchResult? Complete(SocietyCognitionDispatchResult? decision) =>
        decision is { Admission.Accepted: true, ActionPlan: not null } && Refusal is not null
            ? decision with { PlanRefusal = Refusal } : decision;
}
