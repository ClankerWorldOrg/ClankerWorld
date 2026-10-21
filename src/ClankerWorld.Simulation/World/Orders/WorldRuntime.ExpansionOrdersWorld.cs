using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IExpansionOrdersWorld ExpansionOrdersWorld => new ExpansionOrdersAdapter(this);

    private sealed class ExpansionOrdersAdapter(WorldRuntime runtime) : IExpansionOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public string ExpansionOwner(global::ClankerWorld.Simulation.World.PlacedBuilding building) => WorldRuntime.ExpansionOwner(building);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public bool ExpansionJobMatchesOrder(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.ExpansionJobMatchesOrder(job, instruction);
        public global::System.Collections.Generic.IEnumerable<(global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.ExpansionShapes(building);
        public bool CanFitExpansion(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true) => runtime.CanFitExpansion(building, position, footprint, out failure, ownJobId, requireLandRights);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? ExpansionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ExpansionOrderBuilding(instruction);
        public bool MayExpandBuilding(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, out string failure) => runtime.MayExpandBuilding(actor, building, out failure);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public (global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint) ExpansionShapeForOrder(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => runtime.ExpansionShapeForOrder(building, instruction);
        public bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null) => runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool CanAcquireExpansionMaterials(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> costs) => runtime.CanAcquireExpansionMaterials(actor, building, costs);
        public global::ClankerWorld.Simulation.World.BuildingExpansionJob? BoundExpansionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.BoundExpansionJob(instruction);
        public bool ExpansionOrderInputsAvailable(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.ExpansionOrderInputsAvailable(job, building);
        public global::ClankerWorld.Simulation.World.OrderSystem.ExpansionOrderPlan? ExpansionPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExpansionPlanFor(instruction, person);
        public OrderStore Orders => runtime.orderStore;
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person) => runtime.NeedsUrgentWarmth(person);
        public void PauseExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.PauseExpansionForOrder(instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExpansionOrderCandidateFor(instruction, person);
        public string ExpansionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ExpansionOrderBlockedReason(instruction);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public long WorldTick => runtime.WorldTick;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void ApplyBuildingExpansionCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string buildingId, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? orderInstruction = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyBuildingExpansionCandidate(actor, state, buildingId, orderInstruction, originalPlan);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(global::ClankerWorld.Simulation.World.PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.CancelExpansionForOrder(instruction);
        public bool IsConversationBusy(string agentId) => runtime.IsConversationBusy(agentId);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string ExpansionOrderReceipt(string jobId) => WorldRuntime.ExpansionOrderReceipt(jobId);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.SetReservationDeadline(inventory, reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<Identity> reservations) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.HoldReservations(inventory, reservations));
        public void ReleaseExpansionOrderInputs(BuildingExpansionJob job) => runtime.ApplyInventoryTransition(inventory =>
        {
            foreach (var id in job.InputReservationIds)
                if (inventory.Reservations.SingleOrDefault(item => item.Id == id) is
                    { State: InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed })
                    inventory = InventoryAuthority.ReleaseReservation(inventory, id, "owner_expansion_stopped");
            return inventory;
        });
        public void ResumeExpansion(string job, long deadline) => runtime.constructionSystem.Buildings.ResumeExpansion(job, deadline);
        public void PauseExpansion(string job, long tick) => runtime.constructionSystem.Buildings.PauseExpansion(job, tick);
        public void CancelExpansions(IReadOnlySet<string> jobs, string reason) => runtime.constructionSystem.Buildings.CancelPausedExpansions(jobs, reason);
    }
}
