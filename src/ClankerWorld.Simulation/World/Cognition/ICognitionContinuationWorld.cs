namespace ClankerWorld.Simulation.World;

internal interface ICognitionContinuationWorld
{
    global::ClankerWorld.Simulation.Society.SocietyWorldCapture Capture();
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::System.Collections.Generic.IReadOnlySet<string>? WithheldActions { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::System.Collections.Generic.Dictionary<string, string>? AttemptedActions { get; }
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.Cognition.CognitionCandidate> CreateCandidates(string agentId, global::ClankerWorld.Simulation.World.AgentState state, bool restrictForOrder = true);
    void ApplyCandidate(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string candidateId, bool reportIdle);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    bool TryApplyWaitingRoutine(string id, global::ClankerWorld.Simulation.World.AgentState state);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    bool ContinueGuardianPlacement(string actor);
    bool IsConversationBusy(string agentId);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? OrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? UrgentSurvivalCandidateFor(string agentId, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction = null);
    bool ShouldInterruptOrder(global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? orderCandidate, global::ClankerWorld.Simulation.Cognition.CognitionCandidate? urgentCandidate);
    bool IsMarketFoodCandidate(string actor, string candidateId);
    void ContinueMarketWalk(string actor, string id);
    void ExecuteOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Cognition.CognitionCandidate candidate);
    string OrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanContinueEquipmentRepair(string actor);
    global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    bool CanContinueLesson(string actor, bool waitingForModel = false);
    void ContinueLesson(string actor, bool waitingForModel = false, global::ClankerWorld.Simulation.World.LearningActionPlan? originalPlan = null);
    bool ContinueFarmWork(string workerId);
    bool CanContinueProject(global::ClankerWorld.Simulation.World.AgentState state, bool waitingForModel = false);
    void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state);
    string NonLandRemedyPrefix { get; }
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    bool ApplyNonLandRemedyAction(global::ClankerWorld.Simulation.World.TownRuntimeState town, string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? plan = null);
    string NonLandRelayPrefix { get; }
    void ContinueTownCivicVisit(string actor, string candidate);
    void ContinueTownLandSaleWalk(string actor, string candidate);
    bool IsTownProjectDonationCandidate(string id);
    void ContinueTownProjectDonationWalk(string actor, string id);
    bool IsMarketCandidate(string id);
    bool IsOrnamentCandidate(string candidate);
    void ContinueOrnamentWalk(string actor, string candidate);
    bool CanContinuePendingTask(global::ClankerWorld.Simulation.World.AgentState state, string id);
}
