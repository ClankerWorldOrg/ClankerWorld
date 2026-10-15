using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool IsAdult(string actor) => AgentQueries.IsAdult(agentWorld, actor);

    private IEnumerable<SocietyRelationship> Partnerships(string actor) =>
        FamilySystem.Partnerships(FamilyPartnershipWorld, actor);

    private bool AvailablePartner(string actor) =>
        FamilySystem.AvailablePartner(FamilyPartnershipWorld, actor);

    private bool HasFamilyDecision(string actor) =>
        FamilySystem.HasFamilyDecision(FamilyPartnershipWorld, actor);

    // Close relatives cannot become partners or plan a child: parents and children, grandparents and grandchildren
    // at any depth, full and half siblings, and aunts or uncles with their nieces or nephews. Only biological
    // parentage counts, so first cousins, shared households and caregivers do not.
    private bool CloseKin(string actor, string other) =>
        FamilySystem.CloseKin(FamilyPartnershipWorld, actor, other);

    private bool CanProposePartnership(string actor, string other) =>
        FamilySystem.CanProposePartnership(FamilyPartnershipWorld, actor, other);

    private void MaintainPartnerships() =>
        FamilySystem.MaintainPartnerships(FamilyPartnershipWorld);

    private void AddFamilyCandidateOptions(List<CognitionCandidate> candidates, string actor) =>
        FamilySystem.AddFamilyCandidateOptions(FamilyPartnershipWorld, candidates, actor);

    private void ApplyFamilyCandidate(string actor, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as PartnershipActionPlan ?? (PartnershipChoice.Decode(candidate) is { } choice ? PlanPartnershipAction(actor, choice) : null);
        if (plan is not null) ApplyPartnershipActionPlan(plan);
    }

    private void CompletePartnershipAction(PartnershipActionPlan plan) =>
        FamilySystem.CompletePartnershipAction(FamilyPartnershipWorld, plan);
}
