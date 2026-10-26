using ClankerWorld.Simulation.Kernel;
using System.Globalization;

namespace ClankerWorld.Simulation.World;

public enum MarketActionFamily { Occupancy, Stock, Exchange }

public sealed record MarketActionRequest(MarketActionFamily Family, string CandidateId)
{
    public string Encode() => CandidateId;
    public static MarketActionRequest? Decode(string candidate)
    {
        foreach (var (prefix, family) in new[]
        {
            ("market_borrow:", MarketActionFamily.Occupancy), ("market_leave:", MarketActionFamily.Occupancy),
            ("market_load:", MarketActionFamily.Stock), ("market_deposit:", MarketActionFamily.Stock), ("market_collect:", MarketActionFamily.Stock),
            ("market_buy:", MarketActionFamily.Exchange), ("market_continue:", MarketActionFamily.Exchange), ("market_cancel:", MarketActionFamily.Exchange),
        })
            if (candidate.StartsWith(prefix, StringComparison.Ordinal)) return new(family, candidate);
        return null;
    }
}

public sealed record MarketOccupancyChoice(bool Leave, string MarketId, string StallId, string ActorId, string? OccupancyId, bool AtSite)
{
    public string Encode() => Leave ? MarketTradeRules.Identity("market_leave:", OccupancyId!) :
        MarketTradeRules.Identity("market_borrow:", MarketId, StallId, ActorId, AtSite ? "at-site" : "walking");
}

public sealed record MarketOccupancyActionPlan(string ActorId, MarketOccupancyChoice Choice, string TownId,
    string HouseholdId, string HallId, GridPoint MarketSite, int Slot, GridPoint Destination,
    MarketStallOccupancy? Occupancy, string? StockId, string? StockOwnerId, string? ItemKind,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Leave ? "leave that selected Market stall" : "borrow that selected Market stall";
}

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

public enum MarketStockKind { Load, Deposit, Collect }

public sealed record MarketStockChoice(MarketStockKind Kind, string MarketId, string StallId, string StockId,
    int Quantity, string? OccupancyId, bool AtSite)
{
    public string Encode() => Kind == MarketStockKind.Deposit
        ? MarketTradeRules.Identity("market_deposit:", OccupancyId!, StockId, Quantity.ToString(CultureInfo.InvariantCulture), AtSite ? "at-site" : "walking")
        : MarketTradeRules.Identity(Kind == MarketStockKind.Load ? "market_load:" : "market_collect:", MarketId, StallId,
            StockId, Quantity.ToString(CultureInfo.InvariantCulture), AtSite ? "at-site" : "walking");
}

public sealed record MarketStockActionPlan(string ActorId, MarketStockChoice Choice, string TownId, string? HouseholdId,
    GridPoint MarketSite, int Slot, string OwnerId, string ItemKind, GridPoint Target, int InteractionRange,
    string? CarrierId, string? StorageBuildingId, InventoryGroundPosition? GroundPosition,
    MarketStallOccupancy? Occupancy, IReadOnlyList<PlannedStockMember>? Family, IReadOnlyList<GridPoint> Route)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "move that selected Market stock";
}
