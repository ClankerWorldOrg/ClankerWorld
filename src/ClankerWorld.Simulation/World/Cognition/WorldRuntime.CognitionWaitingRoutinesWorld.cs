namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionWaitingRoutinesWorld CognitionWaitingRoutinesWorld => new CognitionWaitingRoutinesAdapter(this);

    private sealed class CognitionWaitingRoutinesAdapter(WorldRuntime runtime) : ICognitionWaitingRoutinesWorld
    {
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool HasGuardianPlacementTask(string actor) => runtime.HasGuardianPlacementTask(actor);
        public global::ClankerWorld.Simulation.World.FamilyTurnState GuardianPlacementActions => runtime.guardianPlacementActions;
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state) => CognitionSystem.TryApplyWaitingRoutine(runtime.CognitionWaitingRoutinesWorld, id, state);
        public global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions => runtime.withheldActions;
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public bool IsWaitingSurvivalCandidate(string id) => WorldRuntime.IsWaitingSurvivalCandidate(id);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentState> ChildrenNeedingCare(string actor) => runtime.ChildrenNeedingCare(actor);
        public global::System.Collections.Generic.IEnumerable<string> IllDependentsNeedingCare(string adult) => runtime.IllDependentsNeedingCare(adult);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public void CareForChild(string actor, string childId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.CareForChild(actor, childId, originalPlan);
        public void TendToIllDependent(string adult, string dependent, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.TendToIllDependent(adult, dependent, originalPlan);
        public bool IsTownProjectCandidate(string id) => WorldRuntime.IsTownProjectCandidate(id);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => runtime.KnowledgeWritingFor(actor);
        public string ExplorationGoalCandidateId(global::ClankerWorld.Simulation.World.ExplorationGoal goal) => WorldRuntime.ExplorationGoalCandidateId(goal);
        public string GatherBlacksmithInputPrefix => WorldRuntime.GatherBlacksmithInputPrefix;
        public string GatherBuildingMaterialPrefix => WorldRuntime.GatherBuildingMaterialPrefix;
        public string RareMiningPrefix => WorldRuntime.RareMiningPrefix;
        public string SupplyWorkstationPrefix => WorldRuntime.SupplyWorkstationPrefix;
        public string ReturnEmptyVesselPrefix => WorldRuntime.ReturnEmptyVesselPrefix;
        public string FillWaterJugPrefix => WorldRuntime.FillWaterJugPrefix;
        public string PullCartPrefix => WorldRuntime.PullCartPrefix;
    }
}
