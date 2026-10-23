namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private void ApplyGuardianTravelPlan(GuardianTravelActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        FamilySystem.ApplyCheckedGuardianTravel(this.FamilyGuardianWorld, plan);
    }
}
