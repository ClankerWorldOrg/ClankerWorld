namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GuardianTravelActionPlan? PlanGuardianTravel(string actor, GuardianTravelChoice choice)
    {
        if (!agents.ContainsKey(actor)) return null;
        var child = choice.Kind == GuardianTravelKind.Relocate ? choice.TargetId : actor;
        if (!agents.TryGetValue(child, out var dependent) || dependent.GuardianPlacement is not { } placement ||
            !agents.ContainsKey(placement.CaregiverId)) return null;
        var target = choice.Kind == GuardianTravelKind.Relocate
            ? placement.Stage == "collecting" ? dependent.Position : placement.HousePosition ?? dependent.Position
            : agents[placement.CaregiverId].Position;
        var range = choice.Kind == GuardianTravelKind.Relocate
            ? placement.Stage == "collecting" ? ResourceInteractionRange : 0
            : target == placement.HousePosition ? 0 : ResourceInteractionRange;
        return new(actor, choice, child, placement, Reach(actor, target, ReachPurpose.GuardianTravel, range, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckGuardianTravelPlan(GuardianTravelActionPlan plan)
    {
        var following = plan.Choice.Kind == GuardianTravelKind.Follow;
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(following ? ActionLifeStages.Dependents : ActionLifeStages.Adults, AllowBoat: true,
                AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: false));
        if (!capability.Allowed) return capability;
        if (!agents.TryGetValue(plan.ChildId, out var child) || child.GuardianPlacement is not { } current ||
            current with { Stage = plan.Placement.Stage, Blocker = plan.Placement.Blocker } != plan.Placement ||
            !IsCurrentGuardianPlacement(plan.ChildId, current))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected child's guardian, care receipt or home has changed");
        if (GuardianPlacementHomeBlocker(plan.ChildId, current) is { } homeBlocker)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, homeBlocker);
        if (PendingInstructionFor(plan.Actor) is not null || IsConversationBusy(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.ActiveOrder, "they need to finish the other activity first");
        if (!following)
            return current.CaregiverId == plan.Actor && GuardianPlacementChildFor(plan.Actor) == plan.ChildId
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "another accepted child has the earlier movement turn");
        if (plan.Actor != plan.ChildId || plan.Choice.TargetId != current.CaregiverId || current.Stage != "escorting" ||
            PendingInstructionFor(current.CaregiverId) is not null || IsConversationBusy(current.CaregiverId))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected guardian is not ready for this escort");
        var adult = agents[current.CaregiverId];
        return !NeedsUrgentFood(adult) && !NeedsUrgentWarmth(adult) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.UrgentNeed, "the guardian is meeting urgent food or warmth needs");
    }

    private void ApplyGuardianTravelPlan(GuardianTravelActionPlan plan)
    {
        if (CheckGuardianTravelPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        // The selected people and home stay bound while collection/escort phases
        // advance. Following reaches the same guardian's current physical position.
        if (plan.Choice.Kind == GuardianTravelKind.Relocate) EscortGuardianChild(plan.Actor, plan.ChildId);
        else FollowPlacementGuardian(plan.ChildId, plan);
    }
}
