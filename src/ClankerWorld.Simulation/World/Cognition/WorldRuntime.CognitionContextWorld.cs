namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionContextWorld CognitionContextWorld => new CognitionContextAdapter(this);

    private sealed class CognitionContextAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionContextWorld
    {
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool IsFinishedOrderDecisionAwaitingReply(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => Runtime.IsFinishedOrderDecisionAwaitingReply(observation);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public bool HasNewObserverGuidanceFor(string agentId) => OrderSystem.HasNewObserverGuidanceFor(Runtime.ControlWorld, agentId);
        public long CognitionReevaluationIntervalTicks => WorldRuntime.CognitionReevaluationIntervalTicks;
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => CommunitySystem.CanContinueLesson(Runtime.CommunityLearningWorld, actor, waitingForModel);
        public string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null) => CognitionSystem.DecisionContext(Runtime.CognitionContextWorld, state, candidates, conversationChoiceContext);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public string MedicalAllowPrefix => global::ClankerWorld.Simulation.World.MedicalConsentChoice.AllowPrefix;
    }
}
