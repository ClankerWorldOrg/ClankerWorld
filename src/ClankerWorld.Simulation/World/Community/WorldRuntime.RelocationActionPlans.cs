namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private RelocationActionPlan? PlanRelocationAction(string actor) =>
        CommunitySystem.PlanRelocationAction(CommunityRelocationWorld, actor);

    private ActionCheck CheckRelocationActionPlan(RelocationActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckRelocationActionPlan(CommunityRelocationWorld, plan, policy);

    private void ApplyRelocationActionPlan(RelocationActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteRelocationAction(plan);
    }
}
