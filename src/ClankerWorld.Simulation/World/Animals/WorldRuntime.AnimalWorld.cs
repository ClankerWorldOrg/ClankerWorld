using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed class AnimalWorldAdapter(WorldRuntime runtime) : IAnimalWorld
    {
        public AgentStore PhysicalAgents => runtime.agentStore;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public System.Collections.ObjectModel.ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.Reach(actor, target, purpose, range, load, swimming);
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => runtime.RefuseChoice(plan, refusal);
        public void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void RunInventoryCommand(InventorySystem.Command command) => runtime.RunInventoryCommand(command);
        public InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(runtime.HandcartWorld, actor);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public int AnimalYardFeedStock(string yardId) => runtime.AnimalYardFeedStock(yardId);
        public bool CanRemoveWorkstationStock(InventoryCheckpoint inventory, InventoryLot root, int quantity) => runtime.CanRemoveWorkstationStock(inventory, root, quantity);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public GoodsAnswer FindGoods(GoodsRequest request) => runtime.FindGoods(request);
        public IReadOnlyList<GoodsMatch> DismountCargo(string actor) => runtime.DismountCargo(actor);
        public IReadOnlyList<GoodsMatch> AnimalPayments(string actor) => runtime.AnimalPayments(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public GridPoint[] FreshWaterShorePositions() => runtime.FreshWaterShorePositions();
        public long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds) => runtime.HoldingsTotal(owners, kinds);
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public BoatState? PassengerBoat(string actor) => runtime.PassengerBoat(actor);
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => runtime.PendingInstructionFor(agentId);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lotId) => runtime.RecheckGoods(request, lotId);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public List<GridPoint> SharedUnoccupiedRoute(GridPoint origin, HashSet<GridPoint> occupied, GridPoint destination, int interactionRange, bool swimming = false) => runtime.SharedUnoccupiedRoute(origin, occupied, destination, interactionRange, swimming);
        public void StageBuiltInContent(string packageId, string requiredActivePackageId, Func<ContentPackageManifest> create, string eventKind) => runtime.StageBuiltInContent(packageId, requiredActivePackageId, create, eventKind);
        public void SyncEcologyResourceStates() => runtime.SyncEcologyResourceStates();
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public long WorldTick => runtime.WorldTick;
        public WorldMap Map => runtime.map;
        public WorldSystemsState Environment => runtime.worldSystems;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public string Seed => runtime.worldSeed;
        public GeographyOptions? Geography => runtime.geographyOptions;
        public long NextEventId => runtime.nextEventId;
        public GroundOccupancy Ground => runtime.Ground;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyList<ContentPackageRecord> Packages => runtime.contentRegistry.ExportState().Packages;
        public GoodsKinds EdibleFoodGoods => WorldRuntime.EdibleFoodGoods;
        public void ReplaceEcologyResource(string id, EcologyResource resource) => runtime.environmentSystem.ReplaceEcologyResource(id, resource);
    }

    internal static class AnimalSharedRules
    {
        internal static bool ActiveParenthood(ParenthoodPlan? plan) => FamilySystem.ActiveParenthood(plan);
        internal static string AnimalActionId(string action, string animal, string? other) => WorldRuntime.AnimalActionId(action, animal, other);
        internal static string AnimalAgreementId(string action, string animal, string other, string? lot) => WorldRuntime.AnimalAgreementId(action, animal, other, lot);
        internal static string AnimalBornId(string mother, long startedTick) => WorldRuntime.AnimalBornId(mother, startedTick);
        internal static string AnimalCargoDismountId(string rider, long tick, long eventId, string lot) => WorldRuntime.AnimalCargoDismountId(rider, tick, eventId, lot);
        internal static string AnimalHerdId(string household) => WorldRuntime.AnimalHerdId(household);
        internal static string AnimalHideId(string animal) => WorldRuntime.AnimalHideId(animal);
        internal static string AnimalHideTransferId(string actor, string animal, long tick, bool carry) => WorldRuntime.AnimalHideTransferId(actor, animal, tick, carry);
        internal static string AnimalInputReservationId(string actor, long tick, long eventId, string purpose, int index) => WorldRuntime.AnimalInputReservationId(actor, tick, eventId, purpose, index);
        internal static string AnimalOfferId(string actor, string animal, long tick, long eventId) => WorldRuntime.AnimalOfferId(actor, animal, tick, eventId);
        internal static string AnimalOrderReceiptId(string instruction, string animal, string? completion) => WorldRuntime.AnimalOrderReceiptId(instruction, animal, completion);
        internal static string AnimalPickupId(string actor, long tick, long eventId) => WorldRuntime.AnimalPickupId(actor, tick, eventId);
        internal static string AnimalProductId(string animal, long tick) => WorldRuntime.AnimalProductId(animal, tick);
        internal static string AnimalProductReservationId(string product) => WorldRuntime.AnimalProductReservationId(product);
        internal static string AnimalSaddleOperationId(string animal, long tick, long eventId) => WorldRuntime.AnimalSaddleOperationId(animal, tick, eventId);
        internal static string AnimalSaddleReservationId(string operation) => WorldRuntime.AnimalSaddleReservationId(operation);
        internal static string AnimalSupplyId(string lot, string yard, string? animal, string? action) => WorldRuntime.AnimalSupplyId(lot, yard, animal, action);
        internal static string AnimalSupplyTripId(string actor) => WorldRuntime.AnimalSupplyTripId(actor);
        internal static bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        internal static int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        internal static bool IsEdibleFood(string kind) => WorldRuntime.IsEdibleFood(kind);
        internal const int ResourceInteractionRange = WorldRuntime.ResourceInteractionRange;
    }
}
