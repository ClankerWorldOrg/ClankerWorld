using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IDeliveryOrderExecutionWorld DeliveryOrderExecutionWorld => new DeliveryOrderExecutionAdapter(this);

    private sealed class DeliveryOrderExecutionAdapter(WorldRuntime runtime) : IDeliveryOrderExecutionWorld
    {
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.DeliveryDestinationMatches(order, building);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSpareFood(string actor, string? itemKind = null, string? sourceLotId = null) => runtime.PersonalSpareFood(actor, itemKind, sourceLotId);
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public int HouseHaulLoadQuantity => WorldRuntime.HouseHaulLoadQuantity;
        public int AvailableLotQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? FarmStorageFor(string householdId, string kind, string? requestedBuildingId = null, global::System.Func<global::ClankerWorld.Simulation.World.PlacedBuilding, bool>? fitsHaul = null) => runtime.FarmStorageFor(householdId, kind, requestedBuildingId, fitsHaul);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmGrainForDelivery(string householdId, string actor, string? itemKind = null, string? destinationId = null, int maximumQuantity = 2147483647) => runtime.FarmGrainForDelivery(householdId, actor, itemKind, destinationId, maximumQuantity);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockHaulPlan? PlanFarmStockHaul(string actor, string destinationId, global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice choice, int maximumQuantity = 2147483647) => runtime.PlanFarmStockHaul(actor, destinationId, choice, maximumQuantity);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmFlourForHouse(string householdId, string farmhouseId, string actor, string houseId, int maximumQuantity = 2147483647) => runtime.FarmFlourForHouse(householdId, farmhouseId, actor, houseId, maximumQuantity);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? UnlocatedHouseholdStock(string householdId, string? actor = null, string? houseId = null, string? itemKind = null, int maximumQuantity = 2147483647) => runtime.UnlocatedHouseholdStock(householdId, actor, houseId, itemKind, maximumQuantity);
        public int HouseHaulPickupQuantity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, string destinationId) => runtime.HouseHaulPickupQuantity(actor, stock, destinationId);
        public int DeliveryResourceQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot carrier, string kind) => runtime.DeliveryResourceQuantity(carrier, kind);
        public int BlacksmithInputTarget(string blacksmithId, string itemKind) => runtime.BlacksmithInputTarget(blacksmithId, itemKind);
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation => runtime.worldSimulation;
        public string? DeliveryRouteForCarried(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding destination) => runtime.DeliveryRouteForCarried(order, destination);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public bool UnusableDeliveryStock(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot root) => WorldRuntime.UnusableDeliveryStock(inventory, root);
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.DeliveryOrderRemaining(order);
        public int HouseDeliveryPhysicalQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot carried) => WorldRuntime.HouseDeliveryPhysicalQuantity(inventory, carried);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? DeliveryPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.DeliveryPlanFor(instruction, person);
        public (global::ClankerWorld.Simulation.World.PlacedBuilding Building, string Route, global::ClankerWorld.Simulation.World.MapResource Source)? DeliveryGatherFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.DeliveryGatherFor(instruction, person);
        public OrderStore Orders => runtime.orderStore;
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId) => runtime.InterruptOrdinaryFieldWorkForCustody(actor, lotId);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public long WorldTick => runtime.WorldTick;
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindDeliveryOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding destination, string ownerId, string route, string? lotId = null, int? quantity = null) => runtime.BindDeliveryOrder(instruction, destination, ownerId, route, lotId, quantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string DeliveryOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.DeliveryOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryNativeEffect? ApplyDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan plan) => runtime.ApplyDeliveryOrderPlan(actor, person, plan);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string StockDeliveryReceipt(string operation) => WorldRuntime.StockDeliveryReceipt(operation);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
        public void TransferOrderDelivery(string actor, DeliveryOrderPlan plan, string operation) => runtime.ApplyInventoryTransition(inventory => plan.TakeFromContainer
            ? InventoryAuthority.TakeFromContainer(inventory, operation, plan.Carrier.OwnerId, actor,
                plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity, destinationDeliveryBuildingId: plan.Destination.InstanceId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
            : InventoryAuthority.Transfer(inventory, operation, plan.Carrier.OwnerId,
                plan.DirectDelivery ? plan.DestinationOwnerId : actor, plan.Carrier.Id, plan.TransferQuantity,
                plan.DirectDelivery ? "owner_stock_delivered" : "owner_stock_picked_up",
                destinationStorageBuildingId: plan.DirectDelivery ? plan.Destination.InstanceId : null,
                destinationDeliveryBuildingId: plan.DirectDelivery ? null : plan.Destination.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, plan.DirectDelivery ? plan.Destination.InstanceId : null)));
    }
}
