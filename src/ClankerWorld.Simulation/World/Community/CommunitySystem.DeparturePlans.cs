using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static DepartureActionPlan? PlanDepartureAction(ICommunityDepartureWorld world, string actor, DepartureChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor)) return null;
        var household = world.Society.GetAgent(actor).HouseholdId;
        return new(actor, choice, household, world.MayRelocateFromHousehold(actor) ? "displaced" : "voluntary",
            world.MovingCareGroup(actor).ToArray(), household is null ? [] : world.PlanDepartureAllowance(actor, household));
    }

    internal static ActionCheck CheckDepartureActionPlan(ICommunityDepartureWorld world, DepartureActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Society.GetAgent(plan.Actor).HouseholdId != plan.HouseholdId ||
            !world.MovingCareGroup(plan.Actor).SequenceEqual(plan.CareGroup, StringComparer.Ordinal) ||
            plan.HouseholdId is not null && plan.CareGroup.Any(id => world.Society.GetAgent(id).HouseholdId != plan.HouseholdId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their household or moving care group has changed");
        if ((world.MayRelocateFromHousehold(plan.Actor) ? "displaced" : "voluntary") != plan.Cause ||
            plan.Cause == "displaced" && (world.HasDependentInPrimaryCare(plan.Actor) || world.HoldsRelocationNotice(plan.Actor) && !world.MayRelocateFromHousehold(plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected departure is no longer needed or permitted");
        if (plan.Choice.Kind == DepartureChoiceKind.Leave && plan.HouseholdId is null ||
            plan.Choice.Kind == DepartureChoiceKind.Found && (world.PhysicalAgents.Living[plan.Actor].Housing?.Request is not null ||
                !world.MayFoundHousehold(plan.Actor) || world.AskableHouseholds(plan.Actor).Any()))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that household choice is no longer available");
        if (plan.HouseholdId is { } household)
            foreach (var selected in plan.Allowance)
            {
                var current = world.RecheckGoods(new GoodsRequest(GoodsUse.DepartureAllowance, plan.Actor, GoodsOwners.One(household), world.EdibleFoodGoods), selected.Stock.Id);
                if (current is null || current.Quantity < selected.Quantity ||
                    current.Lot.OwnerId != selected.Stock.OwnerId || current.Lot.ItemKind != selected.Stock.ItemKind ||
                    current.Lot.StorageBuildingId != selected.Stock.StorageBuildingId || current.Lot.GroundPosition != selected.Stock.GroundPosition ||
                    current.Lot.CarrierId != selected.Stock.CarrierId || current.Lot.ContainerLotId != selected.Stock.ContainerLotId)
                    return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected departure food allowance has changed");
            }
        return new();
    }

    internal static ReplacementCareActionPlan? PlanReplacementCare(ICommunityDepartureWorld world, string actor, ReplacementCareChoice choice)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || !world.PhysicalAgents.Living.ContainsKey(choice.ChildId) || world.HouseholdFor(actor) is not { } household ||
            world.Society.GetAgent(choice.ChildId).PrimaryCaregiverId is not { } caregiver) return null;
        var care = world.Society.Relationships.FirstOrDefault(edge => edge.Type == SocietyRelationshipType.Caregiver &&
            edge.State == SocietyRelationshipState.Accepted && edge.ProposerId == caregiver && edge.TargetId == choice.ChildId);
        return care is null ? null : new(actor, choice, household, caregiver, care);
    }

    internal static ActionCheck CheckReplacementCarePlan(ICommunityDepartureWorld world, ReplacementCareActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (!world.PhysicalAgents.Living.ContainsKey(plan.Choice.ChildId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected child is no longer here");
        var child = world.Society.GetAgent(plan.Choice.ChildId);
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || child.HouseholdId != plan.HouseholdId ||
            !LifeStageRules.IsDependent(child.AgeBand) || child.PrimaryCaregiverId != plan.OriginalCaregiverId ||
            child.PrimaryCaregiverId == plan.Actor || !SocietyRules.HasActivePrimaryCaregiver(world.Society, child.Id) ||
            !RelationshipPlan.Matches(world.Society.Relationships.FirstOrDefault(edge => edge.Id == plan.OriginalCare.Id), plan.OriginalCare))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household or child's primary care has changed");
        return new();
    }

    internal static void CompleteDepartureAction(ICommunityDepartureWorld world, DepartureActionPlan plan)
    {
        if (plan.HouseholdId is not null && !DepartHousehold(world, plan.Actor, plan.Cause, plan)) return;
        if (plan.Choice.Kind != DepartureChoiceKind.Found) return;
        var householdId = world.SoloHouseholdId(plan.Actor, world.WorldTick);
        world.CreateHousehold(householdId, world.Society.GetAgent(plan.Actor).Name + "'s household", plan.CareGroup);
        foreach (var id in plan.CareGroup)
            world.PhysicalAgents.FinishHouseholdDeparture(id, world.PhysicalAgents.Living[id], new(Blocker: world.HousingBlocker(id)), null);
        world.AppendEvent(new HouseholdFoundedEventPayload(plan.Actor, householdId));
    }

    internal static void CompleteReplacementCare(ICommunityDepartureWorld world, ReplacementCareActionPlan plan)
    {
        if (world.AcceptReplacementCare(plan.Actor, plan.Choice.ChildId)) world.AppendEvent(new ReplacementCareAcceptedEventPayload(plan.Actor, plan.Choice.ChildId));
    }
}
