using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public enum MarketExchangeKind { Buy, Continue, Cancel }

public sealed record MarketExchangeChoice(MarketExchangeKind Kind, string OccupancyId, string ActorId,
    string GoodsId, string PaymentId, string? OfferId, string Mode)
{
    public string Encode() => Kind switch
    {
        MarketExchangeKind.Buy => MarketTradeRules.Identity("market_buy:", OccupancyId, ActorId, GoodsId, PaymentId, Mode),
        MarketExchangeKind.Cancel => MarketTradeRules.Identity("market_cancel:", OfferId!),
        _ => MarketTradeRules.Identity("market_continue:", OfferId!, Mode),
    };
}

public sealed record MarketExchangeActionPlan(string ActorId, MarketExchangeChoice Choice, string TownId,
    string MarketId, string StallId, GridPoint MarketSite, int Slot, GridPoint Destination,
    MarketStallOccupancy Occupancy, string GoodsOwnerId, string GoodsKind, string PaymentKind,
    MarketTradeState? Trade, DirectBarterOffer? Offer, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to that selected Market exchange";
}
