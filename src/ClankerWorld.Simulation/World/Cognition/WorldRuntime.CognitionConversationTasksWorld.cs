namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionConversationTasksWorld CognitionConversationTasksWorld => new CognitionConversationTasksAdapter(this);

    private sealed class CognitionConversationTasksAdapter(WorldRuntime runtime) : ICognitionConversationTasksWorld
    {
        public CognitionSystem Host => runtime.cognitionSystem;
        public void ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn pending, global::ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome outcome, long worldTick, global::System.Func<global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent) => runtime.ApplyConversationTurnOutcome(pending, outcome, worldTick, providerRouteIsCurrent);
        public int MaximumConversationProviderCallsPerTick => WorldRuntime.MaximumConversationProviderCallsPerTick;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => runtime.conversations;
        public bool CanContinueConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => runtime.CanContinueConversation(conversation);
        public void SuspendConversation(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption) => runtime.SuspendConversation(conversationId, interruption);
        public long WorldTick => runtime.WorldTick;
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest CreateConversationRequest(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speakerId) => runtime.CreateConversationRequest(conversation, speakerId);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool CanProposeMarriage(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => runtime.CanProposeMarriage(conversation);
        public string? RequestedConversationActivity(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speaker) => runtime.RequestedConversationActivity(conversation, speaker);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages => runtime.marriages;
        public void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null) => runtime.FinishModelWork(actorId, requestId, stage, admittedTick);
        public void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate) => runtime.CancelProviderCall(cancellation, underRuntimeGate);
        public void CancelPendingConversationTurn(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true) => runtime.CancelPendingConversationTurn(conversationId, interruption, suspendCurrent, underRuntimeGate);
        public void MarkModelReplyAdmitted(string requestId) => runtime.admittedModelReplies.Add(requestId);
        public void RecordStartedConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => runtime.conversationSystem.SetConversation(conversation);
        public bool ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnResponse? response, Exception? failure, long tick, Func<bool> providerRouteIsCurrent) => runtime.conversationSystem.ApplyConversationTurnOutcome(runtime.conversationWorld, request, response, failure, tick, providerRouteIsCurrent);
        public Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(global::ClankerWorld.Simulation.Cognition.IAgentConversationProvider provider, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, CancellationToken cancellationToken)
        {
            var outerInvocation = providerInvocationUnderGate;
            providerInvocationUnderGate = runtime;
            try
            {
                return runtime.ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind.Conversation, request.SpeakerId, request.RequestId,
                    () => CognitionSystem.RunConversationProviderAsync(provider, request, runtime.modelTimeProvider, cancellationToken),
                    conversation: request, startInline: true);
            }
            finally { providerInvocationUnderGate = outerInvocation; }
        }
    }
}
