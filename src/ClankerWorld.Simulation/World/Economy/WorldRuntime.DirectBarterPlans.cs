namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckDirectBarter(DirectBarterActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? PersonalBarterCapabilities);
        if (!capability.Allowed) return capability;
        if (!agents.ContainsKey(plan.OtherId) || !IsAdult(plan.OtherId) || SharedStoragePosition != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected trader or meeting place has changed");
        if (plan.Choice.Kind == DirectBarterKind.Propose)
        {
            if (TradeOpportunity(plan.Actor, plan.OtherId, plan.GiveId, plan.TakeId) is not { } trade ||
                trade.Give.ItemKind != plan.GiveKind || trade.Take.ItemKind != plan.TakeKind)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained barter goods or reciprocal demand are unavailable");
        }
        else if (!DirectBarterOfferIsCurrent(plan))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected personal offer or its response state has changed");
        if (plan.Choice.Kind == DirectBarterKind.Decline) return new();
        return IsWithinInteractionRange(agents[plan.Actor].Position, plan.Destination, ResourceInteractionRange) ||
            Reach(plan.Actor, plan.Destination, ReachPurpose.DirectBarter, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0
            ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the retained personal barter meeting is out of reach");
    }

    private void ApplyDirectBarter(DirectBarterActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedDirectBarter(plan);
    }

    private void ApplyCheckedDirectBarter(DirectBarterActionPlan plan)
    {
        ApplyRetainedTradeCandidate(plan.Actor, agents[plan.Actor], plan);
    }
}
