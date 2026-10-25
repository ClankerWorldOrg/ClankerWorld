using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IConstructionOrdersWorld ConstructionOrdersWorld => new ConstructionOrdersAdapter(this);

    private sealed class ConstructionOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IConstructionOrdersWorld
    {
        public string ConstructionInstanceId(string instructionId) => WorldRuntime.ConstructionInstanceId(instructionId);
        public bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsConstructionOrderProject(instruction, project);
        public bool OrdinaryProjectCanYieldToOrder(global::ClankerWorld.Simulation.World.AgentProject project) => Runtime.OrdinaryProjectCanYieldToOrder(project);
        public string? HouseholdBuildingKind(global::ClankerWorld.Simulation.Content.BuildingDefinition definition) => WorldRuntime.HouseholdBuildingKind(definition);
        public bool TownHasRoomForSharedShop(string actor, string? kind) => Runtime.TownHasRoomForSharedShop(actor, kind);
        public bool WorkBuildingHasInputs(string actor, string household, global::ClankerWorld.Simulation.Content.BuildingDefinition building) => Runtime.WorkBuildingHasInputs(actor, household, building);
        public global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null) => Runtime.CreateTownLayoutContext(actor, selectedSite, building);
        public bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure) => OrderSystem.TryConstructionOrderSite(Runtime.ConstructionOrdersWorld, instruction, person, out definition, out household, out position, out failure);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelConstructionForOrder(Runtime.ConstructionOrdersWorld, instruction);
        public string ConstructionOrderReceipt(string instanceId) => WorldRuntime.ConstructionOrderReceipt(instanceId);
        public void RecordConstructionReceipt(WorldConstructionReceipt receipt) => Runtime.constructionSystem.Buildings.RecordConstructionReceipt(receipt);
    }

    private IExpansionOrdersWorld ExpansionOrdersWorld => new ExpansionOrdersAdapter(this);

    private sealed class ExpansionOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IExpansionOrdersWorld
    {
        public string ExpansionOwner(global::ClankerWorld.Simulation.World.PlacedBuilding building) => WorldRuntime.ExpansionOwner(building);
        public bool ExpansionJobMatchesOrder(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.ExpansionJobMatchesOrder(job, instruction);
        public global::System.Collections.Generic.IEnumerable<(global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(global::ClankerWorld.Simulation.World.PlacedBuilding building) => Runtime.ExpansionShapes(building);
        public bool CanFitExpansion(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true) => Runtime.CanFitExpansion(building, position, footprint, out failure, ownJobId, requireLandRights);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? ExpansionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ExpansionOrderBuilding(Runtime.ExpansionOrdersWorld, instruction);
        public bool MayExpandBuilding(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, out string failure) => Runtime.MayExpandBuilding(actor, building, out failure);
        public (global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint) ExpansionShapeForOrder(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction) => OrderSystem.ExpansionShapeForOrder(Runtime.ExpansionOrdersWorld, building, instruction);
        public bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null) => Runtime.CanAcquireProjectInputs(inputs, ownerId, residentId, reachableToolCache, accessibleWarehouses);
        public bool CanAcquireExpansionMaterials(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> costs) => Runtime.CanAcquireExpansionMaterials(actor, building, costs);
        public global::ClankerWorld.Simulation.World.BuildingExpansionJob? BoundExpansionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.BoundExpansionJob(Runtime.ExpansionOrdersWorld, instruction);
        public bool ExpansionOrderInputsAvailable(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.PlacedBuilding building) => OrderSystem.ExpansionOrderInputsAvailable(Runtime.ExpansionOrdersWorld, job, building);
        public global::ClankerWorld.Simulation.World.OrderSystem.ExpansionOrderPlan? ExpansionPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.ExpansionPlanFor(Runtime.ExpansionOrdersWorld, instruction, person);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public void ApplyBuildingExpansionCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string buildingId, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? orderInstruction = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => Runtime.ApplyBuildingExpansionCandidate(actor, state, buildingId, orderInstruction, originalPlan);
        public global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(global::ClankerWorld.Simulation.World.PlacedBuilding building) => WorldRuntime.ExpansionGroundPosition(building);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.CancelExpansionForOrder(Runtime.ExpansionOrdersWorld, instruction);
        public string ExpansionOrderReceipt(string jobId) => WorldRuntime.ExpansionOrderReceipt(jobId);
        public void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => Runtime.RunInventoryCommand(InventorySystem.ExtendOrderReservations(reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<Identity> reservations) => Runtime.RunInventoryCommand(InventorySystem.HoldOrderReservations(reservations));
        public void ReleaseExpansionOrderInputs(BuildingExpansionJob job) => Runtime.RunInventoryCommand(InventorySystem.ReleaseExpansionOrderInputs(job));
        public void ResumeExpansion(string job, long deadline) => Runtime.constructionSystem.Buildings.ResumeExpansion(job, deadline);
        public void PauseExpansion(string job, long tick) => Runtime.constructionSystem.Buildings.PauseExpansion(job, tick);
        public void CancelExpansions(IReadOnlySet<string> jobs, string reason) => Runtime.constructionSystem.Buildings.CancelPausedExpansions(jobs, reason);
    }

    private IRepairOrdersWorld RepairOrdersWorld => new RepairOrdersAdapter(this);

    private sealed class RepairOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IRepairOrdersWorld
    {
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor) => Runtime.WornEquipmentItems(actor);
        public bool CanPrepareEquipmentRepair(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => Runtime.ContinueEquipmentRepair(actor);
        public string EquipmentRepairReceipt(string actor, long startedTick, string lot) => WorldRuntime.EquipmentRepairReceipt(actor, startedTick, lot);
        public void RepairEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string? requestedLotId = null, string? orderInstructionId = null) => Runtime.RepairEquipment(actor, person, requestedLotId, orderInstructionId);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.EquipmentRepairSite(actor, lot);
        public int MissingRepairInputUnits(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot item) => Runtime.MissingRepairInputUnits(actor, item);
        public bool HasCarriedOwnItem(string actor, string kind) => Runtime.HasCarriedOwnItem(actor, kind);
    }

    private IMaterialOrdersWorld MaterialOrdersWorld => new MaterialOrdersAdapter(this);

    private sealed class MaterialOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IMaterialOrdersWorld
    {
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.MapResource> KnownMaterialOrderSources(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.KnownMaterialOrderSources(Runtime.MaterialOrdersWorld, instruction, person);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache) => Runtime.CanGatherFromSource(actor, itemKind, source, reachableToolCache);
        public global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => Runtime.ProjectMaterialHarvest(actor, itemKind, source, useHarvestBonus);
        public global::ClankerWorld.Simulation.World.GridPoint? MaterialOrderTarget(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.MaterialOrderTarget(Runtime.MaterialOrdersWorld, instruction);
        public string MaterialGatherReceipt(string lot) => WorldRuntime.MaterialGatherReceipt(lot);
    }

    private IProductionOrdersWorld ProductionOrdersWorld => new ProductionOrdersAdapter(this);

    private sealed class ProductionOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IProductionOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.PlacedBuilding? ProductionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe) => OrderSystem.ProductionOrderBuilding(Runtime.ProductionOrdersWorld, instruction, recipe);
        public bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null) => Runtime.TryFindRecipeSite(recipe, out siteId, out position, actorId);
        public bool IsHouseholdBuildingTag(string tag) => WorldRuntime.IsHouseholdBuildingTag(tag);
        public bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool CanResumeHeldInputs(string actor, string buildingId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Identity> reservationIds) => CommunitySystem.CanResumeHeldInputs(Runtime.CommunityWorkWorld, actor, buildingId, reservationIds);
        public bool TryResumeKnife(string actor, string jobId, out string? knifeId) => CommunitySystem.TryResumeKnife(Runtime.CommunityWorkWorld, actor, jobId, out knifeId);
        public bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project) => WorldRuntime.IsProductionOrderProject(instruction, project);
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.CancelProductionForOrder(instruction);
        public string ProductionOrderReceipt(string jobId) => WorldRuntime.ProductionOrderReceipt(jobId);
        public void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline) => Runtime.RunInventoryCommand(InventorySystem.OrdersExtendOrderReservations(reservations, deadline));
        public void HoldOrderReservations(IReadOnlyList<Identity> reservations) => Runtime.RunInventoryCommand(InventorySystem.OrdersHoldOrderReservations(reservations));
        public void ReleaseProductionOrderInputs(WorldProductionJob job) => Runtime.RunInventoryCommand(InventorySystem.ReleaseProductionOrderInputs(job));
        public void ResumeProductionJob(string job, long deadline, string? knife) => Runtime.productionSystem.ResumeJob(job, deadline, knife);
        public void PauseProductionJob(string job, long tick) => Runtime.productionSystem.PauseJob(job, tick);
        public void CancelProductionJobs(IReadOnlySet<string> jobs) => Runtime.productionSystem.CancelJobs(jobs);
    }

    private ICollectionOrdersWorld CollectionOrdersWorld => new CollectionOrdersAdapter(this);

    private sealed class CollectionOrdersAdapter(WorldRuntime runtime) : ICollectionOrdersWorld
    {
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CollectionOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.GoodsUse use = (global::ClankerWorld.Simulation.World.GoodsUse)4) => OrderSystem.CollectionOrderGoods(runtime.CollectionOrdersWorld, instruction, use);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public string CollectionOrderGoodsName(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.CollectionOrderGoodsName(order);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => runtime.RecoverablePersonalGoods(actor, kind);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalGoodsAwaitingCollection(string actor) => runtime.PersonalGoodsAwaitingCollection(actor);
        public bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CollectionOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CollectionOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public string PersonalCollectionReceipt(string move) => WorldRuntime.PersonalCollectionReceipt(move);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room) => runtime.VesselFits(lot, room);
    }

    private ICustodyOrdersWorld CustodyOrdersWorld => new CustodyOrdersAdapter(this);

    private sealed class CustodyOrdersAdapter(WorldRuntime runtime) : ICustodyOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => runtime.farmingSystem.FarmWorkFor(runtime.farmingWorld, workerId);
        public void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field) => runtime.farmingSystem.CancelFarmWork(runtime.farmingWorld, field);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId) => runtime.HouseForHousehold(householdId);
        public OrderStore Orders => runtime.orderStore;
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnableBorrowedGoods(string actor) => runtime.ReturnableBorrowedGoods(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ReturnOrderGoods(runtime.CustodyOrdersWorld, instruction);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId) => OrderSystem.CustodyOrderHouse(runtime.CustodyOrdersWorld, instruction, ownerId);
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ReturnOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ReturnOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null) => OrderSystem.BindCustodyOrder(runtime.CustodyOrdersWorld, instruction, house, lotId);
        public global::ClankerWorld.Simulation.World.WorldRuntime.BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null) => runtime.ReturnBorrowedGoods(actor, lotId, maximumQuantity, requiredHouseId, requiredHouseholdId, requiredPosition);
        public string BorrowedReturnReceipt(string move) => WorldRuntime.BorrowedReturnReceipt(move);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }

    private IStorageOrdersWorld StorageOrdersWorld => new StorageOrdersAdapter(this);

    private sealed class StorageOrdersAdapter(WorldRuntime runtime) : IStorageOrdersWorld
    {
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId) => OrderSystem.CustodyOrderHouse(runtime.CustodyOrdersWorld, instruction, ownerId);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalStorageLots(string actor, string houseId) => runtime.PersonalStorageLots(actor, houseId);
        public string? StorageOrderItemKind(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.StorageOrderItemKind(order);
        public string StorageOrderSubject(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.StorageOrderSubject(order);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.StorageOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.StorageOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null) => OrderSystem.BindCustodyOrder(runtime.CustodyOrdersWorld, instruction, house, lotId);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null) => runtime.StorePersonalGoods(actor, lotId, maximumQuantity, requiredHouseId, requiredHouseholdId, requiredPosition);
        public string PersonalStorageReceipt(string move) => WorldRuntime.PersonalStorageReceipt(move);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
    }

    private IToolRepairOrdersWorld ToolRepairOrdersWorld => new ToolRepairOrdersAdapter(this);

    private sealed class ToolRepairOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IToolRepairOrdersWorld
    {
        public global::ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null) => Runtime.RepairableTools(actor, protectedToolIds);
        public string[] ToolRepairOrderTargets(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderTargets(Runtime.ToolRepairOrdersWorld, instruction);
        public string? RepairTool(string actor, global::ClankerWorld.Simulation.World.AgentState state, string lotId, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null) => Runtime.RepairTool(actor, state, lotId, protectedToolIds);
        public string ToolRepairReceipt(string actor, long tick, string lot) => WorldRuntime.ToolRepairReceipt(actor, tick, lot);
        public int UnreservedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.UnreservedQuantity(inventory, lot);
    }

    private ITreePlantingOrdersWorld TreePlantingOrdersWorld => new TreePlantingOrdersAdapter(this);

    private sealed class TreePlantingOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), ITreePlantingOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, global::ClankerWorld.Simulation.World.GridPoint origin) => FarmingSystem.PlantingSite(Runtime.farmingWorld, actor, origin);
        public global::ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(global::ClankerWorld.Simulation.World.GridPoint point, (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) obstacles) => FarmingSystem.PlantingSiteRefusal(Runtime.farmingWorld, point, obstacles);
        public (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles() => Runtime.PlantingObstacles();
        public bool TreeAreaFull(global::ClankerWorld.Simulation.World.GridPoint point) => FarmingSystem.TreeAreaFull(Runtime.farmingWorld, point);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public string PlantingSpecies(global::ClankerWorld.Simulation.World.GridPoint site) => FarmingSystem.PlantingSpecies(Runtime.farmingWorld, site);
        public int PlantingSeedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.PlantingSeedQuantity(lot);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => Runtime.RecoverablePersonalGoods(actor, kind);
        public int PickupCarryCapacity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0) => Runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public global::ClankerWorld.Simulation.World.GridPoint? TreeOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreeOrderSite(Runtime.TreePlantingOrdersWorld, instruction, person);
        public string TreeOrderSpecies(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.GridPoint site) => OrderSystem.TreeOrderSpecies(Runtime.TreePlantingOrdersWorld, order, site);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedTreeOrderSeed(string actor, string kind) => OrderSystem.CarriedTreeOrderSeed(Runtime.TreePlantingOrdersWorld, actor, kind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CollectableTreeOrderSeed(string actor, string kind, global::ClankerWorld.Simulation.World.GridPoint site) => OrderSystem.CollectableTreeOrderSeed(Runtime.TreePlantingOrdersWorld, actor, kind, site);
        public global::ClankerWorld.Simulation.World.OrderSystem.TreeOrderPlan? TreePlantingOrderPlan(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderPlan(Runtime.TreePlantingOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => Runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => Runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public global::ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, global::ClankerWorld.Simulation.World.GridPoint destination) => FarmingSystem.PlantTreeCore(Runtime.farmingWorld, planterId, species, seedLotId, destination);
        public string TreePlantingReceipt(string treeId) => WorldRuntime.TreePlantingReceipt(treeId);
    }

    private IFieldOrdersWorld FieldOrdersWorld => new FieldOrdersAdapter(this);

    private sealed class FieldOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IFieldOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action) => WorldRuntime.FieldOrderKind(action);
        public global::ClankerWorld.Simulation.World.LandFertility Fertility => Runtime.fertility;
        public global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household) => Runtime.HouseholdLandHeldByOthers(household);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(global::ClankerWorld.Simulation.World.GridPoint origin) => FarmingSystem.NearbyFarmTiles(Runtime.farmingWorld, origin);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.FarmFieldState> Fields => Runtime.fields;
        public bool HasOtherAgentClaimedPlanting(global::ClankerWorld.Simulation.World.FarmFieldState field, string actor) => Runtime.farmingSystem.HasOtherAgentClaimedPlanting(Runtime.farmingWorld, field, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, global::ClankerWorld.Simulation.World.FarmFieldState field, string crop, bool requirePickupRoute = false) => FarmingSystem.PlantingStock(Runtime.farmingWorld, actor, field, crop, requirePickupRoute);
        public global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId) => Runtime.farmingSystem.FarmWorkFor(Runtime.farmingWorld, workerId);
        public global::ClankerWorld.Simulation.World.GridPoint? FieldOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.FieldOrderSite(instruction, person);
        public bool ContinueFarmWork(string workerId, out global::ClankerWorld.Simulation.World.FarmFieldState? completed) => Runtime.farmingSystem.ContinueFarmWork(Runtime.farmingWorld, workerId, out completed);
        public string FieldWorkOrderReceipt(string actor, long tick, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.FarmWorkKind kind, int cycle) => WorldRuntime.FieldWorkOrderReceipt(actor, tick, position, kind, cycle);
        public void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field) => Runtime.farmingSystem.CancelFarmWork(Runtime.farmingWorld, field);
        public void ApplyFieldCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, string? orderInstructionId = null) => Runtime.farmingSystem.ApplyFieldCandidate(Runtime.farmingWorld, actor, state, candidate, orderInstructionId);
        public string FarmCandidate(global::ClankerWorld.Simulation.World.FarmWorkKind kind, global::ClankerWorld.Simulation.World.GridPoint point, string? crop = null) => FarmingSystem.FarmCandidate(kind, point, crop);
        public bool FarmableFreeTile(GridPoint point, LandFertility fertility) => Runtime.farmingSystem.FarmableFreeTile(Runtime.farmingWorld, point, fertility);
    }
}

