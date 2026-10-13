using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private string? ImmediateGuardianHousehold(string adult, string child)
    {
        var home = HouseholdFor(adult);
        var town = TownForResident(adult);
        return home is not null && home != HouseholdFor(child) && town is not null && town == TownForResident(child) &&
            CanFitGuardianHousehold(home, adult, child) ? home : null;
    }

    private DependentCareActionPlan? PlanDependentCare(string actor, DependentCareChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        SocietyRelationship? relationship = null;
        GuardianSearchState? search = null;
        var child = choice.TargetId;
        if (choice.Kind == DependentCareChoiceKind.Accept && agents.TryGetValue(child, out var dependent) && NeedsCaregiver(child))
        {
            search = dependent.GuardianSearch;
            if (search is null) return null;
        }
        else if (choice.Kind is DependentCareChoiceKind.Accept or DependentCareChoiceKind.Refuse or DependentCareChoiceKind.End)
        {
            relationship = society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Id == choice.TargetId && edge.Type == SocietyRelationshipType.Caregiver);
            if (relationship is null) return null;
            child = relationship.TargetId;
        }
        else if (choice.Kind == DependentCareChoiceKind.Primary)
            relationship = society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Caregiver &&
                edge.State == SocietyRelationshipState.Accepted && edge.ProposerId == actor && edge.TargetId == child);
        if (choice.Kind != DependentCareChoiceKind.End && !agents.ContainsKey(child)) return null;
        var home = HouseholdFor(actor);
        return new(actor, choice, relationship, home, agents.ContainsKey(child) ? HouseholdFor(child) : null,
            search?.StartedTick, search is not null ? ImmediateGuardianHousehold(actor, child) : null,
            TownForResident(actor), agents.ContainsKey(child) ? TownForResident(child) : null,
            search is not null && home is not null ? HouseholdBuildingWithTag(home, "house")?.InstanceId : null,
            society.Checkpoint.GetAgent(actor).DomesticFamilyUnitId, agents.ContainsKey(child) ? society.Checkpoint.GetAgent(child).DomesticFamilyUnitId : null);
    }

    private ActionCheck CheckDependentCarePlan(DependentCareActionPlan plan)
    {
        var adultAction = plan.AcceptsGuardianSearch || plan.Choice.Kind is DependentCareChoiceKind.Offer or DependentCareChoiceKind.Primary;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(adultAction ? ActionLifeStages.Adults : ActionLifeStages.NonInfants, AllowBoat: true,
                AllowUrgentFood: true, AllowUrgentWarmth: plan.Choice.Kind == DependentCareChoiceKind.End, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (plan.Relationship is { } original && !RelationshipPlan.Matches(society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Id == original.Id), original))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiving relationship has changed");
        var child = plan.Relationship?.TargetId ?? plan.Choice.TargetId;
        if (plan.Choice.Kind != DependentCareChoiceKind.End && (!agents.ContainsKey(child) ||
                HouseholdFor(plan.Actor) != plan.ActorHousehold || HouseholdFor(child) != plan.ChildHousehold ||
                society.Checkpoint.GetAgent(plan.Actor).DomesticFamilyUnitId != plan.ActorFamilyUnit ||
                society.Checkpoint.GetAgent(child).DomesticFamilyUnitId != plan.ChildFamilyUnit))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiver or child's household has changed");
        bool allowed;
        if (plan.AcceptsGuardianSearch)
            allowed = CanAcceptGuardian(plan.Actor, child) && agents[child].GuardianSearch?.StartedTick == plan.GuardianSearchStartedTick &&
                TownForResident(plan.Actor) == plan.ActorTown && TownForResident(child) == plan.ChildTown &&
                ImmediateGuardianHousehold(plan.Actor, child) == plan.DestinationHousehold &&
                (plan.ActorHousehold is null ? null : HouseholdBuildingWithTag(plan.ActorHousehold, "house")?.InstanceId) == plan.HouseId;
        else allowed = plan.Choice.Kind switch
        {
            DependentCareChoiceKind.Accept or DependentCareChoiceKind.Refuse => plan.Relationship is { State: SocietyRelationshipState.Proposed } proposal &&
                proposal.TargetId == plan.Actor && CareProposals().Any(edge => edge.Id == proposal.Id) && EligibleCaregiver(proposal.ProposerId, plan.Actor),
            DependentCareChoiceKind.End => plan.Relationship is { } edge && (edge.ProposerId == plan.Actor || edge.TargetId == plan.Actor) &&
                (edge.State == SocietyRelationshipState.Accepted || edge.State == SocietyRelationshipState.Proposed && edge.ProposerId == plan.Actor),
            DependentCareChoiceKind.Offer => CanOfferCare(plan.Actor, child),
            DependentCareChoiceKind.Primary => plan.Relationship is not null && CanAssumePrimaryCare(plan.Actor, child),
            _ => false,
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected caregiving responsibility is no longer available");
    }

    private void AddDependentCareCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var options = new List<CognitionCandidate>();
        AddDependentCareOptions(options, actor);
        foreach (var candidate in options)
        {
            if (DependentCareChoice.Decode(candidate.Id) is not { } choice) { candidates.Add(candidate); continue; }
            if (PlanDependentCare(actor, choice) is { } plan && CheckDependentCarePlan(plan).Allowed)
                candidates.Add(candidate with { ActionPlan = plan });
        }
    }

    private void ApplyDependentCarePlan(DependentCareActionPlan plan)
    {
        if (CheckDependentCarePlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteDependentCareChoice(plan);
    }
}
