using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IAnimalWorld
{
    bool AdultResident(string actor);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition);
    InventoryLot? AttachedHandcart(string actor);
    int AvailableLotQuantity(InventoryLot lot);
    bool CanRemoveWorkstationStock(InventoryCheckpoint inventory, InventoryLot root, int quantity);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    GoodsAnswer FindGoods(GoodsRequest request);
    List<GridPoint> FindUnoccupiedRoute(string inhabitantId, GridPoint origin, GridPoint destination, int interactionRange, bool allowSwimming = true, int additionalCarriedUnits = 0);
    int FreeCarryCapacity(string actor);
    GridPoint[] FreshWaterShorePositions();
    long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds);
    PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
    string HouseholdFor(string actor);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    void MoveToward(string inhabitantId, InhabitantState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    BoatState? PassengerBoat(string actor);
    OwnerQueuedInstruction? PendingInstructionFor(string inhabitantId);
    int PhysicalUnreservedQuantity(InventoryLot lot);
    int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lotId);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    List<GridPoint> SharedUnoccupiedRoute(GridPoint origin, HashSet<GridPoint> occupied, GridPoint destination, int interactionRange, bool swimming = false);
    void StageBuiltInContent(string packageId, string requiredActivePackageId, Func<ContentPackageManifest> create, string eventKind);
    void SyncEcologyResourceStates();
    bool VesselFits(InventoryLot lot, int room);
    long WorldTick { get; }
    WorldMap Map { get; }
    WorldSystemsState Environment { get; }
    WorldContentSimulationState Simulation { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyDictionary<string, InhabitantState> Inhabitants { get; }
    string Seed { get; }
    GeographyOptions? Geography { get; }
    long NextEventId { get; }
    GroundOccupancy Ground { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyList<ContentPackageRecord> Packages { get; }
    GoodsKinds EdibleFoodGoods { get; }
    void SetInhabitant(string id, InhabitantState state);
    void ReplaceEcologyResource(string id, EcologyResource resource);
}
