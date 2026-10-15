using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
        public PlacedBuilding? BlacksmithForHousehold(string householdId) => runtime.BlacksmithForHousehold(householdId);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public IEnumerable<InventoryLot> WarehouseStockLots(string actor, string? itemKind = null) => runtime.WarehouseStockLots(actor, itemKind);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public IEnumerable<GoodsMatch> WarehouseStockGoods(string actor, string? itemKind, GoodsUse use) => runtime.WarehouseStockGoods(actor, itemKind, use);
        public GoodsAnswer FindGoods(GoodsRequest request) => runtime.FindGoods(request);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public int AgentStepCost(GridPoint from, GridPoint to) => runtime.AgentStepCost(from, to);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => runtime.IsWithinInteractionRange(origin, destination, range);
        public bool OnBorrowedMarketStall(InventoryLot lot) => runtime.OnBorrowedMarketStall(lot);
        public bool OnMarketStall(InventoryLot lot) => runtime.OnMarketStall(lot);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => runtime.RecheckGoods(request, lot);
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public bool MayCollectSharedFood(string actor) => runtime.MayCollectSharedFood(actor);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public IEnumerable<InventoryLot> HandcartControlStock(string actor) => runtime.HandcartControlStock(actor);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public ActorCapabilityPolicy HandcartControlCapabilities(HandcartControlKind kind) => WorldRuntime.HandcartControlCapabilities(kind);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range = 0) => runtime.MoveToward(actor, person, destination, reason, range);
        public int SharedFoodCollectionAllowance(string actor) => runtime.SharedFoodCollectionAllowance(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
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
        public void AppendEvent(string kind, string detail, GridPoint? position = null) => runtime.AppendEvent(kind, detail, position);
        public void MoveCartInventory(string actor, string cart, GridPoint to, int wear) => runtime.RunInventoryCommand(InventorySystem.MoveCartInventory(actor, cart, to, wear));
        public void TransferCartInventory(string actor, string recipient, InventoryLot cart) => runtime.RunInventoryCommand(InventorySystem.TransferCartInventory(runtime.InventoryTransportWorld, actor, recipient, cart));
        public void CollectCartMaterial(HandcartBuildMaterialActionPlan plan)
        {
            runtime.RunInventoryCommand(InventorySystem.CollectCartMaterial(runtime.InventoryTransportWorld, plan));
        }
        public IReadOnlyList<string> RepairCartInventory(HandcartRepairActionPlan plan, InventoryLot cart)
        {
            var actor = plan.Actor;
            var reservations = new List<string>();
            runtime.RunInventoryCommand(InventorySystem.RepairCartInventory(runtime.InventoryTransportWorld, plan, cart, actor, reservations));
            return reservations;
        }
        public void LoadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
        {
            var actor = plan.Actor;
            var quantity = plan.Quantity;
            runtime.RunInventoryCommand(InventorySystem.LoadCartInventory(runtime.InventoryTransportWorld, cart, lot, actor, quantity));
        }
        public void UnloadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
        {
            var actor = plan.Actor;
            var quantity = plan.Quantity;
            runtime.RunInventoryCommand(InventorySystem.UnloadCartInventory(runtime.InventoryTransportWorld, plan, cart, lot, actor, quantity));
        }
        public void RecordNonLandRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<string> reservationIds) => runtime.RecordNonLandRepairCompletion(actor, originalTarget, repairedLotId, nativeReceiptId, position, reservationIds);
        public string CartRepairReservationId(long tick, string actor, string material) => WorldRuntime.CartRepairReservationId(tick, actor, material);
        public string NativeCartRepairId(long tick, string actor, string cart) => WorldRuntime.NativeCartRepairId(tick, actor, cart);
    }
}
