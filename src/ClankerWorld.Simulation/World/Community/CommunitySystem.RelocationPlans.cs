namespace ClankerWorld.Simulation.World;

internal sealed partial class CommunitySystem
{
    internal static RelocationActionPlan? PlanRelocationAction(ICommunityRelocationWorld world, string actor)
    {
        if (!world.PhysicalAgents.Living.ContainsKey(actor) || world.HouseholdFor(actor) is not { } household || !MayVolunteerToRelocate(world, actor) ||
            world.HouseForHousehold(household) is not { } house || RelocationSelection(world, household, actor) is not { } selection) return null;
        var kept = selection.Chosen.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = RelocationNotices(world, household).Where(item => !kept.Contains(item.Id))
            .Select(item => new PlannedRelocationNotice(item.Id, item.Notice)).ToArray();
        return new(actor, new(), household, house, selection.Chosen.ToArray(), replaced,
            replaced.Select(item => (long?)item.Notice.DeadlineTick).Min());
    }

    internal static ActionCheck CheckRelocationActionPlan(ICommunityRelocationWorld world, RelocationActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? HouseholdCommunityCapabilities);
        if (!capability.Allowed) return capability;
        if (world.HouseholdFor(plan.Actor) != plan.HouseholdId || world.HouseForHousehold(plan.HouseholdId) != plan.House ||
            !MayVolunteerToRelocate(world, plan.Actor) || RelocationSelection(world, plan.HouseholdId, plan.Actor) is not { } selection ||
            !selection.Chosen.SequenceEqual(plan.Selected))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or adults who need to move have changed");
        var kept = plan.Selected.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = RelocationNotices(world, plan.HouseholdId).Where(item => !kept.Contains(item.Id))
            .Select(item => new PlannedRelocationNotice(item.Id, item.Notice));
        return replaced.SequenceEqual(plan.Replaced) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected move-out notices or inherited deadline have changed");
    }
}
