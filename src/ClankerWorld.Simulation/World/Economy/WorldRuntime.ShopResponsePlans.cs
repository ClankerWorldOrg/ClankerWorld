namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckShopResponse(ShopResponseActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        marketSystem.CheckShopResponse(MarketShopPlanningWorld, plan, policy);

    private void ApplyShopResponse(ShopResponseActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedShopResponse(plan);
    }

    private void ApplyCheckedShopResponse(ShopResponseActionPlan plan) =>
        marketSystem.ApplyCheckedShopResponse(MarketShopPlanningWorld, plan);
}
