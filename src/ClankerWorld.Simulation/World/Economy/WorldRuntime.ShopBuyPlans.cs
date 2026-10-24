using ShopQuote = ClankerWorld.Simulation.World.MarketSystem.ShopQuote;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void ApplyShopBuy(ShopBuyActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        this.marketSystem.ApplyCheckedShopBuy(this.MarketShopPlanningWorld, plan);
    }
}
