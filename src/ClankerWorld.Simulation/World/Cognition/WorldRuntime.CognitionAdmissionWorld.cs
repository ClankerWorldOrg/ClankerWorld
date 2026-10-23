namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionAdmissionWorld CognitionAdmissionWorld => new CognitionAdmissionAdapter(this);

    private sealed class CognitionAdmissionAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionAdmissionWorld
    {
        public void ApplyMemorySummary(string ownerId, global::ClankerWorld.Simulation.Cognition.CognitionMemorySummaryOption summary) => Runtime.ApplyMemorySummary(ownerId, summary);
        public void ApplyMemoryCompaction(string ownerId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionMemoryCompactionScore> scores) => Runtime.ApplyMemoryCompaction(ownerId, scores);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public global::System.Collections.Generic.List<string> ApplyObserverGuidanceResult(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionAdmissionResult admission) => Runtime.ApplyObserverGuidanceResult(agentId, admission);
        public bool ApplyMedicalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyMedicalConsentDecision(decision);
        public bool ApplyOrnamentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyOrnamentDecision(decision);
        public bool ApplyAnimalConsentDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyAnimalConsentDecision(decision);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => Runtime.PassengerBoat(actor);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => Runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public void ApplyTalkOrderConversationDecision(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyTalkOrderConversationDecision(instruction, decision);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null) => Runtime.UrgentSurvivalCandidateFor(agentId, person, instruction);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => Runtime.IsMarketFoodCandidate(actor, candidateId);
        public bool ApplyMarketCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string id, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyMarketCandidate(actor, state, id, originalPlan);
        public bool IsShopFoodCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ShopBuyActionPlan? selectedPlan = null) => Runtime.IsShopFoodCandidate(actor, candidateId, selectedPlan);
        public void ApplyShopCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyShopCandidate(actor, state, candidate, originalPlan);
        public void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => Runtime.ExecuteOrderStep(instruction, person, candidate);
        public void ApplyPlannedTownCivicCandidate(string actor, string candidate, string? proposalText = null, global::System.Collections.Generic.IReadOnlyList<string>? ballot = null, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionLandTile>? landTiles = null, global::ClankerWorld.Simulation.Cognition.CognitionLandHearingChoice? hearingChoice = null, global::ClankerWorld.Simulation.Cognition.CognitionNonLandChoice? nonLandChoice = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyPlannedTownCivicCandidate(actor, candidate, proposalText, ballot, landTiles, hearingChoice, nonLandChoice, originalPlan);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public bool ApplyTownProjectDonation(string actor, string id, global::ClankerWorld.Simulation.World.TownProjectDonationActionPlan? originalPlan = null) => Runtime.ApplyTownProjectDonation(actor, id, originalPlan);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool ApplyToolMakingRequestDecision(global::ClankerWorld.Simulation.Society.SocietyCognitionDispatchResult decision) => Runtime.ApplyToolMakingRequestDecision(decision);
        public void ApplyRegisteredCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle, global::ClankerWorld.Simulation.World.ActionPlan? plan = null) => Runtime.ApplyRegisteredCandidate(agentId, state, candidateId, reportIdle, plan);
        public string ToWireValue(global::ClankerWorld.Simulation.World.OwnerInstructionKind kind) => WorldRuntime.ToWireValue(kind);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
    }
}
