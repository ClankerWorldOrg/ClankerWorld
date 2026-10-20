using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PracticalCareActionPlan? PlanPracticalCare(string actor, PracticalCareChoice choice)
    {
        if (!agents.ContainsKey(actor) || !agents.TryGetValue(choice.DependentId, out var child)) return null;
        var relationship = society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.EffectiveTick <= WorldTick && edge.ProposerId == actor && edge.TargetId == choice.DependentId);
        var feed = choice.Kind == PracticalCareKind.Child && child.FullnessBasisPoints < NeedThresholds.FoodPickupFullness;
        FoodActionPlan? food = feed ? PlanFoodConsumption(actor) : null;
        var preparing = feed && food is null;
        if (preparing)
        {
            if (AvailableHouseholdServing(actor, agents[actor].Position, returnTo: child.Position, returnRange: ResourceInteractionRange) is { } serving)
                food = PlanSharedFood(actor, serving)! with { DependentId = child.AgentId, ReturnPosition = child.Position, ReturnRange = ResourceInteractionRange };
            else if (AvailableFoodSource(actor, agents[actor].Position) is { } source)
                food = PlanFoodResource(actor, IsWithinInteractionRange(agents[actor].Position, source.Position, ResourceInteractionRange), source);
        }
        return new(actor, choice, child.Position, relationship, relationship is null && NeedsCaregiver(child.AgentId), feed, preparing, food,
            Reach(actor, child.Position, ReachPurpose.PracticalCare, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckPracticalCarePlan(PracticalCareActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? FamilyCareCapabilities);
        if (!capability.Allowed) return capability;
        if (!agents.TryGetValue(plan.Choice.DependentId, out var child) || child.Position != plan.Position ||
            plan.Relationship is { } original && !RelationshipPlan.Matches(society.Checkpoint.Relationships.FirstOrDefault(edge => edge.Id == original.Id), original) ||
            plan.Orphan && !NeedsCaregiver(child.AgentId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected dependent, position or caregiving authority has changed");
        var eligible = plan.Choice.Kind == PracticalCareKind.Child
            ? ChildrenNeedingCare(plan.Actor).Any(person => person.AgentId == child.AgentId)
            : CanTendToIllDependent(plan.Actor, child.AgentId);
        if (!eligible || plan.Choice.Kind == PracticalCareKind.Child &&
            (child.FullnessBasisPoints < NeedThresholds.FoodPickupFullness) != plan.NeedsFeeding)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected dependent no longer needs that care");
        if (NeedsUrgentFood(agents[plan.Actor]) && (plan.PreparingFood || !IsWithinInteractionRange(agents[plan.Actor].Position, plan.Position, ResourceInteractionRange)))
            return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "finding food comes before travelling or preparing a child's serving");
        if (plan.PreparingFood && PreferredFood(plan.Actor, plan.Actor).Any())
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry a serving for the child");
        // An unavailable preparation source retains the native wait. No stock is
        // selected during application; a continuing intention plans its next step.
        return plan.Food is { } food ? CheckFoodPlan(food) : new();
    }

    private void ApplyPracticalCarePlan(PracticalCareActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedPracticalCare(plan);
    }

    private void ApplyCheckedPracticalCare(PracticalCareActionPlan plan)
    {
        if (plan.PreparingFood)
        {
            if (plan.Food is SharedFoodActionPlan shared) CompleteChildFoodPickup(plan.Actor, shared, plan.Position);
            else if (plan.Food is FoodResourceActionPlan gathering)
            {
                if (gathering.Selected.Kind == FoodActionKind.Harvest)
                    CompleteFoodHarvest(plan.Actor, gathering.Source, gathering.ItemKind, gathering.Quantity, gathering.SeedQuantity);
                else MoveToward(plan.Actor, agents[plan.Actor], gathering.Source.Position, "care_food", ResourceInteractionRange);
            }
            return;
        }
        if (!IsWithinInteractionRange(agents[plan.Actor].Position, plan.Position, ResourceInteractionRange))
        { MoveToward(plan.Actor, agents[plan.Actor], plan.Position, "care", ResourceInteractionRange); return; }
        if (plan.Choice.Kind == PracticalCareKind.Child) CompleteChildCare(plan);
        else CompleteIllDependentCare(plan.Actor, plan.Choice.DependentId);
    }
}
