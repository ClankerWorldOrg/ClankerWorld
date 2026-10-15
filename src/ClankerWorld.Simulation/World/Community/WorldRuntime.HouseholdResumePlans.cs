using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private HouseholdResumeActionPlan? PlanHouseholdResume(string actor, HouseholdResumeChoice choice) =>
        CommunitySystem.PlanHouseholdResume(CommunityWorkWorld, actor, choice);

    private ActionCheck CheckHouseholdResumePlan(HouseholdResumeActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        CommunitySystem.CheckHouseholdResumePlan(CommunityWorkWorld, plan, policy);

    private void ApplyHouseholdResumePlan(HouseholdResumeActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        CompleteHouseholdResume(plan);
    }
}
