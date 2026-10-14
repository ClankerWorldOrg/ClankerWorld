using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record ShopResponseChoice(bool Cancel, string OfferId)
{
    public string Encode() => (Cancel ? "shop_cancel:" : "shop_continue:") + OfferId;
    public static ShopResponseChoice? Decode(string candidate) => candidate.StartsWith("shop_cancel:", StringComparison.Ordinal)
        ? new(true, candidate["shop_cancel:".Length..]) : candidate.StartsWith("shop_continue:", StringComparison.Ordinal)
            ? new(false, candidate["shop_continue:".Length..]) : null;
}

public sealed record ShopResponseActionPlan(string ActorId, bool Cancel, ShopTradeState Trade, DirectBarterOffer Offer)
    : ActionPlan(ActorId, (Cancel ? "shop_cancel:" : "shop_continue:") + Offer.Id)
{
    public override string Activity => "respond to that original shop offer";
}
