namespace ClankerWorld.Simulation.World;

public enum ShopBuyKind { Quote, IngredientsTrip, MealTrip }

public sealed record ShopBuyChoice(string BuildingId)
{
    public const string Prefix = "shop_buy:";
    public string Encode() => Prefix + BuildingId;
    public static ShopBuyChoice? Decode(string candidate) => candidate.StartsWith(Prefix, StringComparison.Ordinal)
        ? new(candidate[Prefix.Length..]) : null;
}

public sealed record ShopBuyActionPlan(string ActorId, string BuildingId, string SellerHouseholdId,
    string DefinitionId, GridPoint Destination, ShopBuyKind Kind, PlannedShopQuote? Quote,
    IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, IdentityRules.PrefixedReference("shop_buy:", BuildingId))
{
    public override string Activity => "visit that selected shop or offer that exact quote";
}
