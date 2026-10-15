using FarmStockChoice = ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice;
using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IDeliveryOrderExecutionWorld DeliveryOrderExecutionWorld => new DeliveryOrderExecutionAdapter(this);

    private sealed class DeliveryOrderExecutionAdapter(WorldRuntime runtime) : IDeliveryOrderExecutionWorld
    {
        public int HouseHaulLoadQuantity => WorldRuntime.HouseHaulLoadQuantity;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public OrderStore Orders => runtime.orderStore;
        public long WorldTick => runtime.WorldTick;
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public bool DeliveryDestinationMatches(OrderTask order, PlacedBuilding building) => runtime.DeliveryDestinationMatches(order, building);
        public ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSpareFood(string actor, string? itemKind = null, string? sourceLotId = null) => runtime.PersonalSpareFood(actor, itemKind, sourceLotId);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public ClankerWorld.Simulation.World.PlacedBuilding? FarmStorageFor(string householdId, string kind, string? requestedBuildingId = null, Func<PlacedBuilding, bool>? fitsHaul = null) => runtime.FarmStorageFor(householdId, kind, requestedBuildingId, fitsHaul);
        public ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmGrainForDelivery(string householdId, string actor, string? itemKind = null, string? destinationId = null, int maximumQuantity = 2147483647) => runtime.FarmGrainForDelivery(householdId, actor, itemKind, destinationId, maximumQuantity);
        public ClankerWorld.Simulation.World.WorldRuntime.FarmStockHaulPlan? PlanFarmStockHaul(string actor, string destinationId, FarmStockChoice choice, int maximumQuantity = 2147483647) => runtime.PlanFarmStockHaul(actor, destinationId, choice, maximumQuantity);
        public ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmFlourForHouse(string householdId, string farmhouseId, string actor, string houseId, int maximumQuantity = 2147483647) => runtime.FarmFlourForHouse(householdId, farmhouseId, actor, houseId, maximumQuantity);
        public ClankerWorld.Simulation.Kernel.InventoryLot? UnlocatedHouseholdStock(string householdId, string? actor = null, string? houseId = null, string? itemKind = null, int maximumQuantity = 2147483647) => runtime.UnlocatedHouseholdStock(householdId, actor, houseId, itemKind, maximumQuantity);
        public int HouseHaulPickupQuantity(string actor, InventoryLot stock, string destinationId) => runtime.HouseHaulPickupQuantity(actor, stock, destinationId);
        public int DeliveryResourceQuantity(InventoryLot carrier, string kind) => runtime.DeliveryResourceQuantity(carrier, kind);
        public int BlacksmithInputTarget(string blacksmithId, string itemKind) => runtime.BlacksmithInputTarget(blacksmithId, itemKind);
        public bool UnusableDeliveryStock(InventoryCheckpoint inventory, InventoryLot root) => WorldRuntime.UnusableDeliveryStock(inventory, root);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int DeliveryOrderRemaining(OrderTask order) => WorldRuntime.DeliveryOrderRemaining(order);
        public int HouseDeliveryPhysicalQuantity(InventoryCheckpoint inventory, InventoryLot carried) => WorldRuntime.HouseDeliveryPhysicalQuantity(inventory, carried);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? DeliveryPlanFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.DeliveryPlanFor(instruction, person);
        public (ClankerWorld.Simulation.World.PlacedBuilding Building, string Route, ClankerWorld.Simulation.World.MapResource Source)? DeliveryGatherFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.DeliveryGatherFor(instruction, person);
        public ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId) => runtime.InterruptOrdinaryFieldWorkForCustody(actor, lotId);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0) => runtime.GatherProjectMaterial(agentId, state, itemKind, source, useHarvestBonus, deliveryBuildingId, returnTo, returnRange);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string StockDeliveryReceipt(string operation) => WorldRuntime.StockDeliveryReceipt(operation);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public string? TownForResident(string residentId) => runtime.TownForResident(residentId);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public void TransferOrderDelivery(string actor, DeliveryOrderPlan plan, string operation) => runtime.ApplyInventoryTransition(inventory => plan.TakeFromContainer
            ? InventoryAuthority.TakeFromContainer(inventory, operation, plan.Carrier.OwnerId, actor,
                plan.Carrier.Id, plan.Resource.Id, plan.TransferQuantity, destinationDeliveryBuildingId: plan.Destination.InstanceId, holdAccess: InventoryHoldRules.BorrowedBy(actor))
            : InventoryAuthority.Transfer(inventory, operation, plan.Carrier.OwnerId,
                plan.DirectDelivery ? plan.DestinationOwnerId : actor, plan.Carrier.Id, plan.TransferQuantity,
                plan.DirectDelivery ? "owner_stock_delivered" : "owner_stock_picked_up",
                destinationStorageBuildingId: plan.DirectDelivery ? plan.Destination.InstanceId : null,
                destinationDeliveryBuildingId: plan.DirectDelivery ? null : plan.Destination.InstanceId, holdAccess: InventoryHoldRules.DeliveryFor(actor, plan.DirectDelivery ? plan.Destination.InstanceId : null)));
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
