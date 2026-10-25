using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;
using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IBoatOrdersWorld BoatOrdersWorld => new BoatOrdersAdapter(this);

    private sealed class BoatOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IBoatOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.BoatTransportState BoatTransport => Runtime.boatTransport;
        public global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id) => Runtime.Port(id);
        public bool PortIsLegal(global::ClankerWorld.Simulation.World.PlacedBuilding port) => Runtime.PortIsLegal(port);
        public global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor) => TransportSystem.PassengerBoat(Runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor) => TransportSystem.WaitingBoatRequest(Runtime.BoatWorld, actor);
        public bool CanWalkForTownProject(string actor, global::ClankerWorld.Simulation.World.GridPoint from, global::ClankerWorld.Simulation.World.GridPoint target, int range = 0) => Runtime.CanWalkForTownProject(actor, from, target, range);
        public bool ReadyToBoard(string actor) => TransportSystem.ReadyToBoard(Runtime.BoatWorld, actor);
        public bool ApplyBoatCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyBoatCandidate(actor, candidateId, originalPlan);
        public global::ClankerWorld.Simulation.World.BoatTripRequest? BoundBoatRequest(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.BoundBoatRequest(instruction);
        public bool CanUseTownBoat(string townId, string actor) => TransportSystem.CanUseTownBoat(Runtime.BoatWorld, townId, actor);
        public global::ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(global::ClankerWorld.Simulation.World.PlacedBuilding port, string passenger) => TransportSystem.AvailablePortLanding(Runtime.BoatWorld, port, passenger);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> FreePortDocks(global::ClankerWorld.Simulation.World.PlacedBuilding port, string? exceptBoatId = null) => TransportSystem.FreePortDocks(Runtime.BoatWorld, port, exceptBoatId);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ActiveBoatOrder(global::ClankerWorld.Simulation.World.BoatTripRequest request) => Runtime.ActiveBoatOrder(request);
        public string BoatOrderArrivalEffectId(string requestId) => WorldRuntime.BoatOrderArrivalEffectId(requestId);
        public void CancelBoatRequest(BoatTripRequest request, long tick) => Runtime.transportSystem.CancelBoatRequest(request, tick);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor) => TransportSystem.BoatTripChoices(Runtime.BoatWorld, actor);
        public global::ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice choice, string? orderInstructionId = null) => TransportSystem.QueueBoatTrip(Runtime.BoatWorld, actor, choice, orderInstructionId);
    }

    private ICartCargoOrdersWorld CartCargoOrdersWorld => new CartCargoOrdersAdapter(this);

    private sealed class CartCargoOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), ICartCargoOrdersWorld
    {
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public bool IsCartCargoKind(string? kind) => WorldRuntime.IsCartCargoKind(kind);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => Runtime.ParseCartOrder(text, actor);
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int CargoQuantity(string cartId) => Runtime.CargoQuantity(cartId);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId) => Runtime.CartCargoLots(actor, cartId);
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public bool CanLoadCartLot(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => TransportSystem.CanLoadCartLot(Runtime.HandcartWorld, actor, person, lot);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CargoLotForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.CargoLotForOrder(instruction, person, cart);
        public int ContainerContentsQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.ContainerContentsQuantity(inventory, containerId);
        public string LoadCartPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.LoadPrefix;
        public string UnloadCartGroundPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.GroundPrefix;
        public string UnloadCartPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.UnloadPrefix;
        public string CartCargoOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, int completed) => WorldRuntime.CartCargoOrderEffectId(instruction, completed);
    }

    private ICartOrdersWorld CartOrdersWorld => new CartOrdersAdapter(this);

    private sealed class CartOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), ICartOrdersWorld
    {
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public bool IsCartCargoOrder(string action) => WorldRuntime.IsCartCargoOrder(action);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForCargoOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartForCargoOrder(instruction, person);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(Runtime.HandcartWorld, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForRepairOrder(string actor, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartForRepairOrder(actor, person);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.HandcartHitch> HandcartHitches => Runtime.handcartHitches;
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartCargoOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartCargoOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.CartRepairOrderCandidateFor(instruction, cart);
        public string AttachCartPrefix => global::ClankerWorld.Simulation.World.HandcartControlChoice.AttachPrefix;
        public string? CartRepairOrderBlocker(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.CartRepairOrderBlocker(actor, person, cart);
        public global::ClankerWorld.Simulation.World.AnimalWorldState AnimalWorld => Runtime.animalWorld;
        public void ExecuteCartCargoOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteCartCargoOrderStep(instruction, person);
        public void ExecuteCartRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.ExecuteCartRepairOrderStep(instruction, person, cart);
        public string CartOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.CartOrderEffectId(instruction);
    }

    private ICartRepairOrdersWorld CartRepairOrdersWorld => new CartRepairOrdersAdapter(this);

    private sealed class CartRepairOrdersAdapter(WorldRuntime runtime) : ICartRepairOrdersWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? CartRepairCollectionPlan(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => OrderSystem.CartRepairCollectionPlan(runtime.CartRepairOrdersWorld, actor, cart);
        public global::System.Collections.Generic.IReadOnlyList<string> CartRepairKinds => WorldRuntime.CartRepairKinds;
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public string RepairCartPrefix => global::ClankerWorld.Simulation.World.HandcartRepairChoice.Prefix;
        public string CollectCartRepairPrefix => global::ClankerWorld.Simulation.World.HandcartRepairInputChoice.Prefix;
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep StockCollectionStep(global::ClankerWorld.Simulation.Kernel.InventoryLot stock, int quantity) => runtime.StockCollectionStep(stock, quantity);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? PlanInputCollection(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep> inputs, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.PlanInputCollection(actor, inputs, returnTo, returnRange);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.CartRepairOrderCandidateFor(instruction, cart);
        public bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartRepairOrderReceipt(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, long tick) => WorldRuntime.CartRepairOrderReceipt(instruction, tick);
        public long WorldTick => runtime.WorldTick;
    }

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

    private IDeliveryOrderSelectionWorld DeliveryOrderSelectionWorld => new DeliveryOrderSelectionAdapter(this);

    private sealed class DeliveryOrderSelectionAdapter(WorldRuntime runtime) : IDeliveryOrderSelectionWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.DeliveryOrderRemaining(order);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => OrderSystem.CarriedDeliveryOrderPlan(runtime.DeliveryOrderExecutionWorld, actor, order);
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedHouseDelivery(string actor) => runtime.CarriedHouseDelivery(actor);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => OrderSystem.HouseholdDeliveryOrderPlan(runtime.DeliveryOrderExecutionWorld, actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetBlacksmithOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetWorkstationOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetTownOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetTownOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetStoreOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetStoreOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? BlacksmithForHousehold(string householdId) => runtime.HouseholdBuildingWithTag(householdId, "blacksmith");
        public bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.DeliveryDestinationMatches(order, building);
        public (string ItemKind, global::ClankerWorld.Simulation.World.MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId, string actor, string? itemKind = null) => runtime.BlacksmithInputToGather(householdId, blacksmithId, actor, itemKind);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public int ProjectMaterialCarryUnits(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialCarryUnits(actor, itemKind, source, useHarvestBonus);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public int BlacksmithOreStocked(string householdId, string blacksmithId) => runtime.SmithStoredQuantity(householdId, blacksmithId, "iron_ore");
        public int BlacksmithInputTarget(string blacksmithId, string itemKind) => runtime.BlacksmithInputTarget(blacksmithId, itemKind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSmithOre(string actor) => runtime.PersonalSmithOre(actor);
        public bool BlacksmithHasDeliverableInput(string householdId, global::ClankerWorld.Simulation.World.PlacedBuilding blacksmith, string actor, string itemKind) => runtime.BlacksmithHasDeliverableInput(householdId, blacksmith, actor, itemKind);
        public global::ClankerWorld.Simulation.World.MapResource? MaterialSource(string itemKind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, bool useHarvestBonus = true) => runtime.MaterialSource(itemKind, actor, returnTo, returnRange, useHarvestBonus);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.WorldRuntime.WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null, string? buildingId = null, int maximumResourceQuantity = 2147483647, string? sourceLotId = null) => runtime.WorkstationSupplyNeeds(actor, itemKind, buildingId, maximumResourceQuantity, sourceLotId);
    }
}

internal interface IBoatOrdersWorld
{
    global::ClankerWorld.Simulation.World.BoatTransportState BoatTransport { get; }
    global::ClankerWorld.Simulation.World.PlacedBuilding? Port(string id);
    bool PortIsLegal(global::ClankerWorld.Simulation.World.PlacedBuilding port);
    global::ClankerWorld.Simulation.World.BoatState? PassengerBoat(string actor);
    global::ClankerWorld.Simulation.World.BoatTripRequest? WaitingBoatRequest(string actor);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool CanWalkForTownProject(string actor, global::ClankerWorld.Simulation.World.GridPoint from, global::ClankerWorld.Simulation.World.GridPoint target, int range = 0);
    bool ReadyToBoard(string actor);
    bool ApplyBoatCandidate(string actor, string candidateId, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string BoatOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    OrderStore Orders { get; }
    global::ClankerWorld.Simulation.World.BoatTripRequest? BoundBoatRequest(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool CanUseTownBoat(string townId, string actor);
    global::ClankerWorld.Simulation.World.GridPoint? AvailablePortLanding(global::ClankerWorld.Simulation.World.PlacedBuilding port, string passenger);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> FreePortDocks(global::ClankerWorld.Simulation.World.PlacedBuilding port, string? exceptBoatId = null);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    bool IsActiveOrder(string status);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? ActiveBoatOrder(global::ClankerWorld.Simulation.World.BoatTripRequest request);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string BoatOrderArrivalEffectId(string requestId);
    void CancelBoatRequest(BoatTripRequest request, long tick);

    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice> BoatTripChoices(string actor);
    global::ClankerWorld.Simulation.World.BoatTripRequest QueueBoatTrip(string actor, global::ClankerWorld.Simulation.World.TransportSystem.BoatTripChoice choice, string? orderInstructionId = null);
}

internal interface ICartCargoOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource);
    bool IsCartCargoKind(string? kind);
    global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    bool CanPullHandcart(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    int CargoQuantity(string cartId);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId);
    global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    bool CanLoadCartLot(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CargoLotForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    string? CartCargoOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    int ContainerContentsQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    OrderStore Orders { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    string LoadCartPrefix { get; }
    string UnloadCartGroundPrefix { get; }
    string UnloadCartPrefix { get; }
    bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string CartCargoOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, int completed);
}

internal interface ICartOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource);
    global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    bool IsCartCargoOrder(string action);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForCargoOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? AttachedHandcart(string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForRepairOrder(string actor, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanPullHandcart(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.HandcartHitch> HandcartHitches { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartCargoOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string? CartOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    string AttachCartPrefix { get; }
    string? CartCargoOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string? CartRepairOrderBlocker(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::ClankerWorld.Simulation.World.AnimalWorldState AnimalWorld { get; }
    void ExecuteCartCargoOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    OrderStore Orders { get; }
    void ExecuteCartRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string CartOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
}

internal interface ICartRepairOrdersWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? CartRepairCollectionPlan(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.Generic.IReadOnlyList<string> CartRepairKinds { get; }
    bool HasCarriedMaterial(string actor, string itemKind, int quantity);
    string RepairCartPrefix { get; }
    string CollectCartRepairPrefix { get; }
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep StockCollectionStep(global::ClankerWorld.Simulation.Kernel.InventoryLot stock, int quantity);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? PlanInputCollection(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep> inputs, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    int FreeCarryCapacity(string actor);
    void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string CartRepairOrderReceipt(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, long tick);
    long WorldTick { get; }
}

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

internal interface IDeliveryOrderSelectionWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedHouseDelivery(string actor);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetTownOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetStoreOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    string HouseholdFor(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? BlacksmithForHousehold(string householdId);
    bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    (string ItemKind, global::ClankerWorld.Simulation.World.MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId, string actor, string? itemKind = null);
    int FreeCarryCapacity(string actor);
    int ProjectMaterialCarryUnits(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true);
    int ResourceInteractionRange { get; }
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    int BlacksmithOreStocked(string householdId, string blacksmithId);
    int BlacksmithInputTarget(string blacksmithId, string itemKind);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSmithOre(string actor);
    bool BlacksmithHasDeliverableInput(string householdId, global::ClankerWorld.Simulation.World.PlacedBuilding blacksmith, string actor, string itemKind);
    global::ClankerWorld.Simulation.World.MapResource? MaterialSource(string itemKind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, bool useHarvestBonus = true);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.WorldRuntime.WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null, string? buildingId = null, int maximumResourceQuantity = 2147483647, string? sourceLotId = null);
}
