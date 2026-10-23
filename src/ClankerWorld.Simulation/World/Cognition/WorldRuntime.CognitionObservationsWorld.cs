namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionObservationsWorld CognitionObservationsWorld => new CognitionObservationsAdapter(this);

    private sealed class CognitionObservationsAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionObservationsWorld
    {
        public global::System.Collections.Generic.IReadOnlySet<string> PendingModelAgentIds() => Runtime.society.PendingModelAgentIds();
        public bool IsOrderDecisionObservationCurrent(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsOrderDecisionObservationCurrent(observation);
        public void CloseOrdersNotUnderstood(string agentId) => Runtime.CloseOrdersNotUnderstood(agentId);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool HasNewObserverGuidanceFor(string agentId) => Runtime.HasNewObserverGuidanceFor(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => Runtime.FarmWorkFor(workerId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianPlacementCandidate(string actor) => Runtime.GuardianPlacementCandidate(actor);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.SetProject(agentId, project);
        public bool IsProtectiveProject(global::ClankerWorld.Simulation.World.AgentProject? project) => Runtime.IsProtectiveProject(project);
        public string? DestinationNameForModel(string? id) => Runtime.DestinationNameForModel(id);
        public bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId) => Runtime.PauseExpiredUnfillableHouseholdRecipeProject(agentId);
        public bool NeedsCognition(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionIntention? current, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext) => Runtime.NeedsCognition(agentId, current, candidates, conversationChoiceContext);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionObserverGuidance> ObserverGuidanceFor(string agentId) => Runtime.ObserverGuidanceFor(agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId) => Runtime.KnownMapFactsForCognition(agentId);
        public string? GuardianPlacementModelNote(string actor) => Runtime.GuardianPlacementModelNote(actor);
        public string EquipmentNote(string actor) => Runtime.EquipmentNote(actor);
        public string? ContinuityNote(string actor) => Runtime.ContinuityNote(actor);
        public string DepartureNote(string actor) => Runtime.DepartureNote(actor);
        public string? CivicNote(string actor) => Runtime.CivicNote(actor);
        public string? MedicalCareNoteCore(string actor) => Runtime.MedicalCareNoteCore(actor);
        public string? TownMembershipNote(string actor) => Runtime.townSystem.TownMembershipNote(Runtime.civicWorld, actor);
        public string? ToolMakingRequestNoteCore(string actor) => Runtime.ToolMakingRequestNoteCore(actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionParentIdentity[]? InitialChildFamilyBackground(string id, global::ClankerWorld.Simulation.World.AgentState physical) => Runtime.InitialChildFamilyBackground(id, physical);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => Runtime.knowledge;
        public bool JevEnabled => Runtime.jevEnabled;
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public bool EnqueueCognition(global::ClankerWorld.Simulation.Society.SocietyCognitionScheduleEntry entry) => Runtime.society.EnqueueCognition(entry);
        public int PriorityFor(global::ClankerWorld.Simulation.World.AgentState state) => Runtime.PriorityFor(state);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null) => Runtime.DecisionContext(state, candidates, conversationChoiceContext);
    }
}
