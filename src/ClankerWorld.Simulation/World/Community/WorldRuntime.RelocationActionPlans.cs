namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private RelocationActionPlan? PlanRelocationAction(string actor) =>
        CommunitySystem.PlanRelocationAction(CommunityRelocationWorld, actor);

    private ActionCheck CheckRelocationActionPlan(RelocationActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckRelocationActionPlan(CommunityRelocationWorld, plan, policy);

}
