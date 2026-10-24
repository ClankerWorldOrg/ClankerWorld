namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IGuardianOrdersWorld GuardianOrdersWorld => new GuardianOrdersAdapter(this);

    private sealed class GuardianOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IGuardianOrdersWorld
    {
        public bool NeedsCaregiver(string child) => FamilySystem.NeedsCaregiver(Runtime.FamilyCareWorld, child);
        public global::ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, global::ClankerWorld.Simulation.World.DependentCareChoice choice) => FamilySystem.PlanDependentCare(Runtime.FamilyCareWorld, actor, choice);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(global::ClankerWorld.Simulation.World.DependentCareActionPlan plan, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => FamilySystem.CheckDependentCarePlan(Runtime.FamilyCareWorld, plan, policy);
        public bool CanAcceptGuardian(string adult, string child) => FamilySystem.CanAcceptGuardian(Runtime.FamilyCareWorld, adult, child);
        public void ApplyDependentCareCandidate(string actor, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyDependentCareCandidate(actor, candidate, originalPlan);
        public string GuardianOrderEffectId(string adult, string child) => WorldRuntime.GuardianOrderEffectId(adult, child);
    }
}
