namespace ClankerWorld.Simulation.World;

internal interface IGuardianOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool NeedsCaregiver(string child);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, global::ClankerWorld.Simulation.World.DependentCareChoice choice);
    global::ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(global::ClankerWorld.Simulation.World.DependentCareActionPlan plan, global::ClankerWorld.Simulation.World.ActorCapabilityPolicy? policy = null);
    bool CanAcceptGuardian(string adult, string child);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string GuardianOrderBlockedReason(string adult, string child);
    void ApplyDependentCareCandidate(string actor, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string GuardianOrderEffectId(string adult, string child);
    bool ReadyForBriefInteraction(string actor);
}
