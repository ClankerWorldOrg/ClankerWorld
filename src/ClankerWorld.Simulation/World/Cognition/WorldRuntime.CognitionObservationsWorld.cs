namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionObservationsWorld CognitionObservationsWorld => new CognitionObservationsAdapter(this);

    private sealed class CognitionObservationsAdapter(WorldRuntime runtime) : ICognitionObservationsWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture() => runtime.society.Capture();
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.Generic.IReadOnlySet<string> PendingModelAgentIds() => runtime.society.PendingModelAgentIds();
        public bool IsOrderDecisionObservationCurrent(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => runtime.IsOrderDecisionObservationCurrent(observation);
        public bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => runtime.IsUndeliveredFinishedOrderDecision(observation);
        public void CloseOrdersNotUnderstood(string agentId) => runtime.CloseOrdersNotUnderstood(agentId);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool HasNewObserverGuidanceFor(string agentId) => OrderSystem.HasNewObserverGuidanceFor(runtime.ControlWorld, agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => runtime.FarmWorkFor(workerId);
        public bool ShouldDispatchConversationChoice(string agentId) => runtime.ShouldDispatchConversationChoice(agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianPlacementCandidate(string actor) => FamilySystem.GuardianPlacementCandidate(runtime.FamilyGuardianWorld, actor);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project) => runtime.SetProject(agentId, project);
        public bool IsProtectiveProject(global::ClankerWorld.Simulation.World.AgentProject? project) => runtime.IsProtectiveProject(project);
        public string? ConversationChoiceContextFor(string agentId) => runtime.ConversationChoiceContextFor(agentId);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public string? DestinationNameForModel(string? id) => CognitionSystem.DestinationNameForModel(runtime.CognitionCandidatesWorld, id);
        public bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId) => runtime.PauseExpiredUnfillableHouseholdRecipeProject(agentId);
        public bool NeedsCognition(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionIntention? current, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext) => CognitionSystem.NeedsCognition(runtime.CognitionContextWorld, agentId, current, candidates, conversationChoiceContext);
        public long WorldTick => runtime.WorldTick;
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Cognition.CognitionObserverGuidance> ObserverGuidanceFor(string agentId) => OrderSystem.ObserverGuidanceFor(runtime.ControlWorld, agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId) => runtime.KnownMapFactsForCognition(agentId);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns => runtime.towns;
        public string? GuardianPlacementModelNote(string actor) => runtime.GuardianPlacementModelNote(actor);
        public string EquipmentNote(string actor) => runtime.EquipmentNote(actor);
        public string? ContinuityNote(string actor) => runtime.ContinuityNote(actor);
        public string DepartureNote(string actor) => runtime.DepartureNote(actor);
        public string? CivicNote(string actor) => TownSystem.CivicNote(runtime.civicWorld, actor);
        public string? MedicalCareNoteCore(string actor) => runtime.MedicalCareNoteCore(actor);
        public string? TownMembershipNote(string actor) => runtime.townSystem.TownMembershipNote(runtime.civicWorld, actor);
        public string? ToolMakingRequestNoteCore(string actor) => runtime.ToolMakingRequestNoteCore(actor);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages => runtime.marriages;
        public global::ClankerWorld.Simulation.Cognition.CognitionParentIdentity[]? InitialChildFamilyBackground(string id, global::ClankerWorld.Simulation.World.AgentState physical) => CognitionSystem.InitialChildFamilyBackground(runtime.CognitionPersonalIdentityWorld, id, physical);
        public global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge => runtime.knowledge;
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public bool JevEnabled => runtime.jevEnabled;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public CognitionSystem Host => runtime.cognitionSystem;
        public bool EnqueueCognition(global::ClankerWorld.Simulation.Society.SocietyCognitionScheduleEntry entry) => runtime.society.EnqueueCognition(entry);
        public int PriorityFor(global::ClankerWorld.Simulation.World.AgentState state) => CognitionSystem.PriorityFor(runtime.CognitionCandidatesWorld, state);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public OrderStore Orders => runtime.orderStore;
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null) => CognitionSystem.DecisionContext(runtime.CognitionContextWorld, state, candidates, conversationChoiceContext);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
