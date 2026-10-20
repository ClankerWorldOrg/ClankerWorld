namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckMarketExchange(MarketExchangeActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        MarketSystem.CheckMarketExchange(MarketStallWorld, plan, policy);

    private void ApplyMarketExchange(MarketExchangeActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedMarketExchange(plan);
    }

    private void ApplyCheckedMarketExchange(MarketExchangeActionPlan plan) =>
        MarketSystem.ApplyCheckedMarketExchange(MarketStallWorld, plan);
}
