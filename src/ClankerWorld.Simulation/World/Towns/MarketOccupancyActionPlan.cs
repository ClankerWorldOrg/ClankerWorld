namespace ClankerWorld.Simulation.World;

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
