using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ReplacementCareActionPlan? PlanReplacementCare(string actor, ReplacementCareChoice choice)
    {
        if (!agents.ContainsKey(actor) || !agents.ContainsKey(choice.ChildId) || HouseholdFor(actor) is not { } household ||
            society.Checkpoint.GetAgent(choice.ChildId).PrimaryCaregiverId is not { } caregiver) return null;
        var care = society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.ProposerId == caregiver && edge.TargetId == choice.ChildId);
        return care is null ? null : new(actor, choice, household, caregiver, care);
    }

    private ActionCheck CheckReplacementCarePlan(ReplacementCareActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (!agents.ContainsKey(plan.Choice.ChildId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected child is no longer here");
        var child = society.Checkpoint.GetAgent(plan.Choice.ChildId);
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || child.HouseholdId != plan.HouseholdId ||
            !LifeStageRules.IsDependent(child.AgeBand) || child.PrimaryCaregiverId != plan.OriginalCaregiverId ||
            child.PrimaryCaregiverId == plan.Actor || !SocietyRules.HasActivePrimaryCaregiver(society.Checkpoint, child.Id) ||
            !RelationshipPlan.Matches(society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Id == plan.OriginalCare.Id), plan.OriginalCare))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household or child's primary care has changed");
        return new();
    }

    private void ApplyReplacementCarePlan(ReplacementCareActionPlan plan)
    {
        if (CheckReplacementCarePlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteReplacementCare(plan);
    }
}
