using MarketChoice = ClankerWorld.Simulation.World.MarketSystem.MarketChoice;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckMarketStock(MarketStockActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        MarketSystem.CheckMarketStock(MarketStallWorld, plan, policy);

    private void ApplyMarketStock(MarketStockActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedMarketStock(plan);
    }

    private void ApplyCheckedMarketStock(MarketStockActionPlan plan) =>
        MarketSystem.ApplyCheckedMarketStock(MarketStallWorld, plan);
}
