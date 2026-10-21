namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionModelTasksWorld CognitionModelTasksWorld => new CognitionModelTasksAdapter(this);

    private sealed class CognitionModelTasksAdapter(WorldRuntime runtime) : ICognitionModelTasksWorld
    {
        public CognitionSystem Host => runtime.cognitionSystem;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public bool ShouldDispatchConversationChoice(string agentId) => runtime.ShouldDispatchConversationChoice(agentId);
        public global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture() => runtime.society.Capture();
        public bool IsQueuedObservationReady(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => runtime.IsQueuedObservationReady(observation);
        public global::ClankerWorld.Simulation.Cognition.AgentObservation RefreshOfferedPlans(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => runtime.RefreshOfferedPlans(observation);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Society.SocietyDeferredCognitionRequest> PreviewModelRequests(global::System.Collections.Generic.IReadOnlySet<string> excludedIds, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, bool>? observationIsCurrent, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, global::ClankerWorld.Simulation.Cognition.AgentObservation>? refreshObservation = null) => runtime.society.PreviewModelRequests(excludedIds, observationIsCurrent, refreshObservation);
        public global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false) => runtime.ScheduleModelWork(kind, actorId, requestId, work, decision, conversation, startInline);
        public void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null) => runtime.RecordModelAttempt(id, status, acceptedCandidateId, setupBlocker);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public bool IsUndeliveredFinishedOrderDecision(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => runtime.IsUndeliveredFinishedOrderDecision(observation);
        public long WorldTick => runtime.WorldTick;
        public string? ConversationChoiceContextFor(string agentId) => runtime.ConversationChoiceContextFor(agentId);
        public void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null) => runtime.FinishModelWork(actorId, requestId, stage, admittedTick);
        public void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate) => runtime.CancelProviderCall(cancellation, underRuntimeGate);
    }
}
