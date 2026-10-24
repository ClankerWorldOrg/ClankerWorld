using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IDeliveryOrderExecutionWorld DeliveryOrderExecutionWorld => new DeliveryOrderExecutionAdapter(this);

    private sealed class DeliveryOrderExecutionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IDeliveryOrderExecutionWorld
    {
        public bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building) => Runtime.DeliveryDestinationMatches(order, building);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSpareFood(string actor, string? itemKind = null, string? sourceLotId = null) => Runtime.PersonalSpareFood(actor, itemKind, sourceLotId);
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => Runtime.DestinationRoom(buildingId, movingRoot);
        public int HouseHaulLoadQuantity => WorldRuntime.HouseHaulLoadQuantity;
        public int AvailableLotQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.AvailableLotQuantity(lot);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? FarmStorageFor(string householdId, string kind, string? requestedBuildingId = null, global::System.Func<global::ClankerWorld.Simulation.World.PlacedBuilding, bool>? fitsHaul = null) => Runtime.FarmStorageFor(householdId, kind, requestedBuildingId, fitsHaul);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmGrainForDelivery(string householdId, string actor, string? itemKind = null, string? destinationId = null, int maximumQuantity = 2147483647) => Runtime.FarmGrainForDelivery(householdId, actor, itemKind, destinationId, maximumQuantity);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockHaulPlan? PlanFarmStockHaul(string actor, string destinationId, global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice choice, int maximumQuantity = 2147483647) => Runtime.PlanFarmStockHaul(actor, destinationId, choice, maximumQuantity);
        public global::ClankerWorld.Simulation.World.WorldRuntime.FarmStockChoice? FarmFlourForHouse(string householdId, string farmhouseId, string actor, string houseId, int maximumQuantity = 2147483647) => Runtime.FarmFlourForHouse(householdId, farmhouseId, actor, houseId, maximumQuantity);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? UnlocatedHouseholdStock(string householdId, string? actor = null, string? houseId = null, string? itemKind = null, int maximumQuantity = 2147483647) => Runtime.UnlocatedHouseholdStock(householdId, actor, houseId, itemKind, maximumQuantity);
        public int HouseHaulPickupQuantity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, string destinationId) => Runtime.HouseHaulPickupQuantity(actor, stock, destinationId);
        public int DeliveryResourceQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot carrier, string kind) => Runtime.DeliveryResourceQuantity(carrier, kind);
        public int BlacksmithInputTarget(string blacksmithId, string itemKind) => Runtime.BlacksmithInputTarget(blacksmithId, itemKind);
        public string? DeliveryRouteForCarried(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding destination) => Runtime.DeliveryRouteForCarried(order, destination);
        public bool UnusableDeliveryStock(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot root) => WorldRuntime.UnusableDeliveryStock(inventory, root);
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.DeliveryOrderRemaining(order);
        public int HouseDeliveryPhysicalQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot carried) => WorldRuntime.HouseDeliveryPhysicalQuantity(inventory, carried);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? DeliveryPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.DeliveryPlanFor(instruction, person);
        public (global::ClankerWorld.Simulation.World.PlacedBuilding Building, string Route, global::ClankerWorld.Simulation.World.MapResource Source)? DeliveryGatherFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.DeliveryGatherFor(instruction, person);
        public int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public void InterruptOrdinaryFieldWorkForCustody(string actor, string lotId) => Runtime.InterruptOrdinaryFieldWorkForCustody(actor, lotId);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindDeliveryOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding destination, string ownerId, string route, string? lotId = null, int? quantity = null) => Runtime.BindDeliveryOrder(instruction, destination, ownerId, route, lotId, quantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryNativeEffect? ApplyDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan plan) => Runtime.ApplyDeliveryOrderPlan(actor, person, plan);
        public string StockDeliveryReceipt(string operation) => WorldRuntime.StockDeliveryReceipt(operation);
        public void TransferOrderDelivery(string actor, DeliveryOrderPlan plan, string operation) => Runtime.RunInventoryCommand(InventorySystem.TransferOrderDelivery(plan, operation, actor));
    }
}
