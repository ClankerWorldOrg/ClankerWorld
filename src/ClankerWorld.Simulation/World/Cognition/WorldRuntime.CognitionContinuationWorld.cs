namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionContinuationWorld CognitionContinuationWorld => new CognitionContinuationAdapter(this);

    private sealed class CognitionContinuationAdapter(WorldRuntime runtime) : AgentRuntimeAdapter(runtime), ICognitionContinuationWorld
    {
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => Runtime.PassengerBoat(actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => Runtime.PendingInstructionFor(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => Runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state) => Runtime.TryApplyWaitingRoutine(id, state);
        public bool ContinueGuardianPlacement(string actor) => Runtime.ContinueGuardianPlacement(actor);
        public bool IsConversationBusy(string agentId) => Runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null) => Runtime.UrgentSurvivalCandidateFor(agentId, person, instruction);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => Runtime.IsMarketFoodCandidate(actor, candidateId);
        public void ContinueMarketWalk(string actor, string id) => Runtime.ContinueMarketWalk(actor, id);
        public void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => Runtime.ExecuteOrderStep(instruction, person, candidate);
        public global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => Runtime.ContinueEquipmentRepair(actor);
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => Runtime.CanContinueLesson(actor, waitingForModel);
        public void ContinueLesson(string actor, bool waitingForModel = false, global::ClankerWorld.Simulation.World.LearningActionPlan? originalPlan = null) => Runtime.ContinueLesson(actor, waitingForModel, originalPlan);
        public bool ContinueFarmWork(string workerId) => Runtime.ContinueFarmWork(workerId);
        public void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state) => Runtime.ContinueProject(agentId, state);
        public string NonLandRemedyPrefix => WorldRuntime.NonLandRemedyPrefix;
        public string NonLandRelayPrefix => WorldRuntime.NonLandRelayPrefix;
        public void ContinueTownCivicVisit(string actor, string candidate) => Runtime.ContinueTownCivicVisit(actor, candidate);
        public void ContinueTownLandSaleWalk(string actor, string candidate) => Runtime.ContinueTownLandSaleWalk(actor, candidate);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public void ContinueTownProjectDonationWalk(string actor, string id) => Runtime.ContinueTownProjectDonationWalk(actor, id);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool IsOrnamentCandidate(string candidate) => WorldRuntime.IsOrnamentCandidate(candidate);
        public void ContinueOrnamentWalk(string actor, string candidate) => Runtime.ContinueOrnamentWalk(actor, candidate);
        public bool CanContinuePendingTask(global::ClankerWorld.Simulation.World.AgentState state, string id) => Runtime.CanContinuePendingTask(state, id);
    }
}
