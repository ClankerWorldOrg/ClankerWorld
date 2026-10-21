namespace ClankerWorld.Simulation.World;

internal interface ICognitionIdentityMomentsWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    long WorldTick { get; }
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void PrepareIdentityMoments(global::System.Action<global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment> prepare, global::System.Collections.Generic.IReadOnlySet<string>? activeModelCallIds = null, global::System.Collections.Generic.IReadOnlySet<string>? activeIdentityIds = null);
    void StartPreparedIdentityMoment(global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment prepared);
    CognitionSystem Host { get; }
    bool IsConversationBusy(string agentId);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.AgentObservation CreateIdentityMomentObservation(global::ClankerWorld.Simulation.Society.SocietyAgent person, global::ClankerWorld.Simulation.World.AgentState physical, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment);
    void FinishIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null);
    void SetIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::System.TimeProvider ModelTimeProvider { get; }
    global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false);
    void CancelIdentityMoment(string id, bool underRuntimeGate = true);
    void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null);
    void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate);
    bool IsIdentityMomentProviderCurrent(global::ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment pending);
    void MarkCheckpointCurrent();
    void MarkModelReplyAdmitted(string requestId);
}
