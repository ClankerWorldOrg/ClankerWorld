using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionConversationTasksWorld CognitionConversationTasksWorld => new CognitionConversationTasksAdapter(this);

    private sealed class CognitionConversationTasksAdapter(WorldRuntime runtime) : ICognitionConversationTasksWorld
    {
        public CognitionSystem Host => runtime.cognitionSystem;
        public int MaximumConversationProviderCallsPerTick => WorldRuntime.MaximumConversationProviderCallsPerTick;
        public ReadOnlyCollection<AgentConversation> Conversations => runtime.conversations;
        public long WorldTick => runtime.WorldTick;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public ReadOnlyCollection<AgentMarriage> Marriages => runtime.marriages;
        public void ApplyConversationTurnOutcome(CognitionSystem.PendingConversationTurn pending, CognitionSystem.ConversationTurnOutcome outcome, long worldTick, Func<CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent) => runtime.ApplyConversationTurnOutcome(pending, outcome, worldTick, providerRouteIsCurrent);
        public bool CanContinueConversation(AgentConversation conversation) => runtime.CanContinueConversation(conversation);
        public void SuspendConversation(string conversationId, AgentConversationInterruption interruption) => runtime.SuspendConversation(conversationId, interruption);
        public AgentConversationTurnRequest CreateConversationRequest(AgentConversation conversation, string speakerId) => runtime.CreateConversationRequest(conversation, speakerId);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public bool CanProposeMarriage(AgentConversation conversation) => runtime.CanProposeMarriage(conversation);
        public string? RequestedConversationActivity(AgentConversation conversation, string speaker) => runtime.RequestedConversationActivity(conversation, speaker);
        public void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate) => runtime.CancelProviderCall(cancellation, underRuntimeGate);
        public void CancelPendingConversationTurn(string conversationId, AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true) => runtime.CancelPendingConversationTurn(conversationId, interruption, suspendCurrent, underRuntimeGate);
        public void ApplyConversationTurnOutcome(AgentConversationTurnRequest request, AgentConversationTurnResponse? response, Exception? failure, long worldTick, Func<bool> providerRouteIsCurrent) => runtime.conversationSystem.ApplyConversationTurnOutcome(runtime.conversationWorld, request, response, failure, worldTick, providerRouteIsCurrent);
        public void RecordStartedConversation(AgentConversation conversation) => runtime.conversationSystem.SetConversation(conversation);
        public Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(IAgentConversationProvider provider, AgentConversationTurnRequest request, CancellationToken cancellation) { var outerInvocation = providerInvocationUnderGate; providerInvocationUnderGate = runtime; try { return CognitionSystem.RunConversationProviderAsync(provider, request, cancellation); } finally { providerInvocationUnderGate = outerInvocation; } }
    }
}
