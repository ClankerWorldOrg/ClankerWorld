using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFarmingWorld
{
    long WorldTick { get; }
    WorldMap Map { get; }
    SocietyCheckpoint Society { get; }
    SocietyCognitionSchedulerState Cognition { get; }
    IReadOnlyDictionary<string, InhabitantState> Inhabitants { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldContentSimulationState Simulation { get; }
    DeclarativeWorldContentState Content { get; }
    WorldSystemsState Environment { get; }
    GroundOccupancy Ground { get; }
    bool AdultResident(string actor);
    PlacedBuilding? FarmhouseForHousehold(string householdId);
    bool NeedsUrgentFood(InhabitantState person);
    bool NeedsUrgentWarmth(InhabitantState person);
    bool IsConversationBusy(string actor);
    string? HouseholdFor(string actor);
    HashSet<GridPoint> HouseholdLandHeldByOthers(string householdId);
    int AvailableLotQuantity(InventoryLot lot);
    void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition);
    void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> transition);
    int SkilledWorkTicks(string actor, SettlementSkillKind skill, int ticks);
    void AppendEvent(string kind, string detail);
    IEnumerable<GridPoint> TownProjectProtectedSites();
    IEnumerable<GridPoint> MarketSiteTiles();
    IEnumerable<GridPoint> RoadAndBridgeTiles();
    void RecordAttemptedAction(string actor, string candidate);
    void ApplyToolWork(string actor, params ToolWorkPlan[] plans);
    void CreditCompletedWork(string actor, string domain, SettlementSkillKind? skill = null);
    WeatherKind WeatherAt(GridPoint position);
    bool CanReachByFootOrSwimming(string actor, GridPoint from, GridPoint destination);
    int FreeCarryCapacity(string actor);
    bool OnBorrowedMarketStall(InventoryLot lot);
    int PickupCarryCapacity(string actor, InventoryLot stock, GridPoint destination, int destinationRange = 0);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    bool CanWalkToFieldOrderSite(string actor, GridPoint from, GridPoint destination, int range = 0);
    void MoveToward(string actor, InhabitantState state, GridPoint destination, string reason, int interactionRange = 0);
    bool HasCarriedOwnItem(string actor, string kind);
    InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
    IReadOnlyList<GridPoint> FindUnoccupiedRoute(string actor, GridPoint from, GridPoint destination, int range = 0);
    bool IsWithinInteractionRange(GridPoint from, GridPoint destination, int range);
    void CollectEquipment(string actor, InhabitantState person, string kind, GridPoint? returnTo = null, int returnRange = 0);
    void SyncEcologyResourceStates();
    bool RecordKnowledgeFact(string actor, GridPoint position);
    long EdibleFoodHoldings(IEnumerable<string> owners);
    void ReplaceEcologyResource(string id, EcologyResource replacement);
    void PlantTree(MapResource tree, long day, ChunkManifest chunk);
    void SetMap(WorldMap map);
}
