using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool MayProposeParenthood(string actor, string partner)
    {
        var continuityCouple = ContinuityCoupleOf(actor);
        return Partners(actor, partner) && FamilyResourcesReady(actor) &&
            !agents.Values.Any(person => ActiveParenthood(person.Parenthood) &&
                (person.AgentId == actor || person.AgentId == partner || person.Parenthood!.PartnerId == actor || person.Parenthood.PartnerId == partner)) &&
            (continuityCouple is not null || !society.Checkpoint.Relationships.Any(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.ProposerId == actor && edge.State == SocietyRelationshipState.Accepted && agents.ContainsKey(edge.TargetId) &&
                LifeStageRules.IsYoungChild(society.Checkpoint.GetAgent(edge.TargetId).AgeBand))) &&
            !agents.Values.Any(person => person.Parenthood is { } previous &&
                (person.AgentId == actor || previous.PartnerId == actor) && WorldTick - previous.LastTransitionTick < worldSystems.Config.TicksPerDay);
    }

    private ParenthoodActionPlan? PlanParenthoodAction(string actor, ParenthoodChoice choice)
    {
        var owner = choice.Kind == ParenthoodChoiceKind.Propose ? actor : choice.OwnerId;
        if (!agents.TryGetValue(owner, out var physical) || !agents.ContainsKey(actor)) return null;
        var original = physical.Parenthood;
        var partner = choice.Kind == ParenthoodChoiceKind.Propose ? choice.OwnerId : original?.PartnerId;
        if (partner is null || !agents.ContainsKey(partner)) return null;
        var caregiver = choice.Kind == ParenthoodChoiceKind.Propose ? actor : original?.PrimaryCaregiverId;
        var home = choice.Kind == ParenthoodChoiceKind.Propose ? HouseholdFor(actor) : original?.IntendedHouseholdId;
        if (choice.Kind == ParenthoodChoiceKind.Accept)
        {
            caregiver = choice.Selector == "initiator" ? owner : actor;
            home = choice.HouseholdId;
        }
        var partnership = society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Partnership &&
            edge.State == SocietyRelationshipState.Accepted && (edge.ProposerId == owner && edge.TargetId == partner || edge.ProposerId == partner && edge.TargetId == owner));
        return new(actor, choice, owner, partner, original, partnership, ContinuityCoupleFor(owner, partner), caregiver, home);
    }

    private ActionCheck CheckParenthoodActionPlan(ParenthoodActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ParenthoodCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (!agents.TryGetValue(plan.OwnerId, out var owner) || !agents.ContainsKey(plan.PartnerId) || owner.Parenthood != plan.Original ||
            plan.Partnership is null || !RelationshipPlan.Matches(society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Id == plan.Partnership.Id), plan.Partnership) ||
            ContinuityCoupleFor(plan.OwnerId, plan.PartnerId) != plan.Continuity)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected partnership, parenthood request or continuity deadline has changed");
        if (plan.Choice.Kind == ParenthoodChoiceKind.Propose)
            return plan.CaregiverId == plan.Actor && HouseholdFor(plan.Actor) == plan.IntendedHouseholdId && MayProposeParenthood(plan.Actor, plan.PartnerId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that couple or intended home is no longer ready for a parenthood request");
        if (plan.Original is not { } original || !ActiveParenthood(original) || plan.Actor != plan.OwnerId && plan.Actor != plan.PartnerId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected parenthood plan is no longer active");
        bool allowed;
        switch (plan.Choice.Kind)
        {
            case ParenthoodChoiceKind.Accept:
                allowed = plan.Actor == plan.PartnerId && original.Stage == "requested" && Partners(plan.OwnerId, plan.Actor) &&
                    plan.CaregiverId is { } caregiver && HouseholdFor(caregiver) == plan.IntendedHouseholdId && FamilyResourcesReady(caregiver) &&
                    ParenthoodCaregiverOptions(plan.OwnerId, plan.Actor).Any(option => option.Selector == plan.Choice.Selector &&
                        option.CaregiverId == caregiver && option.HouseholdId == plan.IntendedHouseholdId);
                break;
            case ParenthoodChoiceKind.Decline:
                allowed = original.Stage == "requested" && plan.Actor == plan.PartnerId && plan.Continuity is null;
                break;
            case ParenthoodChoiceKind.Cancel:
                allowed = !(original.Stage == "requested" && plan.Actor == plan.PartnerId) && plan.Continuity is null;
                break;
            default:
                allowed = plan.Continuity is { } rule && ContinuityAllowsPostponement(rule);
                break;
        }
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected parenthood choice is no longer available");
    }

    private void AddParenthoodCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var start = candidates.Count;
        AddParenthoodCandidateOptions(candidates, actor);
        for (var index = start; index < candidates.Count;)
        {
            var candidate = candidates[index];
            if (ParenthoodChoice.Decode(candidate.Id) is not { } choice) { index++; continue; }
            if (ParenthoodDefinition.Offer(this, actor, choice) is { } plan)
            {
                candidates[index] = candidate with { ActionPlan = plan };
                index++;
            }
            else candidates.RemoveAt(index);
        }
    }

    private void ApplyParenthoodActionPlan(ParenthoodActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteParenthoodAction(plan);
    }
}
