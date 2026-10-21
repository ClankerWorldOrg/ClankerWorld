namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool ApplyCivicPlan(WorldRuntime world, ActionPlan plan, ActionContext context, ActionPhase phase)
    {
        world.townSystem.ApplyTownCivicCandidate(world.civicWorld, plan.Actor, plan.CandidateId, originalPlan: plan);
        return true;
    }
}
