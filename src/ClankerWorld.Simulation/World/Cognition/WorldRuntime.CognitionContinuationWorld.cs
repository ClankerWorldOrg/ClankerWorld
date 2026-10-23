namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICognitionContinuationWorld CognitionContinuationWorld => new CognitionContinuationAdapter(this);

    private sealed class CognitionContinuationAdapter(WorldRuntime runtime) : ICognitionContinuationWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture() => runtime.society.Capture();
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions => runtime.withheldActions;
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::System.Collections.Generic.Dictionary<string, string>? AttemptedActions => runtime.attemptedActions;
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true) => runtime.CreateCandidates(agentId, state, restrictForOrder);
        public void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle) => runtime.ApplyCandidate(agentId, state, candidateId, reportIdle);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state) => runtime.TryApplyWaitingRoutine(id, state);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public bool ContinueGuardianPlacement(string actor) => runtime.ContinueGuardianPlacement(actor);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.OrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null) => runtime.UrgentSurvivalCandidateFor(agentId, person, instruction);
        public bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate) => runtime.ShouldInterruptOrder(person, instruction, orderCandidate, urgentCandidate);
        public bool IsMarketFoodCandidate(string actor, string candidateId) => runtime.IsMarketFoodCandidate(actor, candidateId);
        public void ContinueMarketWalk(string actor, string id) => runtime.ContinueMarketWalk(actor, id);
        public void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate) => runtime.ExecuteOrderStep(instruction, person, candidate);
        public string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.OrderBlockedReason(instruction, person);
        public bool CanContinueEquipmentRepair(string actor) => runtime.CanContinueEquipmentRepair(actor);
        public global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public bool CanContinueLesson(string actor, bool waitingForModel = false) => runtime.CanContinueLesson(actor, waitingForModel);
        public void ContinueLesson(string actor, bool waitingForModel = false, global::ClankerWorld.Simulation.World.LearningActionPlan? originalPlan = null) => runtime.ContinueLesson(actor, waitingForModel, originalPlan);
        public bool ContinueFarmWork(string workerId) => runtime.farmingSystem.ContinueFarmWork(runtime.farmingWorld, workerId);
        public bool CanContinueProject(global::ClankerWorld.Simulation.World.AgentState state, bool waitingForModel = false) => runtime.CanContinueProject(state, waitingForModel);
        public void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state) => runtime.ContinueProject(agentId, state);
        public string NonLandRemedyPrefix => WorldRuntime.NonLandRemedyPrefix;
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns => runtime.towns;
        public bool ApplyNonLandRemedyAction(global::ClankerWorld.Simulation.World.TownRuntimeState town, string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? plan = null) => runtime.ApplyNonLandRemedyAction(town, actor, candidateId, plan);
        public string NonLandRelayPrefix => WorldRuntime.NonLandRelayPrefix;
        public void ContinueTownCivicVisit(string actor, string candidate) => runtime.ContinueTownCivicVisit(actor, candidate);
        public void ContinueTownLandSaleWalk(string actor, string candidate) => runtime.ContinueTownLandSaleWalk(actor, candidate);
        public bool IsTownProjectDonationCandidate(string id) => WorldRuntime.IsTownProjectDonationCandidate(id);
        public void ContinueTownProjectDonationWalk(string actor, string id) => runtime.ContinueTownProjectDonationWalk(actor, id);
        public bool IsMarketCandidate(string id) => WorldRuntime.IsMarketCandidate(id);
        public bool IsOrnamentCandidate(string candidate) => WorldRuntime.IsOrnamentCandidate(candidate);
        public void ContinueOrnamentWalk(string actor, string candidate) => runtime.ContinueOrnamentWalk(actor, candidate);
        public bool CanContinuePendingTask(global::ClankerWorld.Simulation.World.AgentState state, string id) => runtime.CanContinuePendingTask(state, id);
    }
}
