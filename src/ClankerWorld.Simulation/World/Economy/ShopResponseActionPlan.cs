using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record ShopResponseActionPlan(string ActorId, bool Cancel, ShopTradeState Trade, DirectBarterOffer Offer)
    : ActionPlan(ActorId, (Cancel ? "shop_cancel:" : "shop_continue:") + Offer.Id)
{
    public override string Activity => "respond to that original shop offer";
}
