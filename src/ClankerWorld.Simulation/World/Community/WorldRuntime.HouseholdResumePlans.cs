
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HouseholdResumeActionPlan? PlanHouseholdResume(string actor, HouseholdResumeChoice choice) =>
        CommunitySystem.PlanHouseholdResume(CommunityWorkWorld, actor, choice);

    private ActionCheck CheckHouseholdResumePlan(HouseholdResumeActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckHouseholdResumePlan(CommunityWorkWorld, plan, policy);

}
