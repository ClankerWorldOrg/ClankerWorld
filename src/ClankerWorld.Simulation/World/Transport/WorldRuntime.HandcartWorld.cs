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
        public void MoveCartInventory(string actor, string cart, GridPoint to, int wear) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.MoveHandcart(inventory, actor, cart, new(to.X, to.Y), wear));
        public void TransferCartInventory(string actor, string recipient, InventoryLot cart) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, IdentityRules.PrefixedReference("cart-gift:", $"{runtime.WorldTick}:{actor}"), actor, recipient, cart.Id, 1, "handcart_given", destinationGroundPosition: cart.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor)));
        public void CollectCartMaterial(HandcartBuildMaterialActionPlan plan)
        {
            runtime.ApplyInventoryTransition(inventory => InventoryAuthority.Transfer(inventory, WorldRuntime.CartBuildMaterialTransferId(runtime.WorldTick, plan.Actor),
            plan.Pickup.OwnerId, plan.Actor, plan.Pickup.StockId, plan.Quantity, "handcart_material_collected",
            holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
        }
        public IReadOnlyList<Identity> RepairCartInventory(HandcartRepairActionPlan plan, InventoryLot cart)
        {
            var actor = plan.Actor;
            var reservations = new List<Identity>();
            runtime.ApplyInventoryTransition(inventory =>
        {
            var updated = inventory;
            foreach (var material in plan.Materials)
            {
                var id = WorldRuntime.CartRepairReservationId(runtime.WorldTick, actor, material.ItemKind);
                updated = InventoryAuthority.Reserve(updated, id, actor, material.StockId, material.Quantity, "equipment_repair",
                    runtime.WorldTick + 1, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                reservations.Add(Identity.Parse(id));
            }
            return InventoryAuthority.RepairSingleUnit(updated, cart.Id, 10_000, reservations, holdAccess: InventoryHoldRules.BorrowedBy(actor));
        });
            return reservations;
        }
        public void LoadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
        {
            var actor = plan.Actor;
            var quantity = plan.Quantity;
            runtime.ApplyInventoryTransition(inventory =>
            {
                var updated = inventory;
                var cargoId = lot.Id;
                if (lot.OwnerId != actor)
                {
                    var operation = $"cart-pickup:{runtime.WorldTick}:{actor}";
                    updated = InventoryAuthority.Transfer(updated, operation, lot.OwnerId, actor, lot.Id, quantity,
                        "handcart_pickup", destinationGroundPosition: cart.GroundPosition, holdAccess: InventoryHoldRules.BorrowedBy(actor));
                    cargoId = quantity == lot.Quantity ? lot.Id : DerivedId.Create($"{lot.Id}#transfer:{operation}");
                }
                return InventoryAuthority.LoadHandcart(updated, IdentityRules.PrefixedReference("load:", $"{runtime.WorldTick}:{actor}"), actor, cart.Id, cargoId, quantity);
            });
        }
        public void UnloadCartInventory(HandcartCargoActionPlan plan, InventoryLot cart, InventoryLot lot)
        {
            var actor = plan.Actor;
            var quantity = plan.Quantity;
            runtime.ApplyInventoryTransition(inventory => InventoryAuthority.UnloadHandcart(inventory, IdentityRules.PrefixedReference("unload:", $"{runtime.WorldTick}:{actor}"),
                actor, cart.Id, lot.Id, quantity, plan.Choice.Kind == HandcartCargoKind.UnloadGround));
        }
        public void RecordNonLandRepairCompletion(string actor, InventoryLot originalTarget, string repairedLotId, string nativeReceiptId, GridPoint position, IReadOnlyList<Identity> reservationIds) => runtime.RecordNonLandRepairCompletion(actor, originalTarget, repairedLotId, nativeReceiptId, position, reservationIds);
        public string CartRepairReservationId(long tick, string actor, string material) => WorldRuntime.CartRepairReservationId(tick, actor, material);
        public string NativeCartRepairId(long tick, string actor, string cart) => WorldRuntime.NativeCartRepairId(tick, actor, cart);
    }
}
