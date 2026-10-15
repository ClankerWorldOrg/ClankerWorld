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
    private ICognitionAdmissionWorld CognitionAdmissionWorld => new CognitionAdmissionAdapter(this);

    private sealed class CognitionAdmissionAdapter(WorldRuntime runtime) : ICognitionAdmissionWorld
    {
        public IReadOnlyDictionary<string, string>? AttemptedActions => runtime.attemptedActions;
        public IReadOnlySet<string>? WithheldActions => runtime.withheldActions;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public FamilyTurnState FamilyTurn => runtime.guardianPlacementActions;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public OrderStore Orders => runtime.orderStore;
        public ActionCheck CheckActionPlan(ActionPlan plan) => runtime.CheckActionPlan(plan);
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public void ApplyMemorySummary(string ownerId, CognitionMemorySummaryOption summary) => runtime.ApplyMemorySummary(ownerId, summary);
        public void ApplyMemoryCompaction(string ownerId, IReadOnlyList<CognitionMemoryCompactionScore> scores) => runtime.ApplyMemoryCompaction(ownerId, scores);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public List<string> ApplyObserverGuidanceResult(string agentId, CognitionAdmissionResult admission) => runtime.ApplyObserverGuidanceResult(agentId, admission);
        public bool ApplyMedicalConsentDecision(SocietyCognitionDispatchResult decision) => runtime.ApplyMedicalConsentDecision(decision);
        public bool ApplyOrnamentDecision(SocietyCognitionDispatchResult decision) => runtime.ApplyOrnamentDecision(decision);
        public bool ApplyAnimalConsentDecision(SocietyCognitionDispatchResult decision) => runtime.ApplyAnimalConsentDecision(decision);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool TalkOrderHasConversation(OwnerQueuedInstruction instruction) => runtime.TalkOrderHasConversation(instruction);
        public void ApplyTalkOrderConversationDecision(OwnerQueuedInstruction instruction, SocietyCognitionDispatchResult decision) => runtime.ApplyTalkOrderConversationDecision(instruction, decision);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.OrderCandidateFor(instruction, person);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, AgentState person, OwnerQueuedInstruction? instruction = null) => runtime.UrgentSurvivalCandidateFor(agentId, person, instruction);
        public bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, CognitionCandidate? orderCandidate, CognitionCandidate? urgentCandidate) => runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => runtime.IsMarketFoodCandidate(actor, candidateId);
        public bool ApplyMarketCandidate(string actor, AgentState state, string id, ActionPlan? originalPlan = null) => runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public void ExecuteOrderStep(OwnerQueuedInstruction instruction, AgentState person, CognitionCandidate candidate) => runtime.ExecuteOrderStep(instruction, person, candidate);
        public string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person) => runtime.OrderBlockedReason(instruction, person);
        public void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null, ActionPlan? originalPlan = null) => runtime.ApplyPlannedTownCivicCandidate(actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice, originalPlan);
        public bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null) => runtime.ApplyNonLandRemedyAction(town, actor, candidateId, plan);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public bool ApplyTownProjectDonation(string actor, string id, TownProjectDonationActionPlan? originalPlan = null) => runtime.ApplyTownProjectDonation(actor, id, originalPlan);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool ApplyToolMakingRequestDecision(SocietyCognitionDispatchResult decision) => runtime.ApplyToolMakingRequestDecision(decision);
        public void ApplyRegisteredCandidate(string agentId, AgentState state, string candidateId, bool reportIdle, ActionPlan? plan = null) => runtime.ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle, plan);
        public string ToWireValue(OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public void RecordAttemptedAction(string actor, string candidate) => runtime.attemptedActions![actor] = candidate;
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
