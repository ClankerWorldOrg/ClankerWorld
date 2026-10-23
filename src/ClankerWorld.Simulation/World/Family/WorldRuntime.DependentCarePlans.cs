using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private DependentCareActionPlan? PlanDependentCare(string actor, DependentCareChoice choice) =>
        FamilySystem.PlanDependentCare(FamilyCareWorld, actor, choice);

    private ActionCheck CheckDependentCarePlan(DependentCareActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        FamilySystem.CheckDependentCarePlan(FamilyCareWorld, plan, policy);

    private void AddDependentCareCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddDependentCareOptions(options, actor);
        foreach (var candidate in options)
        {
            if (DependentCareChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (DependentCareDefinition.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void ApplyDependentCarePlan(DependentCareActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteDependentCareChoice(plan);
    }
}
