namespace ClankerWorld.Simulation.World;

internal interface ICognitionObservationsWorld
{
    global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture();
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlySet<string> PendingModelAgentIds();
    bool IsOrderDecisionObservationCurrent(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    void CloseOrdersNotUnderstood(string agentId);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool HasNewObserverGuidanceFor(string agentId);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool IsConversationBusy(string agentId);
    global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    bool ShouldDispatchConversationChoice(string agentId);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? GuardianPlacementCandidate(string actor);
    bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project);
    bool IsProtectiveProject(global::ClankerWorld.Simulation.World.AgentProject? project);
    string? ConversationChoiceContextFor(string agentId);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    string? DestinationNameForModel(string? id);
    bool PauseExpiredUnfillableHouseholdRecipeProject(string agentId);
    bool NeedsCognition(string agentId, global::ClankerWorld.Simulation.Cognition.CognitionIntention? current, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext);
    long WorldTick { get; }
    global::ClankerWorld.Simulation.Cognition.CognitionObserverGuidance[] ObserverGuidanceFor(string agentId);
    global::ClankerWorld.Simulation.Cognition.CognitionKnowledgeFact[] KnownMapFactsForCognition(string agentId);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    string? GuardianPlacementModelNote(string actor);
    string EquipmentNote(string actor);
    string? ContinuityNote(string actor);
    string DepartureNote(string actor);
    string? CivicNote(string actor);
    string? MedicalCareNoteCore(string actor);
    string? TownMembershipNote(string actor);
    string? ToolMakingRequestNoteCore(string actor);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    global::ClankerWorld.Simulation.Cognition.CognitionParentIdentity[]? InitialChildFamilyBackground(string id, global::ClankerWorld.Simulation.World.AgentState physical);
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool JevEnabled { get; }
    bool IsAdult(string actor);
    CognitionSystem Host { get; }
    bool EnqueueCognition(global::ClankerWorld.Simulation.Society.SocietyCognitionScheduleEntry entry);
    int PriorityFor(global::ClankerWorld.Simulation.World.AgentState state);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    OrderStore Orders { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null);
    void MarkCheckpointCurrent();
}
