namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionModelTasksWorld CognitionModelTasksWorld => new CognitionModelTasksAdapter(this);

    private sealed class CognitionModelTasksAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionModelTasksWorld
    {
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public bool IsQueuedObservationReady(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsQueuedObservationReady(observation);
        public global::ClankerWorld.Simulation.Cognition.AgentObservation RefreshOfferedPlans(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.RefreshOfferedPlans(observation);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Society.SocietyDeferredCognitionRequest> PreviewModelRequests(global::System.Collections.Generic.IReadOnlySet<string> excludedIds, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, bool>? observationIsCurrent, global::System.Func<global::ClankerWorld.Simulation.Cognition.AgentObservation, global::ClankerWorld.Simulation.Cognition.AgentObservation>? refreshObservation = null) => Runtime.society.PreviewModelRequests(excludedIds, observationIsCurrent, refreshObservation);
        public global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome> ScheduleModelWork(global::ClankerWorld.Simulation.Cognition.ModelWorkKind kind, string actorId, string requestId, global::System.Func<global::System.Threading.Tasks.Task<global::ClankerWorld.Simulation.World.CognitionSystem.ModelDecisionOutcome>> work, global::ClankerWorld.Simulation.Cognition.CognitionDecisionRequest? decision = null, global::ClankerWorld.Simulation.Cognition.AgentConversationTurnRequest? conversation = null, bool startInline = false) => Runtime.ScheduleModelWork(kind, actorId, requestId, work, decision, conversation, startInline);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => Runtime.AppendEvent(payload, eventPosition);
    }
}
