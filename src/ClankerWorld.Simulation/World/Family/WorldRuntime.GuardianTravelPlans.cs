namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GuardianTravelActionPlan? PlanGuardianTravel(string actor, GuardianTravelChoice choice) =>
        FamilySystem.PlanGuardianTravel(FamilyGuardianWorld, actor, choice);

    private ActionCheck CheckGuardianTravelPlan(GuardianTravelActionPlan plan, ActorCapabilityPolicy? policy = null) =>
        FamilySystem.CheckGuardianTravelPlan(FamilyGuardianWorld, plan, policy);

    private void ApplyGuardianTravelPlan(GuardianTravelActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedGuardianTravel(plan);
    }

    private void ApplyCheckedGuardianTravel(GuardianTravelActionPlan plan) =>
        FamilySystem.ApplyCheckedGuardianTravel(FamilyGuardianWorld, plan);
}
