using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal interface IHandcartWorld
{
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles);
    TransportSystem Transport { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyList<AnimalState> Animals { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IReadOnlyList<RecipeDefinition> Recipes { get; }
    IReadOnlyList<WorldProductionJob> ProductionJobs { get; }
    InventoryCheckpoint Inventory { get; }
    long WorldTick { get; }
    WorldMap Map { get; }
    ActorCapabilityPolicy TransportCapabilities { get; }
    PlacedBuilding? BlacksmithForHousehold(string householdId);
    bool IsAdult(string actor);
    IEnumerable<InventoryLot> WarehouseStockLots(string actor, string? itemKind = null);
    int AvailableLotQuantity(InventoryLot lot);
    IEnumerable<GoodsMatch> WarehouseStockGoods(string actor, string? itemKind, GoodsUse use);
    GoodsAnswer FindGoods(GoodsRequest request);
    bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId);
    int AgentStepCost(GridPoint from, GridPoint to);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    bool OnBorrowedMarketStall(InventoryLot lot);
    bool OnMarketStall(InventoryLot lot);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    bool IsEdibleFood(string kind);
    bool MayCollectSharedFood(string actor);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    IEnumerable<InventoryLot> HandcartControlStock(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    ActorCapabilityPolicy HandcartControlCapabilities(HandcartControlKind kind);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range = 0);
    int SharedFoodCollectionAllowance(string actor);
    int FreeCarryCapacity(string actor);
    string HouseholdFor(string actor);
    string? RecordedHouseholdFor(string actor);
    PlacedBuilding? HouseholdBuildingWithTag(string household, string tag);
    InventoryLot? SharedItem(string kind, string actor, GridPoint? destination = null, int range = 0);
    GoodsRequest SharedCollectionRequest(string actor, string owner, string kind);
    int HouseholdStockInteractionRange(InventoryLot lot);
    int PickupCarryCapacity(string actor, InventoryLot source, GridPoint destination, int destinationRange = 0);
    IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null, string? building = null, InventoryCheckpoint? inventory = null);
    bool HasCarriedMaterial(string actor, string itemKind, int quantity);
    bool HasCarriedEquipmentAtLeast(string actor, string kind);
    ActionCheck CheckEquipmentPickup(string actor, PlannedEquipmentPickup pickup, string description, ReachPurpose purpose);
    void ApplyPlannedEquipmentPickup(string actor, PlannedEquipmentPickup pickup);
    void AppendEvent(WorldEventPayload payload, GridPoint? position = null);
    void MoveCartInventory(string actor, string cart, GridPoint to, int wear);
    void TransferCartInventory(string actor, string recipient, InventoryLot cart);
    void CollectCartMaterial(HandcartBuildMaterialActionPlan plan);
    IReadOnlyList<Identity> RepairCartInventory(HandcartRepairActionPlan plan, InventoryLot cart);
    void LoadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot);
    void UnloadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot);
    void RecordNonLandRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<Identity> reservationIds);
    string CartRepairReservationId(long tick, string actor, string material);
    string NativeCartRepairId(long tick, string actor, string cart);
}
