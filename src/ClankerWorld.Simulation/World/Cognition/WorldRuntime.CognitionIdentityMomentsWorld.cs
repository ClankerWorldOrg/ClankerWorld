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
    private ICognitionIdentityMomentsWorld CognitionIdentityMomentsWorld => new CognitionIdentityMomentsAdapter(this);

    private sealed class CognitionIdentityMomentsAdapter(WorldRuntime runtime) : ICognitionIdentityMomentsWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public CognitionSystem Host => runtime.cognitionSystem;
        public void PrepareIdentityMoments(Action<CognitionSystem.PreparedIdentityMoment> prepare, IReadOnlySet<string>? activeModelCallIds = null, IReadOnlySet<string>? activeIdentityIds = null) => runtime.PrepareIdentityMoments(prepare, activeModelCallIds, activeIdentityIds);
        public void StartPreparedIdentityMoment(CognitionSystem.PreparedIdentityMoment prepared) => runtime.StartPreparedIdentityMoment(prepared);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public AgentObservation CreateIdentityMomentObservation(SocietyAgent person, AgentState physical, AgentIdentityMoment moment) => runtime.CreateIdentityMomentObservation(person, physical, moment);
        public void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null) => runtime.FinishIdentityMoment(id, kind, outcome, personality, aspiration);
        public void SetIdentityMoment(string id, AgentIdentityMoment moment) => runtime.SetIdentityMoment(id, moment);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void CancelIdentityMoment(string id, bool underRuntimeGate = true) => runtime.CancelIdentityMoment(id, underRuntimeGate);
        public void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate) => runtime.CancelProviderCall(cancellation, underRuntimeGate);
        public bool IsIdentityMomentProviderCurrent(CognitionSystem.PendingIdentityMoment pending) => runtime.IsIdentityMomentProviderCurrent(pending);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
