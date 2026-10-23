namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionAdmissionWorld CognitionAdmissionWorld => new CognitionAdmissionAdapter(this);

    private sealed class CognitionAdmissionAdapter(WorldRuntime runtime) : ICognitionAdmissionWorld
    {
        public global::ClankerWorld.Simulation.World.ActionCheck CheckActionPlan(global::ClankerWorld.Simulation.World.ActionPlan plan) => runtime.CheckActionPlan(plan);
        public void RefuseChoice(global::ClankerWorld.Simulation.World.ActionPlan plan, global::ClankerWorld.Simulation.World.ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public global::System.Collections.Generic.Dictionary<string, string>? AttemptedActions => runtime.attemptedActions;
        public global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions => runtime.withheldActions;
        public void ApplyMemorySummary(string ownerId, global::ClankerWorld.Simulation.Cognition.CognitionMemorySummaryOption summary) => runtime.ApplyMemorySummary(ownerId, summary);
        public void ApplyMemoryCompaction(string ownerId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionMemoryCompactionScore> scores) => runtime.ApplyMemoryCompaction(ownerId, scores);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::System.Collections.Generic.List<string> ApplyObserverGuidanceResult(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionAdmissionResult admission) => runtime.ApplyObserverGuidanceResult(agentId, admission);
        public bool ApplyMedicalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => runtime.ApplyMedicalConsentDecision(decision);
        public bool ApplyOrnamentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => runtime.ApplyOrnamentDecision(decision);
        public bool ApplyAnimalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => runtime.ApplyAnimalConsentDecision(decision);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(runtime.BoatWorld, actor);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public global::ClankerWorld.Simulation.World.FamilyTurnState GuardianPlacementActions => runtime.guardianPlacementActions;
        public bool TalkOrderHasConversation(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.TalkOrderHasConversation(instruction);
        public void ApplyTalkOrderConversationDecision(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => runtime.ApplyTalkOrderConversationDecision(instruction, decision);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.OrderCandidateFor(instruction, person);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null) => runtime.UrgentSurvivalCandidateFor(agentId, person, instruction);
        public bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate) => runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => MarketSystem.IsMarketFoodCandidate(runtime.MarketStallWorld, actor, candidateId);
        public bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null) => runtime.IsShopFoodCandidate(actor, candidateId, selectedPlan);
        public void ApplyShopCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyShopCandidate(actor, state, candidate, originalPlan);
        public void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => runtime.ExecuteOrderStep(instruction, person, candidate);
        public string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.OrderBlockedReason(instruction, person);
        public void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, global::System.Collections.Generic.IReadOnlyList<string>? ballot = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionLandTile>? landTiles = null, global::ClankerWorld.Simulation.Cognition.CognitionLandHearingChoice? hearingChoice = null, global::ClankerWorld.Simulation.Cognition.CognitionNonLandChoice? nonLandChoice = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyPlannedTownCivicCandidate(actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice, originalPlan);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns => runtime.towns;
        public bool ApplyNonLandRemedyAction(global::ClankerWorld.Simulation.World.TownRuntimeState town, string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? plan = null) => runtime.ApplyNonLandRemedyAction(town, actor, candidateId, plan);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public bool ApplyTownProjectDonation(string actor, string id, global::ClankerWorld.Simulation.World.TownProjectDonationActionPlan? originalPlan = null) => runtime.ApplyTownProjectDonation(actor, id, originalPlan);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool ApplyToolMakingRequestDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => runtime.ApplyToolMakingRequestDecision(decision);
        public void ApplyRegisteredCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle, global::ClankerWorld.Simulation.World.ActionPlan? plan = null) => runtime.ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle, plan);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public OrderStore Orders => runtime.orderStore;
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.IsInstructionComplete(instruction);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
