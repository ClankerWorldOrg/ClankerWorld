namespace ClankerWorld.Simulation.World;

public enum ShopBuyKind { Quote, IngredientsTrip, MealTrip }

public sealed record ShopBuyActionPlan(string ActorId, string BuildingId, string SellerHouseholdId,
    string DefinitionId, GridPoint Destination, ShopBuyKind Kind, PlannedShopQuote? Quote,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, "shop_buy:" + BuildingId)
{
    public override string Activity => "visit that selected shop or offer that exact quote";
}
