namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionIdentityMomentsWorld CognitionIdentityMomentsWorld => new CognitionIdentityMomentsAdapter(this);

    private sealed class CognitionIdentityMomentsAdapter(WorldRuntime runtime) : ICognitionIdentityMomentsWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public long WorldTick => runtime.WorldTick;
        public global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents => runtime.agentStore;
        public void PrepareIdentityMoments(global::System.Action<global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment> prepare, global::System.Collections.Generic.IReadOnlySet<string>? activeModelCallIds = null, global::System.Collections.Generic.IReadOnlySet<string>? activeIdentityIds = null) => runtime.PrepareIdentityMoments(prepare, activeModelCallIds, activeIdentityIds);
        public void StartPreparedIdentityMoment(global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment prepared) => runtime.StartPreparedIdentityMoment(prepared);
        public CognitionSystem Host => runtime.cognitionSystem;
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public global::ClankerWorld.Simulation.Cognition.AgentObservation CreateIdentityMomentObservation(global::ClankerWorld.Simulation.Society.SocietyAgent person, global::ClankerWorld.Simulation.World.AgentState physical, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment) => runtime.CreateIdentityMomentObservation(person, physical, moment);
        public void FinishIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null) => runtime.FinishIdentityMoment(id, kind, outcome, personality, aspiration);
        public void SetIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment) => runtime.SetIdentityMoment(id, moment);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::System.TimeProvider ModelTimeProvider => runtime.modelTimeProvider;
        public global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false) => runtime.ScheduleModelWork(kind, actorId, requestId, work, decision, conversation, startInline);
        public void CancelIdentityMoment(string id, bool underRuntimeGate = true) => runtime.CancelIdentityMoment(id, underRuntimeGate);
        public void FinishModelWork(string actorId, string requestId, global::ClankerWorld.Simulation.Cognition.ModelWorkStage stage, long? admittedTick = null) => runtime.FinishModelWork(actorId, requestId, stage, admittedTick);
        public void CancelProviderCall(global::System.Threading.CancellationTokenSource cancellation, bool underRuntimeGate) => runtime.CancelProviderCall(cancellation, underRuntimeGate);
        public bool IsIdentityMomentProviderCurrent(global::ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment pending) => runtime.IsIdentityMomentProviderCurrent(pending);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void MarkModelReplyAdmitted(string requestId) => runtime.admittedModelReplies.Add(requestId);
    }
}
