namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionContextWorld CognitionContextWorld => new CognitionContextAdapter(this);

    private sealed class CognitionContextAdapter(WorldRuntime runtime) : ICognitionContextWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool IsFinishedOrderDecisionAwaitingReply(global::ClankerWorld.Simulation.Cognition.AgentObservation observation) => runtime.IsFinishedOrderDecisionAwaitingReply(observation);
        public OrderStore Orders => runtime.orderStore;
        public bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.IsInstructionComplete(instruction);
        public string? UnderstoodTaskFor(string? candidate) => WorldRuntime.UnderstoodTaskFor(candidate);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool HasNewObserverGuidanceFor(string agentId) => runtime.HasNewObserverGuidanceFor(agentId);
        public long WorldTick => runtime.WorldTick;
        public long CognitionReevaluationIntervalTicks => WorldRuntime.CognitionReevaluationIntervalTicks;
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => CommunitySystem.CanContinueLesson(runtime.CommunityLearningWorld, actor, waitingForModel);
        public bool CanContinueProject(global::ClankerWorld.Simulation.World.AgentState state, bool waitingForModel = false) => runtime.CanContinueProject(state, waitingForModel);
        public string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null) => runtime.DecisionContext(state, candidates, conversationChoiceContext);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public string MedicalAllowPrefix => WorldRuntime.MedicalAllowPrefix;
    }
}
