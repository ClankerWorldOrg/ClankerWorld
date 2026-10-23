
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ReplacementCareActionPlan? PlanReplacementCare(string actor, ReplacementCareChoice choice) =>
        CommunitySystem.PlanReplacementCare(CommunityDepartureWorld, actor, choice);

    private ActionCheck CheckReplacementCarePlan(ReplacementCareActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckReplacementCarePlan(CommunityDepartureWorld, plan, policy);

}
