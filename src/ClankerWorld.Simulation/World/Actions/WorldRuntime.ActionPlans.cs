using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public ActionCheck CheckActionPlan(ActionPlan plan) => plan switch
    {
        ChildActionPlan child => CheckChildPlan(child),
        _ => throw new InvalidOperationException("An action plan has no registered check."),
    };

    private void RefuseChoice(ActionPlan plan, ActionRefusal refusal)
    {
        var name = society.Checkpoint.Agents.SingleOrDefault(person => person.Id == plan.Actor)?.Name ?? "Someone";
        AppendEvent("choice_refused", $"{name} didn't {ChildActionLabel(plan.CandidateId)}: {refusal.Explanation}.");
    }

    private Society.SocietyCognitionDispatchResult CheckAcceptedPlan(Society.SocietyCognitionDispatchResult decision)
    {
        if (decision.Admission.Accepted && decision.ActionPlan is { } plan &&
            CheckActionPlan(plan).Refusal is { } refusal)
            return decision with { PlanRefusal = refusal };
        return decision;
    }
}
