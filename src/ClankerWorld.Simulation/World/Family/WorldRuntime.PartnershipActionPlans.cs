using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PartnershipActionPlan? PlanPartnershipAction(string actor, PartnershipChoice choice) =>
        FamilySystem.PlanPartnershipAction(FamilyPartnershipWorld, actor, choice);

    private ActionCheck CheckPartnershipActionPlan(PartnershipActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        FamilySystem.CheckPartnershipActionPlan(FamilyPartnershipWorld, plan, policy);

    private void AddFamilyCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddFamilyCandidateOptions(options, actor);
        foreach (var candidate in options)
            if (PartnershipChoice.Decode(candidate.Id) is { } choice && PartnershipDefinition.Offer(this, actor, choice) is { } plan)
                candidates.Add(candidate with { ActionPlan = plan });
    }

    private void ApplyPartnershipActionPlan(PartnershipActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompletePartnershipAction(plan);
    }
}
