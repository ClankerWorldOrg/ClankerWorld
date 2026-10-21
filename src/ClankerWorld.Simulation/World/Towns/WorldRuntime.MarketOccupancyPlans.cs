using MarketChoice = ClankerWorld.Simulation.World.MarketSystem.MarketChoice;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private MarketOccupancyActionPlan? PlanMarketOccupancy(string actor, MarketChoice choice) =>
        MarketSystem.PlanMarketOccupancy(MarketStallWorld, actor, choice);

    private ActionCheck CheckMarketOccupancy(MarketOccupancyActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        MarketSystem.CheckMarketOccupancy(MarketStallWorld, plan, policy);

    private void ApplyCheckedMarketOccupancy(MarketOccupancyActionPlan plan) =>
        MarketSystem.ApplyCheckedMarketOccupancy(MarketStallWorld, plan);
}
