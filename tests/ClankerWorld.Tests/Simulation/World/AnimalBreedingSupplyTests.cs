using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class AnimalBreedingSupplyTests
{
    private const string Pot = "a-breeding-pot";
    private const string Jug = "a-breeding-jug";
    private const string Feed = "a-breeding-feed";
    private const string Water = "a-breeding-water";
    private static readonly Lazy<Task<byte[]>> Prepared = new(CreatePaidPairAsync);

    [Theory]
    [InlineData("healthy", true)]
    [InlineData("broken-jug", false)]
    [InlineData("broken-pot", false)]
    [InlineData("missing-water", false)]
    [InlineData("missing-feed", false)]
    [InlineData("worn-vessels", true)]
    [InlineData("held-feed", false)]
    [InlineData("held-water", false)]
    [InlineData("loose-feed", true)]
    [InlineData("healthy-later-jug", true)]
    [InlineData("healthy-later-pot", true)]
    public async Task NativeBreedingCountsOnlyUsableDeliveredSupplies(string boundary, bool expected)
    {
        var state = WorldRuntimeCodec.Decode(await Prepared.Value);
        var mother = state.AnimalWorld!.Animals.Single(item => item.Id == "breeding-mother");
        var home = mother.HouseholdId!;
        var yard = mother.YardId!;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, Pot, "storage_pot", home, 1, storageBuildingId: yard);
        inventory = InventoryAuthority.AddLot(inventory, Jug, "water_jug", home, 1, storageBuildingId: yard);
        if (boundary != "missing-feed") inventory = InventoryAuthority.AddLot(inventory, Feed, "grain", home, 2,
            storageBuildingId: yard, containerLotId: boundary == "loose-feed" ? null : Pot);
        if (boundary != "missing-water") inventory = InventoryAuthority.AddLot(inventory, Water, "fresh_water", home, 2,
            storageBuildingId: yard, containerLotId: Jug);
        if (boundary is "broken-jug" or "broken-pot" or "healthy-later-jug" or "healthy-later-pot" or "worn-vessels")
        {
            var damaged = boundary.EndsWith("jug", StringComparison.Ordinal) ? Jug : Pot;
            inventory = inventory with
            {
                Lots = inventory.Lots.Select(lot => lot.Id == damaged ||
                boundary == "worn-vessels" && lot.Id == Jug ? lot with
                { ConditionBasisPoints = boundary == "worn-vessels" ? 1 : 0 } : lot).ToArray()
            };
        }
        if (boundary is "healthy-later-jug" or "healthy-later-pot")
        {
            var water = boundary == "healthy-later-jug";
            var rootId = water ? "z-breeding-jug" : "z-breeding-pot";
            inventory = InventoryAuthority.AddLot(inventory, rootId, water ? "water_jug" : "storage_pot", home, 1, storageBuildingId: yard);
            inventory = InventoryAuthority.AddLot(inventory, water ? "z-breeding-water" : "z-breeding-feed", water ? "fresh_water" : "grain",
                home, 2, storageBuildingId: yard, containerLotId: rootId);
        }
        if (boundary is "held-feed" or "held-water") inventory = InventoryAuthority.Reserve(inventory, "held-breeding-stock", home,
            boundary == "held-feed" ? Feed : Water, 2, "other-work", inventory.WorldTick + 1_000);
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        using var world = WorldRuntime.Restore(Copy(state), _ => new Idle());
        using var replay = WorldRuntime.Restore(Copy(state), _ => new Idle());
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var female = world.Animals.Single(item => item.Id == mother.Id);
        Assert.Equal(expected, female.Pregnancy is not null);
        var started = world.ExportState().Events.Where(item => item.Kind == "animal_breeding_started").ToArray();
        if (expected)
        {
            Assert.Equal("breeding-father", female.Pregnancy!.FatherId);
            Assert.Equal(world.WorldTick, female.Pregnancy.StartedTick);
            Assert.Equal(mother.Id + ":breeding-father", Assert.Single(started).Detail);
        }
        else Assert.Empty(started);
        Assert.Equal(2, world.Animals.Count);
        Assert.Null(world.Animals.Single(item => item.Id == "breeding-father").Pregnancy);
        Assert.Equal(inventory.Reservations, world.Society.Inventory.Reservations);
        foreach (var lot in inventory.Lots.Where(item => item.Id.StartsWith("a-breeding-", StringComparison.Ordinal) ||
                     item.Id.StartsWith("z-breeding-", StringComparison.Ordinal)))
        {
            var saved = world.Society.Inventory.GetLot(lot.Id);
            Assert.Equal((lot.OwnerId, lot.Quantity, lot.ConditionBasisPoints, lot.ContainerLotId, lot.StorageBuildingId),
                (saved.OwnerId, saved.Quantity, saved.ConditionBasisPoints, saved.ContainerLotId, saved.StorageBuildingId));
        }
        world.Validate();
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Idle());
        restored.Validate();
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(restored.ExportState()));
        // The admitted or refused prerequisite must continue identically after a genuine strict reload.
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    private static async Task<byte[]> CreatePaidPairAsync()
    {
        using var setup = NormalPathWorld.CreateGenerated("animal-breeding-vessel-audit", _ => new Idle());
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var home = state.Society.Society.GetAgent(state.Agents[0].AgentId).HouseholdId!;
        var definition = state.WorldContent!.Buildings.Single(item => item.Tags.Contains(AnimalContent.YardTag));
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definition.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "breeding-yard-cost-" + cost.ResourceId, cost.ResourceId, home, cost.Amount);
        using var builder = WorldRuntime.Restore(state with
        { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } });
        foreach (var tile in state.Map.Tiles.OrderBy(item => state.Map.FootDistance(item.Position, state.Agents[0].Position)))
            if (builder.PlaceBuilding("breeding-yard", definition.CanonicalId, tile.Position, home).Applied) break;
        var yard = Assert.Single(builder.WorldSimulation.Buildings, item => item.InstanceId == "breeding-yard");
        foreach (var cost in definition.BuildCosts) Assert.Equal(
            inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity) - cost.Amount,
            builder.Society.Inventory.Lots.Where(lot => lot.OwnerId == home && lot.ItemKind == cost.ResourceId).Sum(lot => lot.Quantity));
        state = builder.ExportState();
        var day = state.WorldSystems!.Config.TicksPerDay;
        // Explicit cared adult stock fixtures; the tick creates the pregnancy and event itself.
        var female = new AnimalState("breeding-mother", "Moss", "cow", "female", -7L * day, yard.Position,
            "household:" + home, home, yard.InstanceId, CareUntilTick: state.Society.Society.WorldTick + day);
        var male = female with { Id = "breeding-father", Name = "Ash", Sex = "male", Position = new(yard.Position.X + 1, yard.Position.Y) };
        state = state with
        {
            AnimalWorld = new(true, [male, female], []),
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = state.Agents.Select(person => person with
            { FullnessBasisPoints = 9_500, Survival = new(), Project = null, LastDecisionContext = null }).ToArray(),
        };
        using var checkedWorld = WorldRuntime.Restore(state, _ => new Idle());
        checkedWorld.Validate();
        return WorldRuntimeCodec.Encode(checkedWorld.ExportState());
    }

    private static WorldRuntimeState Copy(WorldRuntimeState state) => WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state));
    private sealed class Idle : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            var selected = observation.Candidates.Single(item => item.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, selected.Id, 1,
                observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
