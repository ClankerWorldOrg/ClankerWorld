namespace ClankerWorld.Simulation.World;

internal interface IProductionOrdersWorld
{
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.PlacedBuilding? ProductionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool IsHouseholdBuildingTag(string tag);
    OrderStore Orders { get; }
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ProductionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void PauseProductionOrderJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.WorldProductionJob job);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool CanResumeHeldInputs(string actor, string buildingId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Identity> reservationIds);
    bool TryResumeKnife(string actor, string jobId, out string? knifeId);
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project);
    void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool IsActiveOrder(string status);
    bool IsAdult(string actor);
    void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsConversationBusy(string agentId);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    string ProductionOrderReceipt(string jobId);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void MarkCheckpointCurrent();
    void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline);
    void HoldOrderReservations(IReadOnlyList<Identity> reservations);
    void ReleaseProductionOrderInputs(WorldProductionJob job);
    void ResumeProductionJob(string job, long deadline, string? knife);
    void PauseProductionJob(string job, long tick);
    void CancelProductionJobs(IReadOnlySet<string> jobs);
}
