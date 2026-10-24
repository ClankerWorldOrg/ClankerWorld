
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private PracticalCareActionPlan? PlanPracticalCare(string actor, PracticalCareChoice choice) =>
        FamilySystem.PlanPracticalCare(FamilyPracticalCareWorld, actor, choice);

    private void ApplyPracticalCarePlan(PracticalCareActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        FamilySystem.ApplyCheckedPracticalCare(this.FamilyPracticalCareWorld, plan);
    }
}
