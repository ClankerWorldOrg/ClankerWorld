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

internal interface ICognitionConversationTasksWorld
{
    CognitionSystem Host { get; }
    int MaximumConversationProviderCallsPerTick { get; }
    ReadOnlyCollection<AgentConversation> Conversations { get; }
    long WorldTick { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    ReadOnlyCollection<AgentMarriage> Marriages { get; }
    void ApplyConversationTurnOutcome(CognitionSystem.PendingConversationTurn pending, CognitionSystem.ConversationTurnOutcome outcome, long worldTick, Func<CognitionSystem.PendingConversationTurn, bool> providerRouteIsCurrent);
    bool CanContinueConversation(AgentConversation conversation);
    void SuspendConversation(string conversationId, AgentConversationInterruption interruption);
    AgentConversationTurnRequest CreateConversationRequest(AgentConversation conversation, string speakerId);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    bool CanProposeMarriage(AgentConversation conversation);
    string? RequestedConversationActivity(AgentConversation conversation, string speaker);
    void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate);
    void CancelPendingConversationTurn(string conversationId, AgentConversationInterruption interruption, bool suspendCurrent = true, bool underRuntimeGate = true);
    void ApplyConversationTurnOutcome(AgentConversationTurnRequest request, AgentConversationTurnResponse? response, Exception? failure, long worldTick, Func<bool> providerRouteIsCurrent);
    void RecordStartedConversation(AgentConversation conversation);
    Task<CognitionSystem.ConversationTurnOutcome> RunConversationProviderUnderGate(IAgentConversationProvider provider, AgentConversationTurnRequest request, CancellationToken cancellation);
}
