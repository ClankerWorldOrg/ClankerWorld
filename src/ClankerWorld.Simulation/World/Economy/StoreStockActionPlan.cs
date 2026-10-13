namespace ClankerWorld.Simulation.World;

public sealed record StoreStockActionPlan(string ActorId, string HouseholdId, string StoreId,
    GridPoint Destination, string StockId, string OwnerId, string ItemKind, int Quantity,
    bool DirectDelivery, GridPoint Target, int InteractionRange, IReadOnlyList<GridPoint> Route,
    IReadOnlyList<GridPoint> ReturnRoute) : ActionPlan(ActorId, "shop_stock_store")
{
    public override string Activity => "stock the household Store";
}
