namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionIdentityMomentsWorld CognitionIdentityMomentsWorld => new CognitionIdentityMomentsAdapter(this);

    private sealed class CognitionIdentityMomentsAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionIdentityMomentsWorld
    {
        public void PrepareIdentityMoments(global::System.Action<global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment> prepare, global::System.Collections.Generic.IReadOnlySet<string>? activeModelCallIds = null, global::System.Collections.Generic.IReadOnlySet<string>? activeIdentityIds = null) => Runtime.PrepareIdentityMoments(prepare, activeModelCallIds, activeIdentityIds);
        public void StartPreparedIdentityMoment(global::ClankerWorld.Simulation.World.CognitionSystem.PreparedIdentityMoment prepared) => Runtime.StartPreparedIdentityMoment(prepared);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::ClankerWorld.Simulation.Cognition.AgentObservation CreateIdentityMomentObservation(global::ClankerWorld.Simulation.Society.SocietyAgent person, global::ClankerWorld.Simulation.World.AgentState physical, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment) => CognitionSystem.CreateIdentityMomentObservation(Runtime.CognitionIdentityMomentsWorld, person, physical, moment);
        public void FinishIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMomentKind kind, string outcome, string? personality = null, string? aspiration = null) => Runtime.FinishIdentityMoment(id, kind, outcome, personality, aspiration);
        public void SetIdentityMoment(string id, global::ClankerWorld.Simulation.World.AgentIdentityMoment moment) => CognitionSystem.SetIdentityMoment(Runtime.CognitionIdentityMomentsWorld, id, moment);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
        public global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.IdentityMomentOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false) => Runtime.ScheduleModelWork(kind, actorId, requestId, work, decision, conversation, startInline);
        public void CancelIdentityMoment(string id, bool underRuntimeGate = true) => CognitionSystem.CancelIdentityMoment(Runtime.CognitionIdentityMomentsWorld, id, underRuntimeGate);
        public bool IsIdentityMomentProviderCurrent(global::ClankerWorld.Simulation.World.CognitionSystem.PendingIdentityMoment pending) => Runtime.IsIdentityMomentProviderCurrent(pending);
    }
}
