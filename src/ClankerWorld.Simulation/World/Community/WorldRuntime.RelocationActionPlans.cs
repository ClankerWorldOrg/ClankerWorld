namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private RelocationActionPlan? PlanRelocationAction(string actor)
    {
        if (!agents.ContainsKey(actor) || HouseholdFor(actor) is not { } household || !MayVolunteerToRelocate(actor) ||
            HouseForHousehold(household) is not { } house || RelocationSelection(household, actor) is not { } selection) return null;
        var kept = selection.Chosen.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = RelocationNotices(household).Where(item => !kept.Contains(item.Id))
            .Select(item => new PlannedRelocationNotice(item.Id, item.Notice)).ToArray();
        return new(actor, new(), household, house, selection.Chosen.ToArray(), replaced,
            replaced.Select(item => (long?)item.Notice.DeadlineTick).Min());
    }

    private ActionCheck CheckRelocationActionPlan(RelocationActionPlan plan)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || HouseForHousehold(plan.HouseholdId) != plan.House ||
            !MayVolunteerToRelocate(plan.Actor) || RelocationSelection(plan.HouseholdId, plan.Actor) is not { } selection ||
            !selection.Chosen.SequenceEqual(plan.Selected))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or adults who need to move have changed");
        var kept = plan.Selected.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var replaced = RelocationNotices(plan.HouseholdId).Where(item => !kept.Contains(item.Id))
            .Select(item => new PlannedRelocationNotice(item.Id, item.Notice));
        return replaced.SequenceEqual(plan.Replaced) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected move-out notices or inherited deadline have changed");
    }

    private void ApplyRelocationActionPlan(RelocationActionPlan plan)
    {
        if (CheckRelocationActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteRelocationAction(plan);
    }
}
