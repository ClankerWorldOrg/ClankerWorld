namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckMarketExchange(MarketExchangeActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        MarketSystem.CheckMarketExchange(MarketStallWorld, plan, policy);

    private void ApplyCheckedMarketExchange(MarketExchangeActionPlan plan) =>
        MarketSystem.ApplyCheckedMarketExchange(MarketStallWorld, plan);
}
