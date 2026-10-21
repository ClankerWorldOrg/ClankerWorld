namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IGuardianOrdersWorld GuardianOrdersWorld => new GuardianOrdersAdapter(this);

    private sealed class GuardianOrdersAdapter(WorldRuntime runtime) : IGuardianOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool NeedsCaregiver(string child) => runtime.NeedsCaregiver(child);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, global::ClankerWorld.Simulation.World.DependentCareChoice choice) => runtime.PlanDependentCare(actor, choice);
        public global::ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(global::ClankerWorld.Simulation.World.DependentCareActionPlan plan, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null) => runtime.CheckDependentCarePlan(plan, policy);
        public bool CanAcceptGuardian(string adult, string child) => runtime.CanAcceptGuardian(adult, child);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string GuardianOrderBlockedReason(string adult, string child) => runtime.GuardianOrderBlockedReason(adult, child);
        public void ApplyDependentCareCandidate(string actor, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyDependentCareCandidate(actor, candidate, originalPlan);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string GuardianOrderEffectId(string adult, string child) => WorldRuntime.GuardianOrderEffectId(adult, child);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
    }
}
