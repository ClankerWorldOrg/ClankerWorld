using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IGuardianOrdersWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    bool IsAdult(string actor);
    bool NeedsCaregiver(string child);
    ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, DependentCareChoice choice);
    ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(DependentCareActionPlan plan, ActorCapabilityPolicy? policy = null);
    bool CanAcceptGuardian(string adult, string child);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void ApplyDependentCareCandidate(string actor, string candidate, ActionPlan? originalPlan = null);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string GuardianOrderEffectId(string adult, string child);
    bool ReadyForBriefInteraction(string actor);
}
