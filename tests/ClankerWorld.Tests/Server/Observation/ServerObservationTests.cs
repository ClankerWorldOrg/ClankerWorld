using System.Text.Json;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class ServerObservationTests
{
    [Theory]
    [InlineData("person")]
    [InlineData("household")]
    [InlineData("town")]
    public void GroundStockOwnerNamesReachClientWithoutChangingOwnership(string ownerKind)
    {
        using var generated = NormalPathWorld.CreateGenerated("ground-owner-names",
            _ => new ActionCoverageRecorder(chooseIdle: true));
        var state = generated.ExportState();
        var person = state.Society.Society.Agents[0];
        var household = state.Society.Society.Households[0];
        var town = state.Towns![0];
        var (ownerId, ownerName) = ownerKind switch
        {
            "person" => (person.Id, person.Name),
            "household" => (household.Id, household.Name),
            _ => (town.Id, town.Name),
        };
        var inventory = state.Society.Society.Inventory;
        var wood = inventory.Lots.First(lot => lot.ItemKind == "wood" && lot.Quantity >= 2 && lot.ContainerLotId is null);
        var point = state.Agents[0].Position;
        inventory = wood.OwnerId == ownerId
            ? InventoryAuthority.Relocate(inventory, "ground-label", wood.Id, ownerId, 2, groundPosition: new(point.X, point.Y))
            : InventoryAuthority.Transfer(inventory, "ground-label", wood.OwnerId, ownerId, wood.Id, 2,
                "ground ownership check", destinationGroundPosition: new(point.X, point.Y));
        using var world = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
        }, _ => new ActionCoverageRecorder(chooseIdle: true));
        var store = new OwnerWorldObservationStore(world);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ClankerWorld.Contracts.OwnerWorldGroundStock Read() =>
            JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(
                JsonSerializer.Serialize(store.GetSnapshot(), options), options)!.GroundStocks.Single(stock =>
                    stock.OwnerId == ownerId && stock.Kind == "wood" && stock.Position.X == point.X && stock.Position.Y == point.Y);
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        var stock = Read();
        Assert.Equal((ownerId, ownerName, "wood", 2), (stock.OwnerId, stock.OwnerName, stock.Kind, stock.Quantity));
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        if (ownerKind == "person")
        {
            Assert.True(world.RenameAgent(ownerId, "Élodie Lake"));
            stock = Read();
            Assert.Equal((ownerId, "Élodie Lake", "wood", 2), (stock.OwnerId, stock.OwnerName, stock.Kind, stock.Quantity));
        }
    }

    [Fact]
    public void GeneratedReconnectOmitsOnlyAlreadyHeldTerrain()
    {
        var geography = new GeographyOptions("cached-terrain", WorldSizePreset.Small);
        using var runtime = new WorldRuntime(geography.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: geography);
        var store = new OwnerWorldObservationStore(runtime);
        var initial = store.GetReconnectBaseline(0).Snapshot;
        Assert.NotNull(initial.PackedTerrain);
        Assert.NotNull(initial.PackedMapLayers);
        Assert.Equal(MapLayerManifestCodec.Digest(runtime.ExportState().Map), initial.MapLayersDigest);
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var client = System.Text.Json.JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(
            System.Text.Json.JsonSerializer.Serialize(initial, options), options)!;
        Assert.Equal(initial.PackedMapLayers!.Width, client.PackedMapLayers!.Width);
        Assert.Equal(initial.PackedMapLayers.Height, client.PackedMapLayers.Height);
        Assert.Equal(initial.PackedMapLayers.Encoding, client.PackedMapLayers.Encoding);
        Assert.Equal(initial.PackedMapLayers.Climate, client.PackedMapLayers.Climate);
        Assert.Equal(initial.PackedMapLayers.Elevation, client.PackedMapLayers.Elevation);
        Assert.Equal(initial.PackedMapLayers.Hydrology, client.PackedMapLayers.Hydrology);
        Assert.Equal(initial.PackedMapLayers.Surface, client.PackedMapLayers.Surface);
        Assert.Equal(initial.PackedMapLayers.Vegetation, client.PackedMapLayers.Vegetation);
        Assert.Equal(initial.MapLayersDigest, client.MapLayersDigest);
        Assert.Contains("owner-terrain-delta.v1", store.GetOwnerHandshake().ServerCapabilities);
        Assert.Contains("owner-map-layer-delta.v1", store.GetOwnerHandshake().ServerCapabilities);

        var unchanged = store.GetReconnectBaseline(0, initial.WorldId, initial.MapManifestDigest,
            initial.MapLayersDigest).Snapshot;
        Assert.Null(unchanged.PackedTerrain);
        Assert.Null(unchanged.PackedMapLayers);
        Assert.Empty(unchanged.Tiles);
        Assert.Equal(initial.MapManifestDigest, unchanged.MapManifestDigest);
        Assert.Equal(initial.MapLayersDigest, unchanged.MapLayersDigest);
        var layersUnchangedButTerrainClaimedWithoutTheLayerDigest = store.GetReconnectBaseline(0,
            initial.WorldId, initial.MapManifestDigest, new string('0', 64)).Snapshot;
        Assert.Null(layersUnchangedButTerrainClaimedWithoutTheLayerDigest.PackedTerrain);
        Assert.NotNull(layersUnchangedButTerrainClaimedWithoutTheLayerDigest.PackedMapLayers);
        Assert.NotNull(store.GetReconnectBaseline(0, "other-world", initial.MapManifestDigest).Snapshot.PackedTerrain);
        Assert.NotNull(store.GetReconnectBaseline(0, initial.WorldId, "wrong-digest").Snapshot.PackedTerrain);
    }

    [Fact]
    public async Task HostReportsNightDarknessFromTheWorldClockToTheClient()
    {
        using var runtime = new WorldRuntime("night-projection");
        var store = new OwnerWorldObservationStore(runtime);
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        int? ClientDarkness() => System.Text.Json.JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(
            System.Text.Json.JsonSerializer.Serialize(store.GetSnapshot(), options), options)!.DarknessBasisPoints;

        // A world's clock starts at midnight.
        Assert.Equal(DaylightRules.FullDarkness, store.GetSnapshot().DarknessBasisPoints);
        Assert.Equal(DaylightRules.FullDarkness, ClientDarkness());
        while (DaylightRules.DarknessBasisPoints(runtime.WorldSystems) == DaylightRules.FullDarkness)
            Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
        var dawn = DaylightRules.DarknessBasisPoints(runtime.WorldSystems);
        Assert.InRange(dawn, 1, DaylightRules.FullDarkness - 1);
        Assert.Equal(dawn, store.GetSnapshot().DarknessBasisPoints);
        Assert.Equal(dawn, ClientDarkness());
    }

    [Fact]
    public void ResourceProjectionCarriesAuthoritativeStockAndRegrowthIntoTheClient()
    {
        using var runtime = new WorldRuntime("ecology-projection");
        var snapshot = new OwnerWorldObservationStore(runtime).GetSnapshot();
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var client = System.Text.Json.JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(
            System.Text.Json.JsonSerializer.Serialize(snapshot, options), options)!;
        foreach (var resource in runtime.WorldSystems.Ecology.Resources)
        {
            var projected = snapshot.Resources.Single(item => item.Id == resource.Id);
            Assert.Equal(resource.Quantity, projected.Quantity);
            Assert.Equal(resource.Capacity, projected.Capacity);
            Assert.Equal(resource.RegenerationAmount, projected.RegenerationAmount);
            Assert.Equal(resource.RegenerationIntervalDays, projected.RegenerationIntervalDays);
            Assert.Equal(resource.RegenerationSeason.ToString().ToLowerInvariant(), projected.RegenerationSeason);
            var displayed = client.Resources.Single(item => item.Id == resource.Id);
            Assert.Equal(projected.Quantity, displayed.Quantity);
            Assert.Equal(projected.Capacity, displayed.Capacity);
            Assert.Equal(projected.RegenerationSeason, displayed.RegenerationSeason);
        }
    }

    [Fact]
    public async Task FieldWorkIsVisibleAlongsideWorkstationJobsAndSurvivesReconnect()
    {
        var (state, farmer, household, point) = FarmFieldTests.PreparedFarmer("field-observation");
        var farmhouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-farmhouse");
        var miller = state.Society.Society.Agents.First(person => person.HouseholdId == household && person.Id != farmer).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "observation-grain", "grain", household, 1,
            storageBuildingId: farmhouse.InstanceId);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == miller
                ? person with { Position = farmhouse.Position, FullnessBasisPoints = 10_000 } : person).ToArray(),
        };
        using var runtime = FarmFieldTests.Restore(state);
        var mill = runtime.WorldContent.Recipes.Single(recipe => recipe.LocalId == "mill-grain");
        Assert.True(runtime.StartProduction(mill.CanonicalId, farmhouse.InstanceId, miller).Applied);
        Assert.True(runtime.StartFieldWork(farmer, point, FarmWorkKind.Till).Accepted);
        for (var tick = 0; tick < 3; tick++) Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
        var beforeInspection = WorldRuntimeCodec.Encode(runtime.ExportState());
        var snapshot = new OwnerWorldObservationStore(runtime).GetSnapshot();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var client = JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldSnapshot>(
            JsonSerializer.Serialize(snapshot, options), options)!;
        var displayedJob = Assert.Single(client.ProductionJobs);
        Assert.Equal("Mill grain into flour", displayedJob.Recipe!.Name);
        Assert.Equal(mill.CanonicalId, displayedJob.Recipe.Id);
        Assert.Equal(("grain", 1), (Assert.Single(displayedJob.Recipe.Inputs).Kind, Assert.Single(displayedJob.Recipe.Inputs).Quantity));
        Assert.Equal(("flour", 1), (Assert.Single(displayedJob.Recipe.Outputs).Kind, Assert.Single(displayedJob.Recipe.Outputs).Quantity));
        Assert.Equal(("grain", 1), (Assert.Single(displayedJob.HeldInputs!).Kind, Assert.Single(displayedJob.HeldInputs!).Quantity));
        var available = client.PlacedBuildings.Single(building => building.InstanceId == farmhouse.InstanceId).AvailableRecipes!;
        Assert.Equal(mill.CanonicalId, Assert.Single(available).Id);
        Assert.Empty(client.PlacedBuildings.Single(building => building.Tags!.Contains("warehouse")).AvailableRecipes!);
        var house = client.PlacedBuildings.First(building => building.Tags!.Contains("house"));
        var bread = Assert.Single(house.AvailableRecipes!, recipe => recipe.Name == "Bake bread");
        Assert.Equal(3, bread.Inputs.Count);
        Assert.Contains(bread.Inputs, input => input.Kind == "flour" && input.Quantity == 2);
        Assert.Contains(bread.Inputs, input => input.Kind == "fresh_water" && input.Quantity == 1);
        Assert.Contains(bread.Inputs, input => input.Kind == "wood" && input.Quantity == 1);
        Assert.Equal(("bread", 2), (Assert.Single(bread.Outputs).Kind, Assert.Single(bread.Outputs).Quantity));
        Assert.All(client.PlacedBuildings.SelectMany(building => building.AvailableRecipes ?? []),
            recipe => Assert.DoesNotContain(recipe.Outputs, output => output.Kind is "bedding" or "food"));
        Assert.Equal(beforeInspection, WorldRuntimeCodec.Encode(runtime.ExportState()));
        var field = Assert.Single(snapshot.Fields!);
        Assert.Equal(new PositionView(point.X, point.Y), field.Position);
        Assert.Equal(household, field.HouseholdId);
        Assert.Equal("preparing", field.Stage);
        Assert.Equal(farmer, field.WorkerId);
        // Eight units of tilling, with two units per wooden-hoe work action.
        Assert.Equal(2, field.WorkRemaining);
        Assert.Equal(miller, Assert.Single(snapshot.ProductionJobs).WorkerId);
        Assert.Equal(snapshot.ProductionJobs.Count, snapshot.WorldSystems!.ProductionJobCount);
        using var restored = FarmFieldTests.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(runtime.ExportState())));
        var reconnect = new OwnerWorldObservationStore(restored).GetReconnectBaseline(0).Snapshot;
        Assert.Equal(snapshot.Fields, reconnect.Fields);
        Assert.Equal(JsonSerializer.Serialize(snapshot.ProductionJobs, options),
            JsonSerializer.Serialize(reconnect.ProductionJobs, options));
    }

    [Fact]
    public void EmptyPopulationRemainsObservableAfterRestore()
    {
        using var genesis = new WorldRuntime("test-alpha");
        var state = genesis.ExportState();
        var society = state.Society.Society;
        foreach (var agent in society.Agents)
        {
            society = ClankerWorld.Simulation.Society.SocietyRules.Kill(society, agent.Id,
                ClankerWorld.Simulation.Society.SocietyDeathCause.NaturalAge).Checkpoint;
        }
        state = state with { Society = state.Society with { Society = society }, Agents = [] };
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)));
        var baseline = new OwnerWorldObservationStore(restored).GetReconnectBaseline(0);
        Assert.Empty(baseline.Snapshot.Agents);
        Assert.Null(baseline.Snapshot.Actor);
        Assert.Equal(state.Society.Society.WorldTick, baseline.Snapshot.WorldTick);
    }

    [Fact]
    public void OwnerAgentKnowledgeIsBoundedToLocalPerceptionAndItsCommittedRoute()
    {
        using var runtime = new WorldRuntime("world-a");
        var store = new OwnerWorldObservationStore(runtime);

        var snapshot = store.GetSnapshot();
        var agent = snapshot.Agents[0];
        var knowledge = agent.SpatialKnowledge;

        Assert.Equal(agent.Position, knowledge.CurrentTile);
        Assert.All(knowledge.PerceivedTiles, tile => Assert.Contains(tile, knowledge.KnownTiles));
        Assert.All(agent.Route.Steps, step => Assert.Contains(step, knowledge.KnownTiles));
        if (agent.Route.Destination is not null)
        {
            Assert.Contains(agent.Route.Destination, knowledge.KnownTiles);
        }

        Assert.Equal(
            knowledge.KnownTiles
                .Distinct()
                .OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X),
            knowledge.KnownTiles);
        Assert.True(
            knowledge.KnownTiles.Count < snapshot.Tiles.Count,
            "The native projection must not expose the complete server map as agent knowledge.");
    }

    [Fact]
    public void WorldObservationProjectsTheActiveTownAndNeeds()
    {
        using var runtime = new WorldRuntime("test-alpha");
        var store = new OwnerWorldObservationStore(runtime);

        var snapshot = store.GetSnapshot();

        Assert.Equal("test-alpha", snapshot.WorldId);
        Assert.Equal(4, snapshot.Agents.Count);
        Assert.All(snapshot.Agents, agent =>
        {
            Assert.False(agent.IsDraft);
            Assert.Equal("active", agent.Lifecycle);
            Assert.Contains(agent.DecisionFactors, factor => factor.Key == "personality");
            Assert.Contains(agent.DecisionFactors, factor => factor.Key == "household");
            Assert.NotEmpty(agent.SpatialKnowledge.PerceivedTiles);
            Assert.Contains(
                agent.Relationships,
                relationship => relationship.Type == "household_membership" &&
                    relationship.State == "accepted" &&
                    relationship.OtherPartyId == "household:first");
        });
        Assert.NotNull(snapshot.Cognition);
        Assert.NotNull(snapshot.WorldSystems);
        Assert.Equal("spring", snapshot.WorldSystems!.Season);
        Assert.Equal(4, snapshot.WorldSystems.EcologyResourceCount);
        Assert.Equal(4, snapshot.Agents.Select(agent => agent.Id).Distinct().Count());
    }

    [Fact]
    public async Task WorldObservationExposesPublicIntentionsWithoutModelTrace()
    {
        using var runtime = new WorldRuntime("test-alpha", _ => new ActionCoverageRecorder(chooseIdle: true));
        _ = await runtime.AdvanceOneTickAsync();

        var snapshot = new OwnerWorldObservationStore(runtime).GetSnapshot();

        Assert.Contains(snapshot.Agents, agent =>
            agent.PublicIntention is { WorldTick: 1, Summary: not null });
        Assert.Equal(4, snapshot.Cognition!.Decisions!.Count);
        Assert.All(snapshot.Cognition.Decisions, decision =>
        {
            Assert.Equal(1, decision.WorldTick);
            Assert.Equal("deterministic", decision.Provider);
            Assert.Contains(snapshot.Agents, agent => agent.Id == decision.AgentId);
        });
        Assert.All(snapshot.Agents.Where(agent => agent.PublicIntention is not null), agent =>
        {
            Assert.DoesNotContain("chain", agent.PublicIntention!.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("prompt", agent.PublicIntention.Summary, StringComparison.OrdinalIgnoreCase);
        });
    }
}
