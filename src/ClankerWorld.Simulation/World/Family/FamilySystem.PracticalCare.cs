using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class FamilySystem
{
    internal static PracticalCareActionPlan? PlanPracticalCare(IFamilyPracticalCareWorld world, string actor, PracticalCareChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || !world.PhysicalAgents.Living.TryGetValue(choice.DependentId, out var child)) return null;
        var relationship = world.Society.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.EffectiveTick <= world.WorldTick && edge.ProposerId == actor && edge.TargetId == choice.DependentId);
        var feed = choice.Kind == PracticalCareKind.Child && child.FullnessBasisPoints < NeedThresholds.FoodPickupFullness;
        FoodActionPlan? food = feed ? world.PlanFoodConsumption(actor) : null;
        var preparing = feed && food is null;
        if (preparing)
        {
            if (world.AvailableHouseholdServing(actor, world.PhysicalAgents.Living[actor].Position, returnTo: child.Position, returnRange: world.ResourceInteractionRange) is { } serving)
                food = world.PlanSharedFood(actor, serving)! with { DependentId = child.AgentId, ReturnPosition = child.Position, ReturnRange = world.ResourceInteractionRange };
            else if (world.AvailableFoodSource(actor, world.PhysicalAgents.Living[actor].Position) is { } source)
                food = world.PlanFoodResource(actor, world.IsWithinInteractionRange(world.PhysicalAgents.Living[actor].Position, source.Position, world.ResourceInteractionRange), source);
        }
        return new(actor, choice, child.Position, relationship, relationship is null && NeedsCaregiver(world.CareWorld, child.AgentId), feed, preparing, food,
            world.Reach(actor, child.Position, ReachPurpose.PracticalCare, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckPracticalCarePlan(IFamilyPracticalCareWorld world, PracticalCareActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? FamilyCareCapabilities);
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.TryGetValue(plan.Choice.DependentId, out var child) || child.Position != plan.Position ||
            plan.Relationship is { } original && !RelationshipPlan.Matches(world.Society.Relationships.FirstOrDefault(edge => edge.Id == original.Id), original) ||
            plan.Orphan && !NeedsCaregiver(world.CareWorld, child.AgentId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected dependent, position or caregiving authority has changed");
        var eligible = plan.Choice.Kind == PracticalCareKind.Child
            ? ChildrenNeedingCare(world.ParenthoodWorld, plan.Actor).Any(person => person.AgentId == child.AgentId)
            : CanTendToIllDependent(world.CareWorld, plan.Actor, child.AgentId);
        if (!eligible || plan.Choice.Kind == PracticalCareKind.Child &&
            (child.FullnessBasisPoints < NeedThresholds.FoodPickupFullness) != plan.NeedsFeeding)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected dependent no longer needs that care");
        if (world.NeedsUrgentFood(world.PhysicalAgents.Living[plan.Actor]) && (plan.PreparingFood || !world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Position, world.ResourceInteractionRange)))
            return ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "finding food comes before travelling or preparing a child's serving");
        if (plan.PreparingFood && world.PreferredFood(plan.Actor, plan.Actor).Any())
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they already carry a serving for the child");
        // An unavailable preparation source retains the native wait. No stock is
        // selected during application; a continuing intention plans its next step.
        return plan.Food is { } food ? world.CheckFoodPlan(food) : new();
    }

    internal static void ApplyCheckedPracticalCare(IFamilyPracticalCareWorld world, PracticalCareActionPlan plan)
    {
        if (plan.PreparingFood)
        {
            if (plan.Food is SharedFoodActionPlan shared) world.CompleteChildFoodPickup(plan.Actor, shared, plan.Position);
            else if (plan.Food is FoodResourceActionPlan gathering)
            {
                if (gathering.Selected.Kind == FoodActionKind.Harvest)
                    world.CompleteFoodHarvest(plan.Actor, gathering.Source, gathering.ItemKind, gathering.Quantity, gathering.SeedQuantity);
                else world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], gathering.Source.Position, "care_food", world.ResourceInteractionRange);
            }
            return;
        }
        if (!world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Position, world.ResourceInteractionRange))
        { world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.Position, "care", world.ResourceInteractionRange); return; }
        if (plan.Choice.Kind == PracticalCareKind.Child) CompleteChildCare(world.ParenthoodWorld, plan);
        else CompleteIllDependentCare(world.CareWorld, plan.Actor, plan.Choice.DependentId);
    }
}
