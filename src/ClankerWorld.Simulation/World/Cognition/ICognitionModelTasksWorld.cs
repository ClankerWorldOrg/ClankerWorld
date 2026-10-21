namespace ClankerWorld.Simulation.World;

internal interface ICognitionModelTasksWorld
{
    CognitionSystem Host { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool IsConversationBusy(string agentId);
    bool ShouldDispatchConversationChoice(string agentId);
    global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture();
    bool IsQueuedObservationReady(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    global::ClankerWorld.Simulation.Cognition.AgentObservation RefreshOfferedPlans(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Society.SocietyDeferredCognitionRequest> PreviewModelRequests(global::System.Collections.Generic.IReadOnlySet<string> excludedIds, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, bool>? observationIsCurrent, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, global::ClankerWorld.Simulation.Cognition.AgentObservation>? refreshObservation = null);
    global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false);
    void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    long WorldTick { get; }
    string? ConversationChoiceContextFor(string agentId);
    void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null);
    void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate);
}
