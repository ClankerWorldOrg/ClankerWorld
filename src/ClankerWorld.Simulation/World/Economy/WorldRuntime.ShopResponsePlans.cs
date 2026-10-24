namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void ApplyShopResponse(ShopResponseActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        this.marketSystem.ApplyCheckedShopResponse(this.MarketShopPlanningWorld, plan);
    }
}
