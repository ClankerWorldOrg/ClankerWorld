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
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    WorldContentSimulationState Simulation { get; }
    DeclarativeWorldContentState Content { get; }
    WorldSystemsState Environment { get; }
    GroundOccupancy Ground { get; }
    bool IsAdult(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    IReadOnlyList<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load,
        SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles);
    GoodsAnswer FindGoods(GoodsRequest request);
    GoodsMatch? RecheckGoods(GoodsRequest request, string stockId);
    PlacedBuilding? FarmhouseForHousehold(string householdId);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    bool IsConversationBusy(string actor);
    string? HouseholdFor(string actor);
    HashSet<GridPoint> HouseholdLandHeldByOthers(string householdId);
    int PlantingSeedQuantity(InventoryLot lot);
    int AvailableLotQuantity(InventoryLot lot);
    void RunInventoryCommand(InventorySystem.Command command);
    void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> transition);
    int SkilledWorkTicks(string actor, SkillKind skill, int ticks);
    void AppendEvent(string kind, string detail);
    IEnumerable<GridPoint> TownProjectProtectedSites();
    IEnumerable<GridPoint> MarketSiteTiles();
    IEnumerable<GridPoint> RoadAndBridgeTiles();
    void RecordAttemptedAction(string actor, string candidate);
    void ApplyToolWork(string actor, params ToolWorkPlan[] plans);
    void CreditCompletedWork(string actor, string domain, SkillKind? skill = null);
    WeatherKind WeatherAt(GridPoint position);
    int FreeCarryCapacity(string actor);
    bool OnBorrowedMarketStall(InventoryLot lot);
    int PickupCarryCapacity(string actor, InventoryLot stock, GridPoint destination, int destinationRange = 0);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load,
        SwimmingPolicy swimming, ReachObstacles obstacles = ReachObstacles.OccupiedTiles);
    void MoveToward(string actor, AgentState state, GridPoint destination, string reason, int interactionRange = 0);
    void PickUpEquipment(string actor, PlannedEquipmentPickup pickup);
    bool HasCarriedOwnItem(string actor, string kind);
    InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
    bool IsWithinInteractionRange(GridPoint from, GridPoint destination, int range);
    void SyncEcologyResourceStates();
    bool RecordKnowledgeFact(string actor, GridPoint position);
    long EdibleFoodHoldings(IEnumerable<string> owners);
    void ReplaceEcologyResource(string id, EcologyResource replacement);
    void PlantTree(MapResource tree, long day, ChunkManifest chunk);
    void SetMap(WorldMap map);
}
