namespace ClankerWorld.Simulation.World;

internal interface ICognitionConversationTasksWorld
{
    CognitionSystem Host { get; }
    void ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn pending, global::ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome outcome, long worldTick, global::System.Func<global::ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent);
    int MaximumConversationProviderCallsPerTick { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.Cognition.AgentConversation> Conversations { get; }
    bool CanContinueConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation);
    void SuspendConversation(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption);
    long WorldTick { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest CreateConversationRequest(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speakerId);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool CanProposeMarriage(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation);
    string? RequestedConversationActivity(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation, string speaker);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.AgentMarriage> Marriages { get; }
    void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null);
    void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate);
    void CancelPendingConversationTurn(string conversationId, global::ClankerWorld.Simulation.Cognition.AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true);
    void MarkModelReplyAdmitted(string requestId);
    void RecordStartedConversation(global::ClankerWorld.Simulation.Cognition.AgentConversation conversation);
    bool ApplyConversationTurnOutcome(global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnResponse? response, Exception? failure, long tick, Func<bool> providerRouteIsCurrent);
    Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(global::ClankerWorld.Simulation.Cognition.IAgentConversationProvider provider, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest request, CancellationToken cancellationToken);
}
