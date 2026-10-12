using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private sealed class AnimalWorldAdapter(PrivateWorldRuntime runtime) : IAnimalWorld
    {
        public bool AdultResident(string actor) => runtime.AdultResident(actor);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) => runtime.ApplyInventoryTransition(transition);
        public InventoryLot? AttachedHandcart(string actor) => runtime.AttachedHandcart(actor);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public bool CanRemoveWorkstationStock(InventoryCheckpoint inventory, InventoryLot root, int quantity) => runtime.CanRemoveWorkstationStock(inventory, root, quantity);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public GoodsAnswer FindGoods(GoodsRequest request) => runtime.FindGoods(request);
        public List<GridPoint> FindUnoccupiedRoute(string inhabitantId, GridPoint origin, GridPoint destination, int interactionRange, bool allowSwimming = true, int additionalCarriedUnits = 0) => runtime.FindUnoccupiedRoute(inhabitantId, origin, destination, interactionRange, allowSwimming, additionalCarriedUnits);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public GridPoint[] FreshWaterShorePositions() => runtime.FreshWaterShorePositions();
        public long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds) => runtime.HoldingsTotal(owners, kinds);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MoveToward(string inhabitantId, PlaytestInhabitantState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(inhabitantId, state, destination, reason, interactionRange, horseMovementBudget);
        public BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string inhabitantId) => runtime.PendingInstructionFor(inhabitantId);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lotId) => runtime.RecheckGoods(request, lotId);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public List<GridPoint> SharedUnoccupiedRoute(GridPoint origin, HashSet<GridPoint> occupied, GridPoint destination, int interactionRange, bool swimming = false) => runtime.SharedUnoccupiedRoute(origin, occupied, destination, interactionRange, swimming);
        public void StageBuiltInContent(string packageId, string requiredActivePackageId, Func<ContentPackageManifest> create, string eventKind) => runtime.StageBuiltInContent(packageId, requiredActivePackageId, create, eventKind);
        public void SyncEcologyResourceStates() => runtime.SyncEcologyResourceStates();
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public long WorldTick => runtime.WorldTick;
        public SeededMap Map => runtime.map;
        public WorldSystemsState Environment => runtime.worldSystems;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyDictionary<string, PlaytestInhabitantState> Inhabitants => runtime.inhabitants;
        public string Seed => runtime.worldSeed;
        public GeographyOptions? Geography => runtime.geographyOptions;
        public long NextEventId => runtime.nextEventId;
        public GroundOccupancy Ground => runtime.Ground;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyList<ContentPackageRecord> Packages => runtime.contentRegistry.ExportState().Packages;
        public GoodsKinds EdibleFoodGoods => PrivateWorldRuntime.EdibleFoodGoods;
        public void SetInhabitant(string id, PlaytestInhabitantState state) => runtime.inhabitants[id] = state;
        public void ReplaceEcologyResource(string id, EcologyResource resource) => runtime.environmentSystem.ReplaceEcologyResource(id, resource);
    }

    internal static class AnimalSharedRules
    {
        internal static bool ActiveParenthood(SettlementParenthood? plan) => PrivateWorldRuntime.ActiveParenthood(plan);
        internal static string AnimalActionId(string action, string animal, string? other) => PrivateWorldRuntime.AnimalActionId(action, animal, other);
        internal static string AnimalAgreementId(string action, string animal, string other, string? lot) => PrivateWorldRuntime.AnimalAgreementId(action, animal, other, lot);
        internal static string AnimalBornId(string mother, long startedTick) => PrivateWorldRuntime.AnimalBornId(mother, startedTick);
        internal static string AnimalCargoDismountId(string rider, long tick, long eventId, string lot) => PrivateWorldRuntime.AnimalCargoDismountId(rider, tick, eventId, lot);
        internal static string AnimalHerdId(string household) => PrivateWorldRuntime.AnimalHerdId(household);
        internal static string AnimalHideId(string animal) => PrivateWorldRuntime.AnimalHideId(animal);
        internal static string AnimalHideTransferId(string actor, string animal, long tick, bool carry) => PrivateWorldRuntime.AnimalHideTransferId(actor, animal, tick, carry);
        internal static string AnimalInputReservationId(string actor, long tick, long eventId, string purpose, int index) => PrivateWorldRuntime.AnimalInputReservationId(actor, tick, eventId, purpose, index);
        internal static string AnimalOfferId(string actor, string animal, long tick, long eventId) => PrivateWorldRuntime.AnimalOfferId(actor, animal, tick, eventId);
        internal static string AnimalOrderReceiptId(string instruction, string animal, string? completion) => PrivateWorldRuntime.AnimalOrderReceiptId(instruction, animal, completion);
        internal static string AnimalPickupId(string actor, long tick, long eventId) => PrivateWorldRuntime.AnimalPickupId(actor, tick, eventId);
        internal static string AnimalProductId(string animal, long tick) => PrivateWorldRuntime.AnimalProductId(animal, tick);
        internal static string AnimalProductReservationId(string product) => PrivateWorldRuntime.AnimalProductReservationId(product);
        internal static string AnimalSaddleOperationId(string animal, long tick, long eventId) => PrivateWorldRuntime.AnimalSaddleOperationId(animal, tick, eventId);
        internal static string AnimalSaddleReservationId(string operation) => PrivateWorldRuntime.AnimalSaddleReservationId(operation);
        internal static string AnimalSupplyId(string lot, string yard, string? animal, string? action) => PrivateWorldRuntime.AnimalSupplyId(lot, yard, animal, action);
        internal static string AnimalSupplyTripId(string actor) => PrivateWorldRuntime.AnimalSupplyTripId(actor);
        internal static bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => PrivateWorldRuntime.HasActiveContainerReservation(inventory, containerId);
        internal static int HouseholdStockInteractionRange(InventoryLot lot) => PrivateWorldRuntime.HouseholdStockInteractionRange(lot);
        internal static bool IsEdibleFood(string kind) => PrivateWorldRuntime.IsEdibleFood(kind);
        internal const int ResourceInteractionRange = PrivateWorldRuntime.ResourceInteractionRange;
    }
}
