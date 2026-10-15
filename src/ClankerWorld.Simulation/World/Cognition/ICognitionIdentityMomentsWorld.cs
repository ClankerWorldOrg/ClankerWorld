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

internal interface ICognitionIdentityMomentsWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    long WorldTick { get; }
    AgentStore PhysicalAgents { get; }
    CognitionSystem Host { get; }
    void PrepareIdentityMoments(Action<CognitionSystem.PreparedIdentityMoment> prepare, IReadOnlySet<string>? activeModelCallIds = null, IReadOnlySet<string>? activeIdentityIds = null);
    void StartPreparedIdentityMoment(CognitionSystem.PreparedIdentityMoment prepared);
    bool IsConversationBusy(string agentId);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    AgentObservation CreateIdentityMomentObservation(SocietyAgent person, AgentState physical, AgentIdentityMoment moment);
    void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null);
    void SetIdentityMoment(string id, AgentIdentityMoment moment);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    void CancelIdentityMoment(string id, bool underRuntimeGate = true);
    void CancelProviderCall(CancellationTokenSource cancellation, bool underRuntimeGate);
    bool IsIdentityMomentProviderCurrent(CognitionSystem.PendingIdentityMoment pending);
    void MarkCheckpointCurrent();
}
