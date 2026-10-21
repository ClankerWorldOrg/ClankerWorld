namespace ClankerWorld.Simulation.World;

internal interface ICognitionContextWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool IsFinishedOrderDecisionAwaitingReply(global::ClankerWorld.Simulation.Cognition.AgentObservation observation);
    OrderStore Orders { get; }
    bool IsInstructionComplete(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string? UnderstoodTaskFor(string? candidate);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool HasNewObserverGuidanceFor(string agentId);
    long WorldTick { get; }
    long CognitionReevaluationIntervalTicks { get; }
    bool CanContinueEquipmentRepair(string actor);
    bool CanContinueLesson(string actor, bool waitingForModel = false);
    bool CanContinueProject(global::ClankerWorld.Simulation.World.AgentState state, bool waitingForModel = false);
    string DecisionContext(global::ClankerWorld.Simulation.World.AgentState state, global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> candidates, string? conversationChoiceContext = null);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    string MedicalAllowPrefix { get; }
}
