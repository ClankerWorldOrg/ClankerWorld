using System.Globalization;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

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
