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

internal interface ICognitionContinuationWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlySet<string>? WithheldActions { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyDictionary<string, string>? AttemptedActions { get; }
    string NonLandRemedyPrefix { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    string NonLandRelayPrefix { get; }
    BoatState? PassengerBoat(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool NeedsUrgentFood(AgentState person);
    List<CognitionCandidate> CreateCandidates(string agentId, AgentState state, bool restrictForOrder = true);
    void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle);
    bool NeedsUrgentWarmth(AgentState person);
    bool TryApplyWaitingRoutine(string id, AgentState state);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    bool ContinueGuardianPlacement(string actor);
    bool IsConversationBusy(string agentId);
    CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, AgentState person, OwnerQueuedInstruction? instruction = null);
    bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, CognitionCandidate? orderCandidate, CognitionCandidate? urgentCandidate);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    void ContinueMarketWalk(string actor, string id);
    void ExecuteOrderStep(OwnerQueuedInstruction instruction, AgentState person, CognitionCandidate candidate);
    string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    bool CanContinueEquipmentRepair(string actor);
    EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    bool CanContinueLesson(string actor, bool waitingForModel = false);
    void ContinueLesson(string actor, bool waitingForModel = false, LearningActionPlan? originalPlan = null);
    bool ContinueFarmWork(string workerId);
    bool CanContinueProject(AgentState state, bool waitingForModel = false);
    void ContinueProject(string agentId, AgentState state);
    bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null);
    void ContinueTownCivicVisit(string actor, string candidate);
    void ContinueTownLandSaleWalk(string actor, string candidate);
    bool IsTownProjectDonationCandidate(string id);
    void ContinueTownProjectDonationWalk(string actor, string id);
    bool IsMarketCandidate(string id);
    bool IsOrnamentCandidate(string candidate);
    void ContinueOrnamentWalk(string actor, string candidate);
    bool CanContinuePendingTask(AgentState state, string id);
    SocietyCognitionSchedulerState CaptureCognition();
    void RecordAttemptedAction(string actor, string candidate);
}
