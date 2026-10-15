using ShopQuote = ClankerWorld.Simulation.World.MarketSystem.ShopQuote;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ShopBuyActionPlan? PlanShopBuy(string actor, PlacedBuilding building, ShopQuote? selected = null,
        ShopBuyKind? selectedKind = null) =>
        marketSystem.PlanShopBuy(MarketShopPlanningWorld, actor, building, selected, selectedKind);

    private ActionCheck CheckShopBuy(ShopBuyActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        marketSystem.CheckShopBuy(MarketShopPlanningWorld, plan, policy);

    private PlannedShopQuote? CurrentSelectedShopQuote(ShopBuyActionPlan plan) =>
        MarketSystem.CurrentSelectedShopQuote(MarketShopPlanningWorld, plan);

    private void ApplyShopBuy(ShopBuyActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedShopBuy(plan);
    }

    private void ApplyCheckedShopBuy(ShopBuyActionPlan plan) =>
        marketSystem.ApplyCheckedShopBuy(MarketShopPlanningWorld, plan);
}
