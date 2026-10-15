using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionContinuationWorld CognitionContinuationWorld => new CognitionContinuationAdapter(this);

    private sealed class CognitionContinuationAdapter(WorldRuntime runtime) : ICognitionContinuationWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlySet<string>? WithheldActions => runtime.withheldActions;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyDictionary<string, string>? AttemptedActions => runtime.attemptedActions;
        public string NonLandRemedyPrefix => WorldRuntime.NonLandRemedyPrefix;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public string NonLandRelayPrefix => WorldRuntime.NonLandRelayPrefix;
        public BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public List<CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool TryApplyWaitingRoutine(string id, AgentState state) => runtime.TryApplyWaitingRoutine(id, state);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public bool ContinueGuardianPlacement(string actor) => runtime.ContinueGuardianPlacement(actor);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.OrderCandidateFor(instruction, person);
        public CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, AgentState person, OwnerQueuedInstruction? instruction = null) => runtime.UrgentSurvivalCandidateFor(agentId, person, instruction);
        public bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, CognitionCandidate? orderCandidate, CognitionCandidate? urgentCandidate) => runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => runtime.IsMarketFoodCandidate(actor, candidateId);
        public void ContinueMarketWalk(string actor, string id) => runtime.ContinueMarketWalk(actor, id);
        public void ExecuteOrderStep(OwnerQueuedInstruction instruction, AgentState person, CognitionCandidate candidate) => runtime.ExecuteOrderStep(instruction, person, candidate);
        public string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.OrderBlockedReason(instruction, person);
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => runtime.CanContinueLesson(actor, waitingForModel);
        public void ContinueLesson(string actor, bool waitingForModel = false, LearningActionPlan? originalPlan = null) => runtime.ContinueLesson(actor, waitingForModel, originalPlan);
        public bool ContinueFarmWork(string workerId) => runtime.ContinueFarmWork(workerId);
        public bool CanContinueProject(AgentState state, bool waitingForModel = false) => runtime.CanContinueProject(state, waitingForModel);
        public void ContinueProject(string agentId, AgentState state) => runtime.ContinueProject(agentId, state);
        public bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null) => runtime.ApplyNonLandRemedyAction(town, actor, candidateId, plan);
        public void ContinueTownCivicVisit(string actor, string candidate) => runtime.ContinueTownCivicVisit(actor, candidate);
        public void ContinueTownLandSaleWalk(string actor, string candidate) => runtime.ContinueTownLandSaleWalk(actor, candidate);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public void ContinueTownProjectDonationWalk(string actor, string id) => runtime.ContinueTownProjectDonationWalk(actor, id);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool IsOrnamentCandidate(string candidate) => WorldRuntime.IsOrnamentCandidate(candidate);
        public void ContinueOrnamentWalk(string actor, string candidate) => runtime.ContinueOrnamentWalk(actor, candidate);
        public bool CanContinuePendingTask(AgentState state, string id) => runtime.CanContinuePendingTask(state, id);
        public SocietyCognitionSchedulerState CaptureCognition() => runtime.society.Capture().Cognition;
        public void RecordAttemptedAction(string actor, string candidate) => runtime.attemptedActions![actor] = candidate;
    }
}
