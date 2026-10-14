using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed class FarmingWorldAdapter(WorldRuntime runtime) : IFarmingWorld
    {
        public long WorldTick => runtime.WorldTick;
        public WorldMap Map => runtime.map;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public SocietyCognitionSchedulerState Cognition => runtime.society.Capture().Cognition;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public WorldContentSimulationState Simulation => runtime.worldSimulation;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public WorldSystemsState Environment => runtime.worldSystems;
        public GroundOccupancy Ground => runtime.Ground;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public PlacedBuilding? FarmhouseForHousehold(string householdId) => runtime.FarmhouseForHousehold(householdId);
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool IsConversationBusy(string actor) => runtime.IsConversationBusy(actor);
        public string? HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public HashSet<GridPoint> HouseholdLandHeldByOthers(string householdId) => runtime.HouseholdLandHeldByOthers(householdId);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) => runtime.ApplyInventoryTransition(transition);
        public void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> transition) => runtime.society.Apply(transition);
        public int SkilledWorkTicks(string actor, SkillKind skill, int ticks) => runtime.SkilledWorkTicks(actor, skill, ticks);
        public void AppendEvent(WorldEventPayload payload) => runtime.AppendEvent(payload);
        public IEnumerable<GridPoint> TownProjectProtectedSites() => runtime.TownProjectProtectedSites();
        public IEnumerable<GridPoint> MarketSiteTiles() => runtime.MarketSiteTiles();
        public IEnumerable<GridPoint> RoadAndBridgeTiles() => runtime.RoadAndBridgeTiles();
        public void RecordAttemptedAction(string actor, string candidate) => runtime.RecordAttemptedAction(actor, candidate);
        public void ApplyToolWork(string actor, params ToolWorkPlan[] plans) => runtime.ApplyToolWork(actor, plans);
        public void CreditCompletedWork(string actor, string domain, SkillKind? skill = null) => runtime.CreditCompletedWork(actor, domain, skill);
        public WeatherKind WeatherAt(GridPoint position) => runtime.WeatherAt(position);
        public bool CanReachByFootOrSwimming(string actor, GridPoint from, GridPoint destination) => runtime.CanReachByFootOrSwimming(actor, from, destination);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool OnBorrowedMarketStall(InventoryLot lot) => runtime.OnBorrowedMarketStall(lot);
        public int PickupCarryCapacity(string actor, InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public bool CanWalkToFieldOrderSite(string actor, GridPoint from, GridPoint destination, int range = 0) => runtime.CanWalkToFieldOrderSite(actor, from, destination, range);
        public void MoveToward(string actor, AgentState state, GridPoint destination, string reason, int interactionRange = 0) => runtime.MoveToward(actor, state, destination, reason, interactionRange);
        public bool HasCarriedOwnItem(string actor, string kind) => runtime.HasCarriedOwnItem(actor, kind);
        public InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public IReadOnlyList<GridPoint> FindUnoccupiedRoute(string actor, GridPoint from, GridPoint destination, int range = 0) => runtime.FindUnoccupiedRoute(actor, from, destination, range);
        public bool IsWithinInteractionRange(GridPoint from, GridPoint destination, int range) => runtime.IsWithinInteractionRange(from, destination, range);
        public void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange);
        public void SyncEcologyResourceStates() => runtime.SyncEcologyResourceStates();
        public bool RecordKnowledgeFact(string actor, GridPoint position) => runtime.RecordKnowledgeFact(actor, position);
        public long EdibleFoodHoldings(IEnumerable<string> owners) => runtime.HoldingsTotal(owners, EdibleFoodGoods);
        public void ReplaceEcologyResource(string id, EcologyResource replacement) => runtime.environmentSystem.ReplaceEcologyResource(id, replacement);
        public void PlantTree(MapResource tree, long day, ChunkManifest chunk) => runtime.environmentSystem.PlantTree(tree, day, chunk);
        public void SetMap(WorldMap map) => runtime.map = map;
    }

    internal static class FarmingSharedRules
    {
        internal static string FieldPlantingReservationId(GridPoint position, int cycle, long tick, string worker) => WorldRuntime.FieldPlantingReservationId(position, cycle, tick, worker);
        internal static string FieldToolLotId(string lotId, GridPoint position, long tick, string worker) => WorldRuntime.FieldToolLotId(lotId, position, tick, worker);
        internal static string FieldHarvestId(GridPoint position, int cycle, string kind) => WorldRuntime.FieldHarvestId(position, cycle, kind);
        internal static IEnumerable<GridPoint> ExpansionTiles(BuildingExpansionJob job) => WorldRuntime.ExpansionTiles(job);
        internal static InventoryCheckpoint ApplyToolWorkToInventory(InventoryCheckpoint inventory, string actor, long tick, IReadOnlyList<ToolWorkPlan> plans) => WorldRuntime.ApplyToolWorkToInventory(inventory, actor, tick, plans);
        internal static int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
    }
}
