using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IBoatWorld BoatWorld => new TransportWorldAdapter(this);

    private sealed class TransportWorldAdapter(WorldRuntime runtime) : IBoatWorld
    {
        public TransportSystem Transport => runtime.transportSystem;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<AnimalState> Animals => runtime.animalWorld.Animals;
        public IReadOnlyList<FarmFieldState> Fields => runtime.fields;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public InventoryCheckpoint Inventory => runtime.society.Checkpoint.Inventory;
        public AgentStore PhysicalAgents => runtime.agentStore;
        public long WorldTick => runtime.WorldTick;
        public int CivicDay => runtime.CivicDay;
        public WorldMap Map => runtime.map;
        public ActorCapabilityPolicy BoatTripCapabilities => WorldRuntime.BoatTripCapabilities;
        public ActorCapabilityPolicy TransportCapabilities => WorldRuntime.TransportCapabilities;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public AgentConversation? ConversationFor(string actor) => runtime.ConversationFor(actor);
        public InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(runtime.HandcartWorld, actor);
        public bool HasGuardianPlacementTask(string actor) => FamilySystem.HasGuardianPlacementTask(runtime.FamilyGuardianWorld, actor);
        public IReadOnlyList<string> MovingCareGroup(string actor) => global::ClankerWorld.Simulation.Society.SocietyRules.MovingCareGroup(runtime.society.Checkpoint, actor);
        public bool IsActiveTownProjectDelivery(string lot) => runtime.IsActiveTownProjectDelivery(lot);
        public PortGeometry PortGeometryFor(PlacedBuilding port) => runtime.PortGeometryFor(port);
        public HashSet<GridPoint> PortObstacles(string? exceptProjectId = null, string? exceptPortId = null, bool protectDocks = false) => runtime.PortObstacles(exceptProjectId, exceptPortId, protectDocks);
        public PlacedBuilding? Port(string id) => runtime.Port(id);
        public bool PortIsLegal(PlacedBuilding port) => runtime.PortIsLegal(port);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public ActionCheck CheckActionPlan(ActionPlan plan) => runtime.CheckActionPlan(plan);
        public void SetBoatOrderTravelStatus(BoatTripRequest request, OrderStatus status, string? reason) => runtime.SetBoatOrderTravelStatus(request, status, reason);
        public void CreditBoatOrderArrival(BoatTripRequest request) => runtime.CreditBoatOrderArrival(request);
        public IEnumerable<GridPoint> FreePortDocks(PlacedBuilding port, string? exceptBoatId = null) => TransportSystem.FreePortDocks(runtime.BoatWorld, port, exceptBoatId);
        public void AppendEvent(WorldEventPayload payload, GridPoint? position = null) => runtime.AppendEvent(payload, position);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public void MoveToward(string actor, AgentState state, GridPoint destination, string reason) => runtime.MoveToward(actor, state, destination, reason);
        public IReadOnlyList<GridPoint> ReachBoatGeography(BoatState boat, PlacedBuilding destination) => runtime.ReachBoatGeography(boat, destination);
        public void MoveGroundCargo(IReadOnlyList<string> remaining, GridPoint position, bool preserveCustody) => runtime.RunInventoryCommand(InventorySystem.MoveGroundCargo(remaining, position, preserveCustody));
        public GridPoint? FreePortDock(PlacedBuilding port, string? exceptBoatId = null) => TransportSystem.FreePortDock(runtime.BoatWorld, port, exceptBoatId);
        public void SetTownProject(string townId, TownProject project) => runtime.SetTownProject(townId, project);
        public GroundOccupancy Ground => runtime.Ground;
        public IReadOnlyList<BuildingDefinition> Definitions => runtime.worldContent.Buildings;
    }

    private IHandcartWorld HandcartWorld => new HandcartWorldAdapter(this);

    private sealed class HandcartWorldAdapter(WorldRuntime runtime) : IHandcartWorld
    {
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public TransportSystem Transport => runtime.transportSystem;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<AnimalState> Animals => runtime.animalWorld.Animals;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public IReadOnlyList<RecipeDefinition> Recipes => runtime.worldContent.Recipes;
        public IReadOnlyList<WorldProductionJob> ProductionJobs => runtime.worldSimulation.ProductionJobs;
        public InventoryCheckpoint Inventory => runtime.society.Checkpoint.Inventory;
        public long WorldTick => runtime.WorldTick;
        public WorldMap Map => runtime.map;
        public ActorCapabilityPolicy TransportCapabilities => WorldRuntime.TransportCapabilities;
        public PlacedBuilding? BlacksmithForHousehold(string householdId) => runtime.HouseholdBuildingWithTag(householdId, "blacksmith");
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public IEnumerable<InventoryLot> WarehouseStockLots(string actor, string? itemKind = null) => runtime.WarehouseStockLots(actor, itemKind);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public IEnumerable<GoodsMatch> WarehouseStockGoods(string actor, string? itemKind, GoodsUse use) => runtime.WarehouseStockGoods(actor, itemKind, use);
        public GoodsAnswer FindGoods(GoodsRequest request) => runtime.FindGoods(request);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int AgentStepCost(GridPoint from, GridPoint to) => runtime.AgentStepCost(from, to);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => runtime.IsWithinInteractionRange(origin, destination, range);
        public bool OnBorrowedMarketStall(InventoryLot lot) => runtime.OnBorrowedMarketStall(lot);
        public bool OnMarketStall(InventoryLot lot) => MarketSystem.OnMarketStall(runtime.MarketStallWorld, lot);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => runtime.RecheckGoods(request, lot);
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public bool MayCollectSharedFood(string actor) => runtime.townSystem.MayCollectSharedFood(runtime.civicWorld, actor);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public IEnumerable<InventoryLot> HandcartControlStock(string actor) => runtime.HandcartControlStock(actor);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public ActorCapabilityPolicy HandcartControlCapabilities(HandcartControlKind kind) => WorldRuntime.HandcartControlCapabilities(kind);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range = 0) => runtime.MoveToward(actor, person, destination, reason, range);
        public int SharedFoodCollectionAllowance(string actor) => runtime.townSystem.SharedFoodCollectionAllowance(runtime.civicWorld, actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public string? RecordedHouseholdFor(string actor) => runtime.society.Checkpoint.GetAgent(actor).HouseholdId;
        public PlacedBuilding? HouseholdBuildingWithTag(string household, string tag) => runtime.HouseholdBuildingWithTag(household, tag);
        public InventoryLot? SharedItem(string kind, string actor, GridPoint? destination = null, int range = 0) => runtime.SharedItem(kind, actor, destination, range);
        public GoodsRequest SharedCollectionRequest(string actor, string owner, string kind) => WorldRuntime.SharedCollectionRequest(actor, owner, kind);
        public int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public int PickupCarryCapacity(string actor, InventoryLot source, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, source, destination, destinationRange);
        public IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null, string? building = null, InventoryCheckpoint? inventory = null) => runtime.SpendingGoods(actor, use, owners, kind, building, inventory);
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public bool HasCarriedEquipmentAtLeast(string actor, string kind) => runtime.HasCarriedEquipmentAtLeast(actor, kind);
        public ActionCheck CheckEquipmentPickup(string actor, PlannedEquipmentPickup pickup, string description, ReachPurpose purpose) => runtime.CheckEquipmentPickup(actor, pickup, description, purpose);
        public void ApplyPlannedEquipmentPickup(string actor, PlannedEquipmentPickup pickup) => runtime.ApplyPlannedEquipmentPickup(actor, pickup);
        public void AppendEvent(WorldEventPayload payload, GridPoint? position = null) => runtime.AppendEvent(payload, position);
        public void MoveCartInventory(string actor, string cart, GridPoint to, int wear) => runtime.RunInventoryCommand(InventorySystem.MoveCartInventory(actor, cart, to, wear));
        public void TransferCartInventory(string actor, string recipient, InventoryLot cart) => runtime.RunInventoryCommand(InventorySystem.TransferCartInventory(runtime.InventoryTransportWorld, actor, recipient, cart));
        public void CollectCartMaterial(HandcartBuildMaterialActionPlan plan)
        {
            runtime.RunInventoryCommand(InventorySystem.CollectCartMaterial(runtime.InventoryTransportWorld, plan));
        }
        public IReadOnlyList<Identity> RepairCartInventory(HandcartRepairActionPlan plan, InventoryLot cart)
        {
            var actor = plan.Actor;
            var reservations = new List<Identity>();
            runtime.RunInventoryCommand(InventorySystem.RepairCartInventory(runtime.InventoryTransportWorld, plan, actor, reservations, cart));
            return reservations;
        }
        public void LoadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
        {
            var actor = plan.Actor;
            var quantity = plan.Quantity;
            runtime.RunInventoryCommand(InventorySystem.LoadCartInventory(runtime.InventoryTransportWorld, lot, actor, quantity, cart));
        }
        public void UnloadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
        {
            var actor = plan.Actor;
            var quantity = plan.Quantity;
            runtime.RunInventoryCommand(InventorySystem.UnloadCartInventory(runtime.InventoryTransportWorld, actor, cart, lot, quantity, plan));
        }
        public void RecordNonLandRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<Identity> reservationIds) => runtime.RecordNonLandRepairCompletion(actor, originalTarget, repairedLotId, nativeReceiptId, position, reservationIds);
        public string CartRepairReservationId(long tick, string actor, string material) => WorldRuntime.CartRepairReservationId(tick, actor, material);
        public string NativeCartRepairId(long tick, string actor, string cart) => WorldRuntime.NativeCartRepairId(tick, actor, cart);
    }

    private IRoadWorld RoadWorld => new RoadWorldAdapter(this);

    private sealed class RoadWorldAdapter(WorldRuntime runtime) : IRoadWorld
    {
        public TransportSystem Transport => runtime.transportSystem;
        public WorldMap Map => runtime.map;
        public GroundOccupancy Ground => runtime.Ground;
        public IReadOnlyList<BridgeState> SavedBridges => runtime.Bridges;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public IReadOnlyList<BuildingDefinition> Definitions => runtime.worldContent.Buildings;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public string Seed => runtime.worldSeed;
        public long WorldTick => runtime.WorldTick;
        public int TicksPerWorldDay => runtime.society.Checkpoint.Config.TicksPerWorldDay;
        public IEnumerable<GridPoint> HouseholdLandHeldByOthers(string? household) => runtime.HouseholdLandHeldByOthers(household);
        public void AppendEvent(WorldEventPayload payload, GridPoint? position = null) => runtime.AppendEvent(payload, position);
        public void ApplyBridgeDecks() => runtime.ApplyBridgeDecks();
        public void SetBuildingEntrance(string building, GridPoint entrance) => runtime.SetBuildingEntrance(building, entrance);
        public string RoadRouteId(string town, string building) => WorldRuntime.RoadRouteId(town, building);
    }
}
