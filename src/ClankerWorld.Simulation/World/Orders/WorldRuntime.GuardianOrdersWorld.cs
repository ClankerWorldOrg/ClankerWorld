using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IGuardianOrdersWorld GuardianOrdersWorld => new GuardianOrdersAdapter(this);

    private sealed class GuardianOrdersAdapter(WorldRuntime runtime) : IGuardianOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool NeedsCaregiver(string child) => runtime.NeedsCaregiver(child);
        public ClankerWorld.Simulation.World.DependentCareActionPlan? PlanDependentCare(string actor, DependentCareChoice choice) => runtime.PlanDependentCare(actor, choice);
        public ClankerWorld.Simulation.World.ActionCheck CheckDependentCarePlan(DependentCareActionPlan plan, ActorCapabilityPolicy? policy = null) => runtime.CheckDependentCarePlan(plan, policy);
        public bool CanAcceptGuardian(string adult, string child) => runtime.CanAcceptGuardian(adult, child);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void ApplyDependentCareCandidate(string actor, string candidate, ActionPlan? originalPlan = null) => runtime.ApplyDependentCareCandidate(actor, candidate, originalPlan);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string GuardianOrderEffectId(string adult, string child) => WorldRuntime.GuardianOrderEffectId(adult, child);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
    }
}
