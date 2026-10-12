using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class FarmingSystemTests
{
    [Fact]
    public void CropPhasesAndEventsLeaveCommittedAndDiscardedSnapshotsAlone()
    {
        var input = new List<FarmFieldState>
        {
            new(new(1, 1), "household", FarmFieldStage.Planted, FarmFieldRules.Grain,
                PlantedTick: 10, ReadyTick: 20, Tended: true, LastWorkedTick: 10),
        };
        var committed = new FarmingSystem(new(input), "farming-system");
        var original = committed.State;
        var proposed = committed.Fork();
        input.Clear();
        var world = new FakeWorld { WorldTick = 11 };
        proposed.MaintainFarmFields(world);
        Assert.Equal(FarmFieldStage.Growing, Assert.Single(proposed.State.Fields).Stage);
        var discarded = proposed.Fork();
        world.WorldTick = 20;
        proposed.MaintainFarmFields(world);
        Assert.Equal(FarmFieldStage.Ready, Assert.Single(proposed.State.Fields).Stage);
        Assert.Equal(("field_ready", "field-1-1"), Assert.Single(world.Events));
        Assert.Equal(FarmFieldStage.Growing, Assert.Single(discarded.State.Fields).Stage);
        Assert.Same(original, committed.State);
        Assert.Equal(FarmFieldStage.Planted, Assert.Single(committed.State.Fields).Stage);
    }

    [Fact]
    public void LostWorkerReleasesTheActualSeedAndLeavesThePreparedField()
    {
        var world = new FakeWorld();
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Reserve(
            InventoryAuthority.AddLot(inventory, "seed", FarmFieldRules.GrainSeed, "agent", 1),
            "planting", "agent", "seed", 1, "field_planting", long.MaxValue));
        var field = new FarmFieldState(new(1, 1), "household", FarmFieldStage.Prepared,
            Work: new("agent", FarmWorkKind.Plant, 4, 0, "planting", FarmFieldRules.Grain));
        var committed = new FarmingSystem(new([field]), "farming-system");
        var proposed = committed.Fork();
        proposed.CancelFieldWorkForUnavailableWorkers(world);
        Assert.Null(Assert.Single(proposed.State.Fields).Work);
        Assert.Equal(FarmFieldStage.Prepared, Assert.Single(proposed.State.Fields).Stage);
        Assert.Equal(InventoryReservationState.Released, Assert.Single(world.Society.Inventory.Reservations).State);
        Assert.Equal(1, world.Society.Inventory.GetLot("seed").Quantity);
        Assert.Equal(("field_work_interrupted", "field-1-1"), Assert.Single(world.Events));
        Assert.Equal(field, Assert.Single(committed.State.Fields));
    }

    [Fact]
    public void IdleExpiryUsesCurrentTimeAndReleasesOnlyItsReplantingReserve()
    {
        var world = new FakeWorld();
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Reserve(
            InventoryAuthority.AddLot(inventory, "seed", FarmFieldRules.GrainSeed, "household", 2),
            "replant", "household", "seed", 1, "field_replanting", long.MaxValue));
        var field = new FarmFieldState(new(1, 1), "household", FarmFieldStage.Harvested,
            FarmFieldRules.Grain, PlantedTick: 1, ReadyTick: 2, Tended: true,
            ReplantingReservationId: "replant", LastWorkedTick: 3);
        var system = new FarmingSystem(new([field]), "farming-system");
        var deadline = 3 + FarmFieldRules.IdleTicksBeforeGrass(world.Environment.Config);
        world.WorldTick = deadline - 1;
        system.MaintainFarmFields(world);
        Assert.Equal(field, Assert.Single(system.State.Fields));
        Assert.Empty(world.Events);
        world.WorldTick = deadline;
        system.MaintainFarmFields(world);
        Assert.Empty(system.State.Fields);
        Assert.Equal(InventoryReservationState.Released, Assert.Single(world.Society.Inventory.Reservations).State);
        Assert.Equal(2, world.Society.Inventory.GetLot("seed").Quantity);
        Assert.Equal(("field_returned_to_grass", "field-1-1:household"), Assert.Single(world.Events));
    }

    [Fact]
    public void FertilityIsSharedByForksAndInvalidatedByTerrainSnapshots()
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var world = new FakeWorld();
        var system = new FarmingSystem(FarmingState.Empty, "farming-system");
        var original = system.Fertility(world.Map);
        Assert.Same(original, system.Fork().Fertility(world.Map));
        var changed = world.Map with
        {
            Tiles = world.Map.Tiles.Select(tile => tile.Position == new GridPoint(1, 1)
                ? tile with { Terrain = TerrainKind.Lake } : tile).ToArray(),
        };
        var replacement = system.Fertility(changed);
        Assert.NotSame(original, replacement);
        Assert.True(original.CanFarm(new(1, 1)));
        Assert.False(replacement.CanFarm(new(1, 1)));
        var expected = new LandFertility(changed, "farming-system");
        foreach (var tile in changed.Tiles) Assert.Equal(expected.At(tile.Position), replacement.At(tile.Position));
    }

    [Fact]
    public void PlantingObstaclesUseTheCurrentRoadsAndResources()
    {
        var world = new FakeWorld();
        var point = new GridPoint(1, 1);
        Assert.Null(FarmingSystem.PlantingSiteRefusal(world, point, FarmingSystem.PlantingObstacles(world)));
        world.Roads.Add(point);
        Assert.Equal(TreePlantingRefusal.Road,
            FarmingSystem.PlantingSiteRefusal(world, point, FarmingSystem.PlantingObstacles(world)));
        world.Roads.Clear();
        world.SetMap(world.Map with { Resources = [new("tree", "wood", point, true)] });
        Assert.Equal(TreePlantingRefusal.Occupied,
            FarmingSystem.PlantingSiteRefusal(world, point, FarmingSystem.PlantingObstacles(world)));
    }

    private sealed class FakeWorld : IFarmingWorld
    {
        public long WorldTick { get; set; }
        public WorldMap Map { get; private set; } = new(8, 8, 0,
            Enumerable.Range(0, 64).Select(index => new TerrainTile(new(index % 8, index / 8), TerrainKind.Meadow)).ToArray(),
            [], [], "farming-system");
        public SocietyCheckpoint Society { get; private set; } = SocietyRules.CreateHousehold(
            SocietyRules.CreateGenesis("farming-system", [SocietyRules.CreateFounder("agent", "Aster Ash")]),
            "household", "Home", ["agent"]).Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents { get; } =
            new Dictionary<string, AgentState>();
        public WorldContentSimulationState Simulation => WorldContentSimulationState.Empty;
        public DeclarativeWorldContentState Content { get; } = new([], []);
        public IReadOnlyList<TownRuntimeState> Towns => [];
        public WorldSystemsState Environment { get; } = WorldSystemsRules.CreateGenesis("farming-system");
        public GroundOccupancy Ground => new(Map, Content, Simulation);
        public List<(string, string)> Events { get; } = [];
        public HashSet<GridPoint> Roads { get; } = [];
        public void AppendEvent(string kind, string detail) => Events.Add((kind, detail));
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) =>
            Society = Society with { Inventory = transition(Society.Inventory) };
        public void SetMap(WorldMap map) => Map = map;
        public IEnumerable<GridPoint> RoadAndBridgeTiles() => Roads;
        public SocietyCognitionSchedulerState Cognition => throw new NotSupportedException();
        public bool AdultResident(string actor) => throw new NotSupportedException();
        public PlacedBuilding? FarmhouseForHousehold(string householdId) => throw new NotSupportedException();
        public bool NeedsUrgentFood(AgentState person) => throw new NotSupportedException();
        public bool NeedsUrgentWarmth(AgentState person) => throw new NotSupportedException();
        public bool IsConversationBusy(string actor) => throw new NotSupportedException();
        public string? HouseholdFor(string actor) => throw new NotSupportedException();
        public HashSet<GridPoint> HouseholdLandHeldByOthers(string householdId) => throw new NotSupportedException();
        public int AvailableLotQuantity(InventoryLot lot) => throw new NotSupportedException();
        public void ApplySocietyTransition(Func<SocietyCheckpoint, SocietyOperationResult> transition) => throw new NotSupportedException();
        public int SkilledWorkTicks(string actor, SettlementSkillKind skill, int ticks) => throw new NotSupportedException();
        public IEnumerable<GridPoint> TownProjectProtectedSites() => throw new NotSupportedException();
        public IEnumerable<GridPoint> MarketSiteTiles() => throw new NotSupportedException();
        public void RecordAttemptedAction(string actor, string candidate) => throw new NotSupportedException();
        public void ApplyToolWork(string actor, params ToolWorkPlan[] plans) => throw new NotSupportedException();
        public void CreditCompletedWork(string actor, string domain, SettlementSkillKind? skill = null) => throw new NotSupportedException();
        public WeatherKind WeatherAt(GridPoint position) => throw new NotSupportedException();
        public bool CanReachByFootOrSwimming(string actor, GridPoint from, GridPoint destination) => throw new NotSupportedException();
        public int FreeCarryCapacity(string actor) => throw new NotSupportedException();
        public bool OnBorrowedMarketStall(InventoryLot lot) => throw new NotSupportedException();
        public int PickupCarryCapacity(string actor, InventoryLot stock, GridPoint destination, int destinationRange = 0) => throw new NotSupportedException();
        public GridPoint HouseholdStockPosition(InventoryLot lot) => throw new NotSupportedException();
        public bool CanWalkToFieldOrderSite(string actor, GridPoint from, GridPoint destination, int range = 0) => throw new NotSupportedException();
        public void MoveToward(string actor, AgentState state, GridPoint destination, string reason, int interactionRange = 0) => throw new NotSupportedException();
        public bool HasCarriedOwnItem(string actor, string kind) => throw new NotSupportedException();
        public InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => throw new NotSupportedException();
        public IReadOnlyList<GridPoint> FindUnoccupiedRoute(string actor, GridPoint from, GridPoint destination, int range = 0) => throw new NotSupportedException();
        public bool IsWithinInteractionRange(GridPoint from, GridPoint destination, int range) => throw new NotSupportedException();
        public void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0) => throw new NotSupportedException();
        public void SyncEcologyResourceStates() => throw new NotSupportedException();
        public bool RecordKnowledgeFact(string actor, GridPoint position) => throw new NotSupportedException();
        public long EdibleFoodHoldings(IEnumerable<string> owners) => throw new NotSupportedException();
        public void ReplaceEcologyResource(string id, EcologyResource replacement) => throw new NotSupportedException();
        public void PlantTree(MapResource tree, long day, ChunkManifest chunk) => throw new NotSupportedException();
    }
}
