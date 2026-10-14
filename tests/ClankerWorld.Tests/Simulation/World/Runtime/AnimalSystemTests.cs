using ClankerWorld.Simulation;
using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class AnimalSystemTests
{
    [Fact]
    public async Task TrustedPreparationSharesTheSnapshotWithADistinctSystemOwner()
    {
        using var committed = NormalPathWorld.CreateGenerated("animal-wild-arrival", _ => new DeterministicDecisionProvider());
        Assert.True((await committed.AdvanceOneTickAsync()).Advanced);
        var field = typeof(WorldRuntime).GetField("animalSystem", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = (AnimalSystem)field.GetValue(committed)!;
        var before = WorldRuntimeCodec.Encode(committed.ExportState());
        var restore = typeof(WorldRuntime).GetMethod("RestoreCore", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var prepared = (WorldRuntime)restore.Invoke(null, [committed.ExportState(), null, 4, true, false, null, null, null])!;
        var proposed = (AnimalSystem)field.GetValue(prepared)!;
        Assert.NotSame(original, proposed);
        Assert.Same(original.State, proposed.State);
        var animal = Assert.Single(proposed.State.Animals.Take(1));

        proposed.SetAnimal(animal with { CareUntilTick = prepared.WorldTick + 1 });

        Assert.NotSame(original.State, proposed.State);
        Assert.Equal(animal, original.Animal(animal.Id));
        Assert.Equal(before, WorldRuntimeCodec.Encode(committed.ExportState()));
        committed.Validate();
    }

    [Theory]
    [InlineData("chicken", 1, 1)]
    [InlineData("sheep", 2, 2)]
    [InlineData("cow", 2, 2)]
    public void CareSpendsPhysicalFeedAndSplitJugWaterWithoutChangingAnotherOwner(string species, int feed, int water)
    {
        var world = new FakeWorld();
        world.AddCareGoods(feed, water);
        var animal = world.OwnedAnimal(species);
        var initial = new AnimalWorldState(true, [animal], []);
        var committed = new AnimalSystem(initial);
        var proposed = new AnimalSystem(initial);

        var care = proposed.AnimalChoices(world, "agent").First(choice => choice.Action == "care");
        Assert.True(proposed.ApplyAnimalCandidate(world, "agent", care.Id));

        Assert.Equal(world.WorldTick + world.Environment.Config.TicksPerDay, Assert.Single(proposed.State.Animals).CareUntilTick);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "feed" || lot.ItemKind == "fresh_water");
        Assert.Equal(water, world.Society.Inventory.Lots.Count(lot => lot.ItemKind == "water_jug"));
        Assert.All(world.Society.Inventory.Reservations, reservation => Assert.Equal(InventoryReservationState.Completed, reservation.State));
        Assert.Equal(("animal_cared", "agent:cow", animal.Position), Assert.Single(world.Events));
        Assert.Same(initial, committed.State);
        Assert.Equal(0, Assert.Single(committed.State.Animals).CareUntilTick);
    }

    [Fact]
    public void AReservationAddedAfterConstructionBlocksCareUntilTheCurrentWorldReleasesIt()
    {
        var world = new FakeWorld();
        world.AddCareGoods(2, 2);
        var animal = world.OwnedAnimal("cow");
        var initial = new AnimalWorldState(true, [animal], []);
        var system = new AnimalSystem(initial);
        world.ApplyInventoryTransition(inventory => InventoryAuthority.Reserve(inventory,
            "planting", "agent", "feed", 2, "planting", long.MaxValue));

        Assert.Null(AnimalSystem.AnimalCareInputs(world, "agent", animal, 2, 2));
        Assert.Same(initial, system.State);
        Assert.Equal(2, world.Society.Inventory.GetLot("feed").Quantity);
        Assert.Empty(world.Events);

        world.ApplyInventoryTransition(inventory => InventoryAuthority.ReleaseReservation(inventory, "planting", "cancelled"));
        var care = system.AnimalChoices(world, "agent").First(choice => choice.Action == "care");
        Assert.True(system.ApplyAnimalCandidate(world, "agent", care.Id));
        Assert.True(Assert.Single(system.State.Animals).CareUntilTick > world.WorldTick);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "feed");
    }

    [Fact]
    public void ProductAndNaturalDeathUseRealReservationsAndLeaveSharedAnimalStateUnchanged()
    {
        var world = new FakeWorld();
        var day = world.Environment.Config.TicksPerDay;
        var animal = world.OwnedAnimal("cow") with { CareUntilTick = 2L * day, ProductProgressTicks = day - 1 };
        var initial = new AnimalWorldState(true, [animal], []);
        var system = new AnimalSystem(initial);
        var discarded = new AnimalSystem(initial);

        system.AdvanceAnimals(world, world.WorldTick);

        var ready = Assert.Single(system.State.Animals);
        var product = world.Society.Inventory.GetLot(ready.ReadyProductLotId!);
        Assert.Equal(("milk", "household", 2), (product.ItemKind, product.OwnerId, product.Quantity));
        Assert.Equal(new InventoryGroundPosition(animal.Position.X, animal.Position.Y), product.GroundPosition);
        Assert.Equal(0, Math.Max(0, InventoryRules.AvailableQuantity(InventoryIndex.For(world.Society.Inventory), product)));
        Assert.Equal(InventoryReservationState.Reserved, Assert.Single(world.Society.Inventory.Reservations).State);
        Assert.Same(initial, discarded.State);

        world.WorldTick = animal.BornTick + (long)AnimalRules.Definition("cow").LifespanDays * day;
        system.AdvanceAnimals(world, world.WorldTick);

        Assert.Equal(world.WorldTick, Assert.Single(system.State.Animals).DiedTick);
        Assert.Null(Assert.Single(system.State.Animals).ReadyProductLotId);
        var hide = Assert.Single(world.Society.Inventory.Lots);
        Assert.Equal(("hide", "household", 1), (hide.ItemKind, hide.OwnerId, hide.Quantity));
        Assert.Equal(product.GroundPosition, hide.GroundPosition);
        Assert.Equal(InventoryReservationState.Released, Assert.Single(world.Society.Inventory.Reservations).State);
        Assert.Collection(world.Events,
            item => Assert.Equal("animal_product_ready", item.Kind),
            item => Assert.Equal("animal_died", item.Kind));
        Assert.Same(initial, discarded.State);
        Assert.Null(Assert.Single(discarded.State.Animals).DiedTick);
    }

    [Fact]
    public void ASecondWildAnimalSeesTheEcologyReplacementMadeByTheFirst()
    {
        var world = new FakeWorld();
        var source = new GridPoint(2, 1);
        world.Map = world.Map with { Resources = [new("greens", "food", source, true, NaturalObjectKind: "wild_greens")] };
        world.Environment = WorldSystemsRules.CreateGenesis("animal-system", resources:
            [new("greens", "food", source, true, 2, 2, 1, 1, SeasonKind.Spring, 1, EcologyResourceState.Available)]);
        var first = new AnimalState("first", "First", "cow", "female", 0, new(1, 1), "wild");
        var second = first with { Id = "second", Name = "Second", Position = new(2, 2) };
        var system = new AnimalSystem(new(true, [first, second], []));

        var fed = system.AdvanceWildAnimalCare(world, first, world.WorldTick);
        var unfed = system.AdvanceWildAnimalCare(world, second, world.WorldTick);

        Assert.Equal(world.WorldTick + world.Environment.Config.TicksPerDay, fed.WildFedUntilTick);
        Assert.Equal(0, unfed.WildFedUntilTick);
        Assert.Equal(0, world.Environment.Ecology.GetResource("greens").Quantity);
        Assert.Equal(1, world.EcologySyncs);
        Assert.Equal("greens", Assert.Single(world.ReplacedResources));
    }

    private sealed class FakeWorld : IAnimalWorld
    {
        public long WorldTick { get; set; } = 1;
        public long NextEventId => Events.Count + 1;
        public string Seed => "animal-system";
        public GeographyOptions? Geography => null;
        public WorldMap Map { get; set; } = new(8, 8, 0,
            Enumerable.Range(0, 64).Select(index => new TerrainTile(new(index % 8, index / 8), TerrainKind.Meadow)).ToArray(),
            [], [], "animal-system");
        public SocietyCheckpoint Society { get; private set; } = SocietyRules.CreateHousehold(
            SocietyRules.CreateGenesis("animal-system", [SocietyRules.CreateFounder("agent", "Aster Ash")]),
            "household", "Home", ["agent"]).Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents { get; } =
            new Dictionary<string, AgentState> { ["agent"] = new("agent", new(1, 1), 5000, 0, "curious", "care") };
        public WorldContentSimulationState Simulation => WorldContentSimulationState.Empty;
        public DeclarativeWorldContentState Content { get; } = new([], []);
        public WorldSystemsState Environment { get; set; } = WorldSystemsRules.CreateGenesis("animal-system");
        public GroundOccupancy Ground => new(Map, Content, Simulation);
        public List<(string Kind, string Detail, GridPoint? Position)> Events { get; } = [];
        public List<string> ReplacedResources { get; } = [];
        public int EcologySyncs { get; private set; }
        public bool IsAdult(string actor) => actor == "agent";
        public ActorCapabilityState? CapabilitiesFor(string actor) => new(Society.GetAgent(actor).AgeBand, false, false, false, false, false);
        public System.Collections.ObjectModel.ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) =>
            Map.FootDistance(Agents[actor].Position, target) <= range ? Array.AsReadOnly(new[] { Agents[actor].Position }) : throw new NotSupportedException();
        public void RefuseChoice(ActionPlan plan, ActionRefusal refusal) => Events.Add(("choice_refused", refusal.Explanation, null));
        public string HouseholdFor(string actor) => Society.GetAgent(actor).HouseholdId!;
        public void AppendEvent(WorldEventPayload payload, GridPoint? eventPosition = null) => Events.Add((payload.Kind, payload.DiagnosticDetail, eventPosition));
        public void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition) => Society = Society with { Inventory = transition(Society.Inventory) };
        public int AnimalYardFeedStock(string yardId) => throw new NotSupportedException();
        public int AvailableLotQuantity(InventoryLot lot) => Math.Max(0, InventoryRules.AvailableQuantity(InventoryIndex.For(Society.Inventory), lot));
        public InventoryLot? AttachedHandcart(string actor) => null;
        public BoatState? PassengerBoat(string actor) => null;
        public GoodsAnswer FindGoods(GoodsRequest request) => GoodsInventoryQuery.FindConsumableGoods(Society.Inventory, request, (_, _) => null);
        public long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds) => GoodsInventoryQuery.FindGoods(Society.Inventory,
            new(GoodsUse.Holdings, null, new(owners.ToArray()), kinds), (_, _) => null,
            (index, lot) => new(lot, index.Root(lot), new(GoodsPlaceKind.Carried), AvailableLotQuantity(lot), AvailableLotQuantity(lot))).Total;
        public GoodsKinds EdibleFoodGoods => new(FoodRules.EdibleKinds);
        public GridPoint[] FreshWaterShorePositions() => [];
        public List<GridPoint> SharedUnoccupiedRoute(GridPoint origin, HashSet<GridPoint> occupied, GridPoint destination, int interactionRange, bool swimming = false) =>
            Map.FootDistance(origin, destination) <= interactionRange ? [origin] : throw new NotSupportedException();
        public void ReplaceEcologyResource(string id, EcologyResource resource)
        {
            ReplacedResources.Add(id);
            Environment = Environment with { Ecology = new(Environment.Ecology.Resources.Select(item => item.Id == id ? resource : item).ToArray()) };
        }
        public void SyncEcologyResourceStates() => EcologySyncs++;
        public AnimalState OwnedAnimal(string species) => new("cow", "Moss", species, "female",
            -(long)AnimalRules.Definition(species).AdultDays * Environment.Config.TicksPerDay, new(1, 1), "home", "household", "yard");
        public void AddCareGoods(int feed, int water)
        {
            ApplyInventoryTransition(inventory => InventoryAuthority.AddLot(inventory, "feed", "grain", "agent", feed));
            for (var index = 0; index < water; index++)
            {
                var jug = "jug-" + index;
                ApplyInventoryTransition(inventory => InventoryAuthority.AddLot(inventory, jug, "water_jug", "agent", 1));
                ApplyInventoryTransition(inventory => InventoryAuthority.AddLot(inventory, "water-" + index, "fresh_water", "agent", 1, containerLotId: jug));
            }
        }

        public bool CanRemoveWorkstationStock(InventoryCheckpoint inventory, InventoryLot root, int quantity) => throw new NotSupportedException();
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, Identity effectId, int units) => throw new NotSupportedException();
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => throw new NotSupportedException();
        public List<GridPoint> FindUnoccupiedRoute(string agentId, GridPoint origin, GridPoint destination, int interactionRange, bool allowSwimming = true, int additionalCarriedUnits = 0) => throw new NotSupportedException();
        public int FreeCarryCapacity(string actor) => throw new NotSupportedException();
        public PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => throw new NotSupportedException();
        public GridPoint HouseholdStockPosition(InventoryLot lot) => throw new NotSupportedException();
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => throw new NotSupportedException();
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => throw new NotSupportedException();
        public OwnerQueuedInstruction? PendingInstructionFor(string agentId) => throw new NotSupportedException();
        public int PhysicalUnreservedQuantity(InventoryLot lot) => throw new NotSupportedException();
        public int PickupCarryCapacity(string actor, ClankerWorld.Simulation.Kernel.InventoryLot stock, GridPoint destination, int destinationRange = 0) => throw new NotSupportedException();
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lotId) => FindGoods(request).Matches.FirstOrDefault(match => match.Lot.Id == lotId);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, OrderStatus status, string? reason, bool waitForDecision = false) => throw new NotSupportedException();
        public void StageBuiltInContent(string packageId, string requiredActivePackageId, Func<ContentPackageManifest> create, string eventKind) => throw new NotSupportedException();
        public bool VesselFits(InventoryLot lot, int room) => throw new NotSupportedException();
        public IReadOnlyList<ContentPackageRecord> Packages => throw new NotSupportedException();
        public void SetAgent(string id, AgentState state) => throw new NotSupportedException();
    }
}
