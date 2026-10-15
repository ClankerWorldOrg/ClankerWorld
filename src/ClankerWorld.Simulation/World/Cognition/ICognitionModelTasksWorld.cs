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

internal interface ICognitionModelTasksWorld
{
    CognitionSystem Host { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    bool IsConversationBusy(string agentId);
    bool ShouldDispatchConversationChoice(string agentId);
    IReadOnlyList<SocietyDeferredCognitionRequest> PreviewModelRequests(IReadOnlySet<string> excludedIds, Func<AgentObservation, bool>? observationIsCurrent);
    bool IsQueuedObservationReady(AgentObservation observation);
    void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    bool IsUndeliveredFinishedOrderDecision(AgentObservation observation);
    string? ConversationChoiceContextFor(string agentId);
    void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate);

}
