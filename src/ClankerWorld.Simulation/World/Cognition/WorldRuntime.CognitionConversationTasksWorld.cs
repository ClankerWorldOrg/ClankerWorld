namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionConversationTasksWorld CognitionConversationTasksWorld => new CognitionConversationTasksAdapter(this);

    private sealed class CognitionConversationTasksAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionConversationTasksWorld
    {
        public void ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn pending, global::ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome outcome, long worldTick, global::System.Func<global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent) => Runtime.ApplyConversationTurnOutcome(pending, outcome, worldTick, providerRouteIsCurrent);
        public int MaximumConversationProviderCallsPerTick => WorldRuntime.MaximumConversationProviderCallsPerTick;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations => Runtime.conversations;
        public bool CanContinueConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => Runtime.CanContinueConversation(conversation);
        public void SuspendConversation(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption) => Runtime.SuspendConversation(conversationId, interruption);
        public global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest CreateConversationRequest(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speakerId) => Runtime.CreateConversationRequest(conversation, speakerId);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public bool CanProposeMarriage(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => Runtime.CanProposeMarriage(conversation);
        public string? RequestedConversationActivity(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speaker) => Runtime.RequestedConversationActivity(conversation, speaker);
        public void CancelPendingConversationTurn(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true) => Runtime.CancelPendingConversationTurn(conversationId, interruption, suspendCurrent, underRuntimeGate);
        public void RecordStartedConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation) => Runtime.conversationSystem.SetConversation(conversation);
        public bool ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnResponse? response, Exception? failure, long tick, Func<bool> providerRouteIsCurrent) => Runtime.conversationSystem.ApplyConversationTurnOutcome(Runtime.conversationWorld, request, response, failure, tick, providerRouteIsCurrent);
        public Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(global::ClankerWorld.Simulation.Cognition.IAgentConversationProvider provider, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, CancellationToken cancellationToken)
        {
            var outerInvocation = providerInvocationUnderGate;
            providerInvocationUnderGate = Runtime;
            try
            {
                return Runtime.ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind.Conversation, request.SpeakerId, request.RequestId,
                    () => CognitionSystem.RunConversationProviderAsync(provider, request, Runtime.modelTimeProvider, cancellationToken),
                    conversation: request, startInline: true);
            }
            finally { providerInvocationUnderGate = outerInvocation; }
        }
    }
}