internal interface ICollectionOrdersWorld
{
    bool ReadyForBriefInteraction(string actor);
    int FreeCarryCapacity(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CollectionOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.GoodsUse use = (global::ClankerWorld.Simulation.World.GoodsUse)4);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    string CollectionOrderGoodsName(global::ClankerWorld.Simulation.World.OrderTask order);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalGoodsAwaitingCollection(string actor);
    bool IsEdibleFood(string kind);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CollectionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string CollectionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647);
    string PersonalCollectionReceipt(string move);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room);
}

internal interface IConstructionOrdersWorld
{
    string ConstructionInstanceId(string instructionId);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    OrderStore Orders { get; }
    long WorldTick { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool IsAdult(string actor);
    bool IsConstructionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    bool OrdinaryProjectCanYieldToOrder(global::ClankerWorld.Simulation.World.AgentProject project);
    string? HouseholdBuildingKind(global::ClankerWorld.Simulation.Content.BuildingDefinition definition);
    bool TownHasRoomForSharedShop(string actor, string? kind);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    bool WorkBuildingHasInputs(string actor, string household, global::ClankerWorld.Simulation.Content.BuildingDefinition building);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.TownLayoutContext CreateTownLayoutContext(string actor, global::ClankerWorld.Simulation.World.GridPoint? selectedSite = null, global::ClankerWorld.Simulation.Content.BuildingDefinition? building = null);
    bool ReadyForBriefInteraction(string actor);
    bool TryConstructionOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, out global::ClankerWorld.Simulation.Content.BuildingDefinition? definition, out string? household, out global::ClankerWorld.Simulation.World.GridPoint position, out string failure);
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project);
    void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state);
    bool IsActiveOrder(string status);
    void CancelConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string ConstructionOrderReceipt(string instanceId);
    void MarkCheckpointCurrent();
    void RecordConstructionReceipt(WorldConstructionReceipt receipt);
}

