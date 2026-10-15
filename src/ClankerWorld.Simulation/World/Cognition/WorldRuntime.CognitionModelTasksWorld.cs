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
    private ICognitionModelTasksWorld CognitionModelTasksWorld => new CognitionModelTasksAdapter(this);

    private sealed class CognitionModelTasksAdapter(WorldRuntime runtime) : ICognitionModelTasksWorld
    {
        public CognitionSystem Host => runtime.cognitionSystem;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public bool ShouldDispatchConversationChoice(string agentId) => runtime.ShouldDispatchConversationChoice(agentId);
        public IReadOnlyList<SocietyDeferredCognitionRequest> PreviewModelRequests(IReadOnlySet<string> excludedIds, Func<AgentObservation, bool>? observationIsCurrent) => runtime.society.PreviewModelRequests(excludedIds, observationIsCurrent);
        public bool IsQueuedObservationReady(AgentObservation observation) => runtime.IsQueuedObservationReady(observation);
        public void RecordModelAttempt(string id, string status, string? acceptedCandidateId = null, string? setupBlocker = null) => runtime.RecordModelAttempt(id, status, acceptedCandidateId, setupBlocker);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public bool IsUndeliveredFinishedOrderDecision(AgentObservation observation) => runtime.IsUndeliveredFinishedOrderDecision(observation);
        public string? ConversationChoiceContextFor(string agentId) => runtime.ConversationChoiceContextFor(agentId);
        public void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate) => runtime.CancelProviderCall(cancellation, underRuntimeGate);

    }
}
