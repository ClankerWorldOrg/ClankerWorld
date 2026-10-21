namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckDirectBarter(DirectBarterActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        MarketSystem.CheckDirectBarter(MarketBarterWorld, plan, policy);

    private void ApplyCheckedDirectBarter(DirectBarterActionPlan plan) =>
        MarketSystem.ApplyCheckedDirectBarter(MarketBarterWorld, plan);
}
