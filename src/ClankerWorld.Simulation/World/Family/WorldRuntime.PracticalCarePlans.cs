
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PracticalCareActionPlan? PlanPracticalCare(string actor, PracticalCareChoice choice) =>
        FamilySystem.PlanPracticalCare(FamilyPracticalCareWorld, actor, choice);

    private ActionCheck CheckPracticalCarePlan(PracticalCareActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        FamilySystem.CheckPracticalCarePlan(FamilyPracticalCareWorld, plan, policy);

    private void ApplyPracticalCarePlan(PracticalCareActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedPracticalCare(plan);
    }

    private void ApplyCheckedPracticalCare(PracticalCareActionPlan plan) =>
        FamilySystem.ApplyCheckedPracticalCare(FamilyPracticalCareWorld, plan);
}
