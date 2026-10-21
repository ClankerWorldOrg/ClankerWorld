using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

internal interface IExpansionOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    string ExpansionOwner(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    string HouseholdFor(string actor);
    string? TownForResident(string residentId);
    bool ExpansionJobMatchesOrder(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.Generic.IEnumerable<(global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool CanFitExpansion(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true);
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? ExpansionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool MayExpandBuilding(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, out string failure);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    (global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint) ExpansionShapeForOrder(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction);
    bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null);
    bool CanAcquireExpansionMaterials(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> costs);
    global::ClankerWorld.Simulation.World.BuildingExpansionJob? BoundExpansionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool ExpansionOrderInputsAvailable(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.World.OrderSystem.ExpansionOrderPlan? ExpansionPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    OrderStore Orders { get; }
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    void PauseExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ExpansionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void ApplyBuildingExpansionCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string buildingId, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? orderInstruction = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool IsActiveOrder(string status);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsConversationBusy(string agentId);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string ExpansionOrderReceipt(string jobId);
    void MarkCheckpointCurrent();
    void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline);
    void HoldOrderReservations(IReadOnlyList<Identity> reservations);
    void ReleaseExpansionOrderInputs(BuildingExpansionJob job);
    void ResumeExpansion(string job, long deadline);
    void PauseExpansion(string job, long tick);
    void CancelExpansions(IReadOnlySet<string> jobs, string reason);
}
