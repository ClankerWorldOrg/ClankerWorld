namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IGuardianOrdersWorld GuardianOrdersWorld => new GuardianOrdersAdapter(this);

    private sealed class GuardianOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IGuardianOrdersWorld
    {
        public bool NeedsCaregiver(string child) => Runtime.NeedsCaregiver(child);
        public global::ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, global::ClankerWorld.Simulation.World.DependentCareChoice choice) => Runtime.PlanDependentCare(actor, choice);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(global::ClankerWorld.Simulation.World.DependentCareActionPlan plan, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => Runtime.CheckDependentCarePlan(plan, policy);
        public bool CanAcceptGuardian(string adult, string child) => Runtime.CanAcceptGuardian(adult, child);
        public void ApplyDependentCareCandidate(string actor, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyDependentCareCandidate(actor, candidate, originalPlan);
        public string GuardianOrderEffectId(string adult, string child) => WorldRuntime.GuardianOrderEffectId(adult, child);
    }
}
