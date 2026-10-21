namespace ClankerWorld.Simulation.World;

internal interface ICognitionWaitingRoutinesWorld
{
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool HasGuardianPlacementTask(string actor);
    global::ClankerWorld.Simulation.World.FamilyTurnState GuardianPlacementActions { get; }
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state);
    global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions { get; }
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    bool IsWaitingSurvivalCandidate(string id);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentState> ChildrenNeedingCare(string actor);
    global::System.Collections.Generic.IEnumerable<string> IllDependentsNeedingCare(string adult);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    void CareForChild(string actor, string childId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void TendToIllDependent(string adult, string dependent, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    bool IsTownProjectCandidate(string id);
    global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor);
    string ExplorationGoalCandidateId(global::ClankerWorld.Simulation.World.ExplorationGoal goal);
    string GatherBlacksmithInputPrefix { get; }
    string GatherBuildingMaterialPrefix { get; }
    string RareMiningPrefix { get; }
    string SupplyWorkstationPrefix { get; }
    string ReturnEmptyVesselPrefix { get; }
    string FillWaterJugPrefix { get; }
    string PullCartPrefix { get; }
}