internal interface ICustodyOrdersWorld
{
    global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseForHousehold(string householdId);
    OrderStore Orders { get; }
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnableBorrowedGoods(string actor);
    bool ReadyForBriefInteraction(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> ReturnOrderGoods(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId);
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    bool VesselFits(global::ClankerWorld.Simulation.Kernel.InventoryLot lot, int room);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ReturnOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string ReturnOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null);
    global::ClankerWorld.Simulation.World.WorldRuntime.BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null);
    string BorrowedReturnReceipt(string move);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    void MarkCheckpointCurrent();
}

internal interface IExpansionOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    string ExpansionOwner(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    string HouseholdFor(string actor);
    string? TownForResident(string residentId);
    bool ExpansionJobMatchesOrder(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::System.Collections.Generic.IEnumerable<(global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint)> ExpansionShapes(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool CanFitExpansion(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision footprint, out string failure, string? ownJobId = null, bool requireLandRights = true);
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? ExpansionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool MayExpandBuilding(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, out string failure);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    (global::ClankerWorld.Simulation.World.GridPoint Position, global::ClankerWorld.Simulation.World.BuildingFootprintRevision Footprint) ExpansionShapeForOrder(global::ClankerWorld.Simulation.World.PlacedBuilding building, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? instruction);
    bool CanAcquireProjectInputs(global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> inputs, string? ownerId = null, string? residentId = null, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?>? reachableToolCache = null, global::System.Lazy<global::ClankerWorld.Simulation.World.PlacedBuilding[]>? accessibleWarehouses = null);
    bool CanAcquireExpansionMaterials(string actor, global::ClankerWorld.Simulation.World.PlacedBuilding building, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Content.ContentQuantity> costs);
    global::ClankerWorld.Simulation.World.BuildingExpansionJob? BoundExpansionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool ExpansionOrderInputsAvailable(global::ClankerWorld.Simulation.World.BuildingExpansionJob job, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    global::ClankerWorld.Simulation.World.OrderSystem.ExpansionOrderPlan? ExpansionPlanFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    OrderStore Orders { get; }
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    void PauseExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ExpansionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ExpansionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void ApplyBuildingExpansionCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string buildingId, global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? orderInstruction = null, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.Kernel.InventoryGroundPosition ExpansionGroundPosition(global::ClankerWorld.Simulation.World.PlacedBuilding building);
    bool IsActiveOrder(string status);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void CancelExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsConversationBusy(string agentId);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string ExpansionOrderReceipt(string jobId);
    void MarkCheckpointCurrent();
    void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline);
    void HoldOrderReservations(IReadOnlyList<Identity> reservations);
    void ReleaseExpansionOrderInputs(BuildingExpansionJob job);
    void ResumeExpansion(string job, long deadline);
    void PauseExpansion(string job, long tick);
    void CancelExpansions(IReadOnlySet<string> jobs, string reason);
}

internal interface IFieldOrdersWorld
{
    string HouseholdFor(string actor);
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? FarmhouseForHousehold(string householdId);
    global::ClankerWorld.Simulation.World.FarmWorkKind FieldOrderKind(string action);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.World.LandFertility Fertility { get; }
    global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> HouseholdLandHeldByOthers(string? household);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.GridPoint> NearbyFarmTiles(global::ClankerWorld.Simulation.World.GridPoint origin);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.FarmFieldState> Fields { get; }
    bool HasOtherAgentClaimedPlanting(global::ClankerWorld.Simulation.World.FarmFieldState field, string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? PlantingStock(string actor, global::ClankerWorld.Simulation.World.FarmFieldState field, string crop, bool requirePickupRoute = false);
    global::ClankerWorld.Simulation.World.FarmFieldState? FarmWorkFor(string workerId);
    global::ClankerWorld.Simulation.World.GridPoint? FieldOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool ContinueFarmWork(string workerId, out global::ClankerWorld.Simulation.World.FarmFieldState? completed);
    string FieldWorkOrderReceipt(string actor, long tick, global::ClankerWorld.Simulation.World.GridPoint position, global::ClankerWorld.Simulation.World.FarmWorkKind kind, int cycle);
    long WorldTick { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void CancelFarmWork(global::ClankerWorld.Simulation.World.FarmFieldState field);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string FieldOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void ApplyFieldCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState state, string candidate, string? orderInstructionId = null);
    string FarmCandidate(global::ClankerWorld.Simulation.World.FarmWorkKind kind, global::ClankerWorld.Simulation.World.GridPoint point, string? crop = null);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool FarmableFreeTile(GridPoint point, LandFertility fertility);
}

internal interface IMaterialOrdersWorld
{
    int FreeCarryCapacity(string actor);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.MapResource> KnownMaterialOrderSources(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    int ResourceInteractionRange { get; }
    bool CanGatherFromSource(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, global::System.Collections.Generic.Dictionary<(global::ClankerWorld.Simulation.World.ToolFamily Family, int Tier), global::ClankerWorld.Simulation.World.ToolDefinition?> reachableToolCache);
    global::ClankerWorld.Simulation.World.ToolGatheringPlan? ProjectMaterialHarvest(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true);
    bool CanExploreForOrder(global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.GridPoint? MaterialOrderTarget(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    global::ClankerWorld.Simulation.World.WorldKnowledgeState Knowledge { get; }
    global::System.Collections.Generic.Dictionary<string, global::ClankerWorld.Simulation.World.ResourceState> Resources { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    bool RecordKnowledgeFact(string actor, global::ClankerWorld.Simulation.World.GridPoint position);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? MaterialOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string MaterialOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OrderSystem.MaterialGatherEffect? GatherProjectMaterial(string agentId, global::ClankerWorld.Simulation.World.AgentState state, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true, string? deliveryBuildingId = null, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    string MaterialGatherReceipt(string lot);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
}

internal interface IProductionOrdersWorld
{
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.World.PlacedBuilding? ProductionOrderBuilding(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Content.RecipeDefinition recipe);
    bool TryFindRecipeSite(global::ClankerWorld.Simulation.Content.RecipeDefinition recipe, out string siteId, out global::ClankerWorld.Simulation.World.GridPoint position, string? actorId = null);
    global::ClankerWorld.Simulation.World.WorldContentSimulationState Simulation { get; }
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool IsHouseholdBuildingTag(string tag);
    OrderStore Orders { get; }
    bool NeedsUrgentFood(global::ClankerWorld.Simulation.World.AgentState person);
    bool NeedsUrgentWarmth(global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ProductionOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string ProductionOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void PauseProductionOrderJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.WorldProductionJob job);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool CanResumeHeldInputs(string actor, string buildingId, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.Identity> reservationIds);
    bool TryResumeKnife(string actor, string jobId, out string? knifeId);
    long WorldTick { get; }
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    bool IsProductionOrderProject(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentProject? project);
    global::ClankerWorld.Simulation.World.AgentStore PhysicalAgents { get; }
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    void SetProject(string agentId, global::ClankerWorld.Simulation.World.AgentProject project);
    void ContinueProject(string agentId, global::ClankerWorld.Simulation.World.AgentState state);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool IsActiveOrder(string status);
    bool IsAdult(string actor);
    void CancelProductionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    bool IsConversationBusy(string agentId);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction? PendingInstructionFor(string agentId);
    string ProductionOrderReceipt(string jobId);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void MarkCheckpointCurrent();
    void ExtendOrderReservations(IReadOnlyList<Identity> reservations, long deadline);
    void HoldOrderReservations(IReadOnlyList<Identity> reservations);
    void ReleaseProductionOrderInputs(WorldProductionJob job);
    void ResumeProductionJob(string job, long deadline, string? knife);
    void PauseProductionJob(string job, long tick);
    void CancelProductionJobs(IReadOnlySet<string> jobs);
}

internal interface IRepairOrdersWorld
{
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor);
    bool CanPrepareEquipmentRepair(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    string EquipmentRepairReceipt(string actor, long startedTick, string lot);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void CancelEquipmentRepair(string actor);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void RepairEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string? requestedLotId = null, string? orderInstructionId = null);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    global::ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    int MissingRepairInputUnits(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot item);
    int FreeCarryCapacity(string actor);
    bool HasCarriedOwnItem(string actor, string kind);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
}

internal interface IStorageOrdersWorld
{
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? CustodyOrderHouse(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, string? ownerId);
    string HouseholdFor(string actor);
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> PersonalStorageLots(string actor, string houseId);
    string? StorageOrderItemKind(global::ClankerWorld.Simulation.World.OrderTask order);
    string StorageOrderSubject(global::ClankerWorld.Simulation.World.OrderTask order);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? StorageOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string StorageOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.OwnerQueuedInstruction BindCustodyOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.PlacedBuilding house, string? lotId = null);
    global::ClankerWorld.Simulation.World.WorldRuntime.PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647, string? requiredHouseId = null, string? requiredHouseholdId = null, global::ClankerWorld.Simulation.World.GridPoint? requiredPosition = null);
    string PersonalStorageReceipt(string move);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
}

internal interface IToolRepairOrdersWorld
{
    global::ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null);
    string[] ToolRepairOrderTargets(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string ToolRepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string? RepairTool(string actor, global::ClankerWorld.Simulation.World.AgentState state, string lotId, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null);
    string ToolRepairReceipt(string actor, long tick, string lot);
    long WorldTick { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    int UnreservedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
}

internal interface ITreePlantingOrdersWorld
{
    global::ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, global::ClankerWorld.Simulation.World.GridPoint origin);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    global::ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(global::ClankerWorld.Simulation.World.GridPoint point, (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) obstacles);
    (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles();
    bool TreeAreaFull(global::ClankerWorld.Simulation.World.GridPoint point);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    string PlantingSpecies(global::ClankerWorld.Simulation.World.GridPoint site);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    int PlantingSeedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null);
    int PickupCarryCapacity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.GridPoint? TreeOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string TreeOrderSpecies(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.GridPoint site);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedTreeOrderSeed(string actor, string kind);
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CollectableTreeOrderSeed(string actor, string kind, global::ClankerWorld.Simulation.World.GridPoint site);
    global::ClankerWorld.Simulation.World.OrderSystem.TreeOrderPlan? TreePlantingOrderPlan(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647);
    void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, global::ClankerWorld.Simulation.World.GridPoint destination);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string TreePlantingReceipt(string treeId);
}
