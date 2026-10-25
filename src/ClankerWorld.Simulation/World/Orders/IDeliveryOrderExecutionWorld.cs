using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
namespace ClankerWorld.Simulation.World;

internal interface IDeliveryOrderExecutionWorld
{
    string HouseholdFor(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSpareFood(string actor, string? itemKind = null, string? sourceLotId = null);
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    int HouseHaulLoadQuantity { get; }
    int AvailableLotQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    global::ClankerWorld.Simulation.World.PlacedBuilding? FarmStorageFor(string householdId, string kind, string? requestedBuildingId = null, global::System.Func<global::ClankerWorld.Simulation.World.PlacedBuilding, bool>? fitsHaul = null);
    global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmGrainForDelivery(string householdId, string actor, string? itemKind = null, string? destinationId = null, int maximumQuantity = 2147483647);
    global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockHaulPlan? PlanFarmStockHaul(string actor, string destinationId, global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice choice, int maximumQuantity = 2147483647);
    global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmFlourForHouse(string householdId, string farmhouseId, string actor, string houseId, int maximumQuantity = 2147483647);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? UnlocatedHouseholdStock(string householdId, string? actor = null, string? houseId = null, string? itemKind = null, int maximumQuantity = 2147483647);
    int HouseHaulPickupQuantity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, string destinationId);
    int DeliveryResourceQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot carrier, string kind);
    int BlacksmithInputTarget(string blacksmithId, string itemKind);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    string? DeliveryRouteForCarried(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding destination);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool UnusableDeliveryStock(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot root);
    bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order);
    int HouseDeliveryPhysicalQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot carried);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? DeliveryPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    (global::ClankerWorld.Simulation.World.PlacedBuilding Building, string Route, global::ClankerWorld.Simulation.World.MapResource Source)? DeliveryGatherFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    OrderStore Orders { get; }
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindDeliveryOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding destination, string ownerId, string route, string? lotId = null, int? quantity = null);
    global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string DeliveryOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryNativeEffect? ApplyDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan plan);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string StockDeliveryReceipt(string operation);
    bool IsAdult(string actor);
    string? TownForResident(string residentId);
    bool ReadyForBriefInteraction(string actor);
    void MarkCheckpointCurrent();
    void TransferOrderDelivery(string actor, DeliveryOrderPlan plan, string operation);
}
