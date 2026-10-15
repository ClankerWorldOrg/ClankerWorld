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

internal interface ICognitionAdmissionWorld
{
    IReadOnlyDictionary<string, string>? AttemptedActions { get; }
    IReadOnlySet<string>? WithheldActions { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    FamilyTurnState FamilyTurn { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    ActionCheck CheckActionPlan(ActionPlan plan);
    void RefuseChoice(ActionPlan plan, ActionRefusal refusal);
    void ApplyMemorySummary(string ownerId, CognitionMemorySummaryOption summary);
    void ApplyMemoryCompaction(string ownerId, IReadOnlyList<CognitionMemoryCompactionScore> scores);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    List<string> ApplyObserverGuidanceResult(string agentId, CognitionAdmissionResult admission);
    bool ApplyMedicalConsentDecision(SocietyCognitionDispatchResult decision);
    bool ApplyOrnamentDecision(SocietyCognitionDispatchResult decision);
    bool ApplyAnimalConsentDecision(SocietyCognitionDispatchResult decision);
    OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    BoatState? PassengerBoat(string actor);
    void ApplyCandidate(string agentId, AgentState state, string candidateId, bool reportIdle);
    bool TalkOrderHasConversation(OwnerQueuedInstruction instruction);
    void ApplyTalkOrderConversationDecision(OwnerQueuedInstruction instruction, SocietyCognitionDispatchResult decision);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    CognitionCandidate? OrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, AgentState person, OwnerQueuedInstruction? instruction = null);
    bool ShouldInterruptOrder(AgentState person, OwnerQueuedInstruction instruction, CognitionCandidate? orderCandidate, CognitionCandidate? urgentCandidate);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    bool ApplyMarketCandidate(string actor, AgentState state, string id, ActionPlan? originalPlan = null);
    void ExecuteOrderStep(OwnerQueuedInstruction instruction, AgentState person, CognitionCandidate candidate);
    string OrderBlockedReason(OwnerQueuedInstruction instruction, AgentState person);
    void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, IReadOnlyList<string>? ballot = null, IReadOnlyList<CognitionLandTile>? landTiles = null, CognitionLandHearingChoice? hearingChoice = null, CognitionNonLandChoice? nonLandChoice = null, ActionPlan? originalPlan = null);
    bool ApplyNonLandRemedyAction(TownRuntimeState town, string actor, string candidateId, ActionPlan? plan = null);
    bool IsTownProjectDonationCandidate(string id);
    bool ApplyTownProjectDonation(string actor, string id, TownProjectDonationActionPlan? originalPlan = null);
    bool IsMarketCandidate(string id);
    bool ApplyToolMakingRequestDecision(SocietyCognitionDispatchResult decision);
    void ApplyRegisteredCandidate(string agentId, AgentState state, string candidateId, bool reportIdle, ActionPlan? plan = null);
    string ToWireValue(OwnerInstructionKind kind);
    string? UnderstoodTaskFor(string? candidate);
    void RecordAttemptedAction(string actor, string candidate);
    void MarkCheckpointCurrent();
}
