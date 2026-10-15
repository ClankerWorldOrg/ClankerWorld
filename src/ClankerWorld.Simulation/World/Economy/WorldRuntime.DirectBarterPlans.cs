namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckDirectBarter(DirectBarterActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        MarketSystem.CheckDirectBarter(MarketBarterWorld, plan, policy);

    private void ApplyDirectBarter(DirectBarterActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedDirectBarter(plan);
    }

    private void ApplyCheckedDirectBarter(DirectBarterActionPlan plan) =>
        MarketSystem.ApplyCheckedDirectBarter(MarketBarterWorld, plan);
}
