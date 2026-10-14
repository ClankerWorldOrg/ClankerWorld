namespace ClankerWorld.Simulation.World;

public readonly record struct StoreStockChoice
{
    public const string Candidate = "shop_stock_store";
    public static string Encode() => Candidate;
    public static bool TryDecode(string candidate, out StoreStockChoice choice)
    {
        choice = new();
        return candidate == Candidate;
    }
}

public sealed record StoreStockActionPlan(string ActorId, string HouseholdId, string StoreId,
    GridPoint Destination, string StockId, string OwnerId, string ItemKind, int Quantity,
    bool DirectDelivery, GridPoint Target, int InteractionRange, IReadOnlyList<GridPoint> Route,
    IReadOnlyList<GridPoint> ReturnRoute) : ActionPlan(ActorId, StoreStockChoice.Candidate)
{
    public override string Activity => "stock the household Store";
}
