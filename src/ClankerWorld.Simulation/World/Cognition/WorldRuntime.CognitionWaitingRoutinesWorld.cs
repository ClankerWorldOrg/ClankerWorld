namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionWaitingRoutinesWorld CognitionWaitingRoutinesWorld => new CognitionWaitingRoutinesAdapter(this);

    private sealed class CognitionWaitingRoutinesAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionWaitingRoutinesWorld
    {
        public bool HasGuardianPlacementTask(string actor) => Runtime.HasGuardianPlacementTask(actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state) => Runtime.TryApplyWaitingRoutine(id, state);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsWaitingSurvivalCandidate(string id) => WorldRuntime.IsWaitingSurvivalCandidate(id);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => Runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool IsAdult(string actor) => Runtime.IsAdult(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.AgentState> ChildrenNeedingCare(string actor) => Runtime.ChildrenNeedingCare(actor);
        public global::System.Collections.Generic.IEnumerable<string> IllDependentsNeedingCare(string adult) => Runtime.IllDependentsNeedingCare(adult);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => Runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public void CareForChild(string actor, string childId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.CareForChild(actor, childId, originalPlan);
        public void TendToIllDependent(string adult, string dependent, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.TendToIllDependent(adult, dependent, originalPlan);
        public bool IsTownProjectCandidate(string id) => WorldRuntime.IsTownProjectCandidate(id);
        public global::ClankerWorld.Simulation.World.AgentKnowledgeWritingProject? KnowledgeWritingFor(string actor) => Runtime.KnowledgeWritingFor(actor);
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
