using FarmStockChoice = ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice;
using MaterialGatherEffect = ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IDeliveryOrderExecutionWorld
{
    int HouseHaulLoadQuantity { get; }
    DeclarativeWorldContentState Content { get; }
    SocietyCheckpoint Society { get; }
    WorldContentSimulationState Simulation { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    string HouseholdFor(string actor);
    ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    bool DeliveryDestinationMatches(OrderTask order, PlacedBuilding building);
    ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSpareFood(string actor, string? itemKind = null, string? sourceLotId = null);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    int AvailableLotQuantity(InventoryLot lot);
    ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    ClankerWorld.Simulation.World.PlacedBuilding? FarmStorageFor(string householdId, string kind, string? requestedBuildingId = null, Func<PlacedBuilding, bool>? fitsHaul = null);
    ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmGrainForDelivery(string householdId, string actor, string? itemKind = null, string? destinationId = null, int maximumQuantity = 2147483647);
    ClankerWorld.Simulation.World.WorldRuntime.FarmStockHaulPlan? PlanFarmStockHaul(string actor, string destinationId, FarmStockChoice choice, int maximumQuantity = 2147483647);
    ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmFlourForHouse(string householdId, string farmhouseId, string actor, string houseId, int maximumQuantity = 2147483647);
    ClankerWorld.Simulation.Kernel.InventoryLot? UnlocatedHouseholdStock(string householdId, string? actor = null, string? houseId = null, string? itemKind = null, int maximumQuantity = 2147483647);
    int HouseHaulPickupQuantity(string actor, InventoryLot stock, string destinationId);
    int DeliveryResourceQuantity(InventoryLot carrier, string kind);
    int BlacksmithInputTarget(string blacksmithId, string itemKind);
    bool UnusableDeliveryStock(InventoryCheckpoint inventory, InventoryLot root);
    bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId);
    int DeliveryOrderRemaining(OrderTask order);
    int HouseDeliveryPhysicalQuantity(InventoryCheckpoint inventory, InventoryLot carried);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? DeliveryPlanFor(OwnerQueuedInstruction instruction, AgentState person);
    (ClankerWorld.Simulation.World.PlacedBuilding Building, string Route, ClankerWorld.Simulation.World.MapResource Source)? DeliveryGatherFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, AgentState state, string itemKind, MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, GridPoint? returnTo = null, int returnRange = 0);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string StockDeliveryReceipt(string operation);
    bool IsAdult(string actor);
    string? TownForResident(string residentId);
    bool ReadyForBriefInteraction(string actor);
    void TransferOrderDelivery(string actor, DeliveryOrderPlan plan, string operation);
    void MarkCheckpointCurrent();
}
