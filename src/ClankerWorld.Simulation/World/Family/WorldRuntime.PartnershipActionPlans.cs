using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PartnershipActionPlan? PlanPartnershipAction(string actor, PartnershipChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        if (choice.Kind == PartnershipChoiceKind.Propose)
            return agents.ContainsKey(choice.TargetId) ? new(actor, choice, choice.TargetId, null) : null;
        var relationship = Partnerships(actor).FirstOrDefault(item => item.Id == choice.TargetId);
        return relationship is null ? null : new(actor, choice,
            relationship.ProposerId == actor ? relationship.TargetId : relationship.ProposerId, relationship);
    }

    private ActionCheck CheckPartnershipActionPlan(PartnershipActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (plan.Choice.Kind == PartnershipChoiceKind.Propose)
            return agents.ContainsKey(plan.PartnerId) && CanProposePartnership(plan.Actor, plan.PartnerId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that person is no longer available for a partnership proposal");
        if (plan.Original is not { } original ||
            !RelationshipPlan.Matches(Partnerships(plan.Actor).FirstOrDefault(item => item.Id == original.Id), original))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected partnership or consent has changed");
        var allowed = plan.Choice.Kind switch
        {
            PartnershipChoiceKind.Accept => original.State == SocietyRelationshipState.Proposed && original.TargetId == plan.Actor &&
                IsAdult(original.ProposerId) && !CloseKin(plan.Actor, original.ProposerId),
            PartnershipChoiceKind.Refuse => original.State == SocietyRelationshipState.Proposed && original.TargetId == plan.Actor,
            _ => original.State is SocietyRelationshipState.Proposed or SocietyRelationshipState.Accepted &&
                !(original.State == SocietyRelationshipState.Proposed && original.TargetId == plan.Actor),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that partnership response is no longer available");
    }

    private void AddFamilyCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddFamilyCandidateOptions(options, actor);
        foreach (var candidate in options)
            if (PartnershipChoice.Decode(candidate.Id) is { } choice && PlanPartnershipAction(actor, choice) is { } plan &&
                CheckPartnershipActionPlan(plan).Allowed)
                candidates.Add(candidate with { ActionPlan = plan });
    }

    private void ApplyPartnershipActionPlan(PartnershipActionPlan plan)
    {
        if (CheckPartnershipActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompletePartnershipAction(plan);
    }
}
