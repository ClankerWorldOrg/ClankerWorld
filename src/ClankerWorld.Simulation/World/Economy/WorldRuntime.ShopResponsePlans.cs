namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckShopResponse(ShopResponseActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ShopCapabilities);
        if (!capability.Allowed) return capability;
        if (shopTrades.SingleOrDefault(trade => trade.OfferId == plan.Offer.Id) != plan.Trade ||
            plan.Actor != plan.Trade.BuyerId && !MayRespondAtShop(plan.Actor, plan.Trade) || !ShopResponseOfferIsCurrent(plan))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the original shop offer or their personal response authority has changed");
        return new();
    }

    private void ApplyShopResponse(ShopResponseActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedShopResponse(plan);
    }

    private void ApplyCheckedShopResponse(ShopResponseActionPlan plan)
    {
        ApplyRetainedShopResponse(plan.Actor, agents[plan.Actor], plan);
    }
}
