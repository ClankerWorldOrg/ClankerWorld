namespace ClankerWorld.Simulation.World;

internal interface ICognitionAdmissionWorld
{
    global::ClankerWorld.Simulation.World.ActionCheck CheckActionPlan(global::ClankerWorld.Simulation.World.ActionPlan plan);
    void RefuseChoice(global::ClankerWorld.Simulation.World.ActionPlan plan, global::ClankerWorld.Simulation.World.ActionRefusal refusal);
    global::System.Collections.Generic.Dictionary<string, string>? AttemptedActions { get; }
    global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions { get; }
    void ApplyMemorySummary(string ownerId, global::ClankerWorld.Simulation.Cognition.CognitionMemorySummaryOption summary);
    void ApplyMemoryCompaction(string ownerId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionMemoryCompactionScore> scores);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::System.Collections.Generic.List<string> ApplyObserverGuidanceResult(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionAdmissionResult admission);
    bool ApplyMedicalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    bool ApplyOrnamentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    bool ApplyAnimalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    global::ClankerWorld.Simulation.World.FamilyTurnState GuardianPlacementActions { get; }
    bool TalkOrderHasConversation(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ApplyTalkOrderConversationDecision(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null);
    bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null);
    void ApplyShopCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate);
    string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, global::System.Collections.Generic.IReadOnlyList<string>? ballot = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionLandTile>? landTiles = null, global::ClankerWorld.Simulation.Cognition.CognitionLandHearingChoice? hearingChoice = null, global::ClankerWorld.Simulation.Cognition.CognitionNonLandChoice? nonLandChoice = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    bool ApplyNonLandRemedyAction(global::ClankerWorld.Simulation.World.TownRuntimeState town, string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? plan = null);
    bool IsTownProjectDonationCandidate(string id);
    bool ApplyTownProjectDonation(string actor, string id, global::ClankerWorld.Simulation.World.TownProjectDonationActionPlan? originalPlan = null);
    bool IsMarketCandidate(string id);
    bool ApplyToolMakingRequestDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision);
    void ApplyRegisteredCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle, global::ClankerWorld.Simulation.World.ActionPlan? plan = null);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind);
    string? UnderstoodTaskFor(string? candidate);
    bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MarkCheckpointCurrent();
}
