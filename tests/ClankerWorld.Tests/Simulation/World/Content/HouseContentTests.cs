using System.Text.Json;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class HouseContentTests
{
    [Fact]
    public async Task AdultCarriesPersonalSpareFoodIntoOwnHouseWithoutLosingLastServing()
    {
        using var seed = new WorldRuntime("house-personal-food", _ => new IdleProvider(),
            startPace: WorldStartPace.FounderSetup);
        await PrepareWorldAsync(seed, 8);

        var state = seed.ExportState();
        var actor = state.Society.Society.Agents.First(person =>
            person.HouseholdId == "household:first").Id;
        var startingSite = state.Map.GetObject("storage").Position;
        var site = FindHouseSite(state, actor, startingSite);
        var house = seed.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        var placed = seed.PlaceBuilding("food-home-alpha", house.CanonicalId, site, "household:first");
        Assert.True(placed.Applied, placed.Failure);
        var foodBefore = HouseholdQuantity(seed, "household:first", "food");
        state = seed.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = startingSite, FullnessBasisPoints = 9_000 } : person).ToArray(),
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
                        "food:personal-surplus", "food", actor, 4),
                },
            },
        };
        using var world = WorldRuntime.Restore(state,
            id => id == actor ? new PreferredCandidateProvider("store_household_food") : new IdleProvider());
        for (var tick = 0; tick < 20 && !world.ExportState().Events.Any(item =>
                 item.Kind == "household_food_stored" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal)); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var delivery = world.ExportState().Events.Single(item => item.Kind == "household_food_stored" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        Assert.Equal(site, delivery.Position);
        Assert.Equal(1, world.Society.Inventory.GetLot("food:personal-surplus").Quantity);
        Assert.Equal(foodBefore + 3, HouseholdQuantity(world, "household:first", "food"));
        var stored = new OwnerWorldObservationStore(world).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "food-home-alpha").StoredItems!;
        Assert.Contains(stored, item => item.Kind == "food" && item.Quantity == 3);

        using var restored = WorldRuntime.Restore(
            WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Contains(new OwnerWorldObservationStore(restored).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "food-home-alpha").StoredItems!,
            item => item.Kind == "food" && item.Quantity == 3);
    }

    [Fact]
    public async Task HouseMealUsesOnlyItsHouseholdsIngredientsAndKeepsTheOutputOnReload()
    {
        using var seed = new WorldRuntime("house-meal", _ => new IdleProvider(),
            startPace: WorldStartPace.FounderSetup);
        await PrepareWorldAsync(seed, 8);

        var state = seed.ExportState();
        var alpha = state.Society.Society.Agents.First(person => person.HouseholdId == "household:first").Id;
        var beta = state.Society.Society.Agents.First(person => person.HouseholdId == "household:second").Id;
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, state.Map.GetObject("storage").Position) >= 3 &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.AgentId != beta && person.Position == point));
        var house = seed.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        var recipe = seed.WorldContent.Recipes.Single(item => item.LocalId == "house-meal");
        Assert.True(seed.PlaceBuilding("meal-home-beta", house.CanonicalId, site, "household:second").Applied);
        state = seed.ExportState() with
        {
            Agents = seed.ExportState().Agents.Select(person => person.AgentId == beta
                ? person with { Position = site, FullnessBasisPoints = 9_000 } : person).ToArray(),
        };
        using (var unstocked = WorldRuntime.Restore(state, _ => new IdleProvider()))
        {
            var remote = unstocked.StartProduction(recipe.CanonicalId, "meal-home-beta", beta);
            Assert.False(remote.Applied);
            Assert.Contains("on-site", remote.Failure, StringComparison.Ordinal);
        }
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = state.Society.Society.Inventory with
                    {
                        Lots = state.Society.Society.Inventory.Lots.Select(lot =>
                            lot.OwnerId == "household:second" && (lot.ItemKind is "potatoes" or "wood")
                                ? lot with { StorageBuildingId = "meal-home-beta" } : lot).ToArray(),
                    },
                },
            },
        };
        state = state with
        {
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory,
                        "beta-iron-knife", "iron_knife", beta, 1),
                },
            },
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        var alphaFood = HouseholdQuantity(world, "household:first", "potatoes");
        var betaFood = HouseholdQuantity(world, "household:second", "potatoes");
        var betaWood = HouseholdQuantity(world, "household:second", "wood");
        var rejected = world.StartProduction(recipe.CanonicalId, "meal-home-beta", alpha);
        Assert.False(rejected.Applied);
        Assert.Contains("Only a member", rejected.Failure, StringComparison.Ordinal);
        var started = world.StartProduction(recipe.CanonicalId, "meal-home-beta", beta);
        Assert.True(started.Applied, started.Failure);
        var activeJob = Assert.Single(world.WorldSimulation.ProductionJobs, job => job.JobId == started.JobId);
        Assert.Equal("beta-iron-knife", activeJob.ToolLotId);
        Assert.Equal(recipe.DurationTicks / 2, activeJob.CompletionTick - activeJob.StartedTick);
        Assert.All(activeJob.InputReservationIds, reservationId =>
                Assert.Equal("household:second", world.Society.Inventory.GetReservation(reservationId).OwnerId));

        var activeWithKnife = world.ExportState();
        Assert.Equal(WorldRuntime.StateSchemaVersion, activeWithKnife.SchemaVersion);
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(
            activeWithKnife with { SchemaVersion = WorldRuntime.ToolProgressionSchemaVersion - 1 }, _ => new IdleProvider()));
        var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(activeWithKnife));
        using var resumed = WorldRuntime.Restore(saved, _ => new IdleProvider());
        Assert.Equal(10_000, resumed.Society.Inventory.GetLot("beta-iron-knife").ConditionBasisPoints);
        for (var tick = 0; tick < recipe.DurationTicks / 2 - 1; tick++)
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldProductionJobState.Running,
            resumed.WorldSimulation.ProductionJobs.Single(job => job.JobId == started.JobId).State);
        Assert.Equal(10_000, resumed.Society.Inventory.GetLot("beta-iron-knife").ConditionBasisPoints);
        Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(alphaFood, HouseholdQuantity(resumed, "household:first", "potatoes"));
        Assert.Equal(betaFood - 2, HouseholdQuantity(resumed, "household:second", "potatoes"));
        Assert.Equal(betaWood - 1, HouseholdQuantity(resumed, "household:second", "wood"));
        Assert.Equal(2, HouseholdQuantity(resumed, "household:second", "simple_meal"));
        Assert.Equal(9_000, resumed.Society.Inventory.GetLot("beta-iron-knife").ConditionBasisPoints);
        Assert.Equal(WorldProductionJobState.Completed,
            resumed.WorldSimulation.ProductionJobs.Single(job => job.JobId == started.JobId).State);
        var cookedLotId = $"{started.JobId}:output:00";
        Assert.Equal("meal-home-beta", resumed.Society.Inventory.GetLot(cookedLotId).StorageBuildingId);
        var projected = new OwnerWorldObservationStore(resumed).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "meal-home-beta");
        Assert.Contains(projected.StoredItems!, item => item.Kind == "simple_meal" && item.Quantity == 2);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var client = JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldPlacedBuilding>(
            JsonSerializer.Serialize(projected, options), options)!;
        Assert.Contains(client.StoredItems!, item => item.Kind == "simple_meal" && item.Quantity == 2);

        var finished = resumed.ExportState();
        var invalid = finished with
        {
            Society = finished.Society with
            {
                Society = finished.Society.Society with
                {
                    Inventory = finished.Society.Society.Inventory with
                    {
                        Lots = finished.Society.Society.Inventory.Lots.Select(lot => lot.Id == cookedLotId
                            ? lot with { StorageBuildingId = "missing-house" } : lot).ToArray(),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalid));

        var otherBeta = finished.Society.Society.Agents.First(person =>
            person.HouseholdId == "household:second" && person.Id != beta).Id;
        var houseApproach = finished.Map.FootNeighbors(site).First(point =>
            finished.Agents.All(person => person.AgentId == beta || person.Position != point));
        var pickupInventory = finished.Society.Society.Inventory;
        foreach (var ration in pickupInventory.Lots.Where(lot => lot.OwnerId == "household:second" && lot.ItemKind == "food").ToArray())
        {
            var reservationId = "pickup-fixture:" + ration.Id;
            pickupInventory = InventoryAuthority.ConsumeReservation(InventoryAuthority.Reserve(pickupInventory,
                reservationId, ration.OwnerId, ration.Id, ration.Quantity, "fixture", finished.Society.Society.WorldTick), reservationId);
        }
        var pickupState = finished with
        {
            Society = finished.Society with { Society = finished.Society.Society with { Inventory = StockHoldTestState.Custody(pickupInventory) } },
            Agents = finished.Agents.Select(person => person.AgentId == beta
                ? person with { Position = houseApproach, FullnessBasisPoints = 2_000 }
                : person.AgentId == otherBeta ? person with { Position = site }
                : person).ToArray(),
        };
        using var pickup = WorldRuntime.Restore(pickupState,
            _ => new PreferredCandidateProvider("collect_shared_food"));
        for (var tick = 0; tick < 40 && !pickup.ExportState().Events.Any(item =>
                 item.Kind == "household_food_collected" && item.Detail.StartsWith(beta + ":" + cookedLotId + ":", StringComparison.Ordinal)); tick++)
            Assert.True((await pickup.AdvanceOneTickAsync()).Advanced);
        var pickupEvents = pickup.ExportState().Events;
        var collection = pickupEvents.FirstOrDefault(item =>
            item.Kind == "household_food_collected" && item.Detail.StartsWith(beta + ":" + cookedLotId + ":", StringComparison.Ordinal));
        Assert.NotNull(collection);
        Assert.NotNull(collection.Position);
        Assert.Equal(site, collection.Position!.Value);
        Assert.Equal(site, pickup.Agents.Single(person => person.AgentId == otherBeta).Position);
        Assert.Contains(pickup.Society.Inventory.Lots, lot => lot.OwnerId == beta &&
            (lot.Id == cookedLotId || lot.ProvenanceLotId == cookedLotId) && lot.StorageBuildingId is null);
    }

    [Fact]
    public async Task HouseholdStockIsCarriedFromSharedStorageBeforeItAppearsAtTheHouse()
    {
        using var seed = new WorldRuntime("house-haul", _ => new IdleProvider(),
            startPace: WorldStartPace.FounderSetup);
        await PrepareWorldAsync(seed, 8);

        var initial = seed.ExportState();
        var actor = initial.Society.Society.Agents.First(person =>
            person.HouseholdId == "household:first").Id;
        var startingSite = initial.Map.GetObject("storage").Position;
        var site = FindHouseSite(initial, actor, startingSite);
        var house = seed.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        Assert.True(seed.PlaceBuilding("haul-home-alpha", house.CanonicalId, site, "household:first").Applied);
        var staged = seed.ExportState();
        var foodBefore = staged.Society.Society.Inventory.Lots
            .Where(lot => lot.OwnerId == "household:first" && lot.ItemKind == "food")
            .Sum(lot => lot.Quantity);
        staged = staged with
        {
            Agents = staged.Agents.Select(person => person.AgentId == actor
                ? person with { Position = startingSite, FullnessBasisPoints = 9_000 } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(staged,
            id => id == actor ? new PreferredCandidateProvider("haul_household_stock") : new IdleProvider());
        for (var tick = 0; tick < 20 && !world.ExportState().Events.Any(item =>
                 item.Kind == "household_stock_picked_up" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal)); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "household_stock_picked_up" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        var carried = world.Society.Inventory.Lots.Single(lot =>
            lot.OwnerId == actor && lot.DeliveryBuildingId == "haul-home-alpha");
        Assert.Null(carried.StorageBuildingId);
        Assert.Empty(new OwnerWorldObservationStore(world).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "haul-home-alpha").StoredItems!);

        var midJourney = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        var invalid = midJourney with
        {
            Society = midJourney.Society with
            {
                Society = midJourney.Society.Society with
                {
                    Inventory = midJourney.Society.Society.Inventory with
                    {
                        Lots = midJourney.Society.Society.Inventory.Lots.Select(lot => lot.Id == carried.Id
                            ? lot with { DeliveryBuildingId = "missing-house" } : lot).ToArray(),
                    },
                },
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(invalid));

        using var resumed = WorldRuntime.Restore(midJourney,
            id => id == actor ? new PreferredCandidateProvider("haul_household_stock") : new IdleProvider());
        for (var tick = 0; tick < 20 && !resumed.ExportState().Events.Any(item =>
                 item.Kind == "household_stock_delivered" && item.Detail.StartsWith(actor + ":", StringComparison.Ordinal)); tick++)
            Assert.True((await resumed.AdvanceOneTickAsync()).Advanced);
        var delivery = resumed.ExportState().Events.First(item => item.Kind == "household_stock_delivered" &&
            item.Detail.StartsWith(actor + ":", StringComparison.Ordinal));
        Assert.Equal(site, delivery.Position);
        Assert.Equal("haul-home-alpha", resumed.Society.Inventory.GetLot(carried.Id).StorageBuildingId);
        Assert.Null(resumed.Society.Inventory.GetLot(carried.Id).DeliveryBuildingId);
        Assert.Equal(foodBefore, resumed.Society.Inventory.Lots
            .Where(lot => lot.OwnerId == "household:first" && lot.ItemKind == "food")
            .Sum(lot => lot.Quantity));
        Assert.Contains(new OwnerWorldObservationStore(resumed).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "haul-home-alpha").StoredItems!,
            item => item.Kind == "food" && item.Quantity == carried.Quantity);
    }

    [Fact]
    public async Task AnotherHouseholdDoesNotReceiveHouseRefugeInSnow()
    {
        using var seed = new WorldRuntime("house-refuge", _ => new IdleProvider(),
            startPace: WorldStartPace.FounderSetup);
        await PrepareWorldAsync(seed, 6);

        var state = seed.ExportState();
        var alpha = state.Society.Society.Agents.First(person => person.HouseholdId == "household:first").Id;
        var beta = state.Society.Society.Agents.First(person => person.HouseholdId == "household:second").Id;
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point =>
        {
            var neighbor = point with { X = point.X + 1 };
            return state.Map.IsBuildable(point) && state.Map.IsPassable(neighbor) &&
                !state.Map.MapObjects.Any(item => item.Position == point) &&
                !state.Map.Resources.Any(item => item.Position == point) &&
                !state.Agents.Any(person => person.AgentId != alpha && person.AgentId != beta &&
                    (person.Position == point || person.Position == neighbor));
        });
        var house = seed.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        Assert.True(seed.PlaceBuilding("refuge-alpha", house.CanonicalId, site, "household:first").Applied);
        state = seed.ExportState();
        var snowy = state.WorldSystems!;
        var profiles = Enum.GetValues<SeasonKind>().Select(season =>
            new WeatherProfile(season, 0, 0, 0, 0, 1)).ToArray();
        state = state with
        {
            WorldSystems = snowy with
            {
                RegionalWeather = null,
                Config = snowy.Config with { WeatherProfiles = profiles },
                Climate = snowy.Climate with { Weather = WeatherKind.Snow },
            },
            Agents = state.Agents.Select(person => person.AgentId == alpha
                ? person with { Position = site, FullnessBasisPoints = 9_000, Survival = new SurvivalCondition(5_000) }
                : person.AgentId == beta
                    ? person with
                    {
                        Position = site with { X = site.X + 1 },
                        FullnessBasisPoints = 9_000,
                        Survival = new SurvivalCondition(5_000)
                    }
                    : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new IdleProvider());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var protectedWarmth = world.Agents.Single(person => person.AgentId == alpha).Survival!.WarmthBasisPoints;
        var outsiderWarmth = world.Agents.Single(person => person.AgentId == beta).Survival!.WarmthBasisPoints;
        Assert.True(protectedWarmth > outsiderWarmth,
            $"The owner's refuge should protect only its household: {protectedWarmth} vs {outsiderWarmth}.");

        var beforeAlphaWood = HouseholdQuantity(world, "household:first", "wood");
        var beforeBetaWood = HouseholdQuantity(world, "household:second", "wood");
        var secondState = world.ExportState();
        var map = secondState.Map;
        var definitions = world.WorldContent.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var simulation = secondState.WorldSimulation!;
        var occupied = map.MapObjects.Select(item => item.Position)
            .Concat(map.Resources.Select(item => item.Position))
            .Concat(secondState.Agents.Select(item => item.Position))
            .Concat(secondState.RoadTiles ?? [])
            .Concat((secondState.Bridges ?? []).SelectMany(item => item.Entrances))
            .Concat((secondState.Fields ?? []).Select(item => item.Position))
            .Concat((simulation.BuildingExpansions ?? []).Where(job => job.State == WorldProductionJobState.Running)
                .SelectMany(job => Enumerable.Range(0, job.TargetFootprint.Height).SelectMany(dy =>
                    Enumerable.Range(0, job.TargetFootprint.Width).Select(dx =>
                        new GridPoint(job.TargetPosition.X + dx, job.TargetPosition.Y + dy)))))
            .Concat(simulation.Buildings.SelectMany(building =>
                WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)))
            .ToHashSet();
        var secondSite = map.Tiles.Select(tile => tile.Position).First(point =>
            map.IsBuildable(point) && !occupied.Contains(point));
        var betaPlacement = world.PlaceBuilding("refuge-beta", house.CanonicalId, secondSite,
            "household:second");
        Assert.True(betaPlacement.Applied, betaPlacement.Failure);
        Assert.Equal(beforeAlphaWood, HouseholdQuantity(world, "household:first", "wood"));
        Assert.Equal(beforeBetaWood - 8, HouseholdQuantity(world, "household:second", "wood"));
    }

    [Fact]
    public async Task HouseRequiresARealHouseholdAndKeepsItsOwnerAcrossSaveAndOwnerProjection()
    {
        using var world = new WorldRuntime("house-property");
        Assert.True(world.StageStarterContent());
        for (var tick = 0; tick < 6; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var house = world.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        var state = world.ExportState();
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.WorldSimulation!.Buildings.Any(item => item.Position == point));
        Assert.False(world.PlaceBuilding("home-alpha", house.CanonicalId, site).Applied);
        Assert.False(world.PlaceBuilding("home-alpha", house.CanonicalId, site, "household:unknown").Applied);
        var placement = world.PlaceBuilding("home-alpha", house.CanonicalId, site, "household:first");
        Assert.True(placement.Applied, placement.Failure);

        var saved = WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = WorldRuntime.Restore(saved);
        var projected = new OwnerWorldObservationStore(restored).GetSnapshot().PlacedBuildings
            .Single(building => building.InstanceId == "home-alpha");
        Assert.Equal("household:first", projected.HouseholdId);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var client = JsonSerializer.Deserialize<ClankerWorld.Contracts.OwnerWorldPlacedBuilding>(
            JsonSerializer.Serialize(projected, options), options)!;
        Assert.Equal(projected.HouseholdId, client.HouseholdId);

        var orphaned = saved with
        {
            WorldSimulation = saved.WorldSimulation! with
            {
                Buildings = saved.WorldSimulation.Buildings.Select(building =>
                    building.InstanceId == "home-alpha" ? building with { HouseholdId = "household:unknown" } : building).ToArray(),
            },
        };
        Assert.Throws<InvalidDataException>(() => WorldRuntime.Restore(orphaned));
    }

    private static async Task PrepareWorldAsync(WorldRuntime seed, int ticks)
    {
        var founderPositions = new[]
        {
            new GridPoint(0, 0), new GridPoint(1, 2), new GridPoint(2, 2), new GridPoint(3, 2),
        };
        for (var index = 0; index < founderPositions.Length; index++)
            seed.PlaceFounder("founder:" + (index + 1).ToString("x32", CultureInfo.InvariantCulture), founderPositions[index]);
        seed.StartWorld();
        Assert.True(seed.StageStarterContent());
        for (var tick = 0; tick < ticks; tick++)
            Assert.True((await seed.AdvanceOneTickAsync()).Advanced);
    }

    private static GridPoint FindHouseSite(WorldRuntimeState state, string actor, GridPoint startingSite)
    {
        var occupied = state.Agents.Where(person => person.AgentId != actor)
            .Select(person => person.Position).ToHashSet();
        var reachable = new HashSet<GridPoint> { startingSite };
        var pending = new Queue<GridPoint>();
        pending.Enqueue(startingSite);
        while (pending.TryDequeue(out var current))
        {
            foreach (var next in state.Map.FootNeighbors(current))
            {
                if (occupied.Contains(next) ||
                    state.Map.IsDiagonalFootStep(current, next) &&
                    (occupied.Contains(new GridPoint(next.X, current.Y)) ||
                     occupied.Contains(new GridPoint(current.X, next.Y))))
                    continue;
                if (reachable.Add(next)) pending.Enqueue(next);
            }
        }
        return reachable.OrderBy(point => state.Map.FootDistance(startingSite, point))
            .ThenBy(point => point.Y).ThenBy(point => point.X).First(point =>
            state.Map.IsBuildable(point) &&
            !state.Map.MapObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Agents.Any(person => person.Position == point));
    }

    private static int HouseholdQuantity(WorldRuntime world, string householdId, string kind) =>
        world.Society.Inventory.Lots.Where(lot => lot.OwnerId == householdId && lot.ItemKind == kind)
            .Sum(lot => lot.Quantity);

    private sealed class IdleProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(new CognitionDecisionResponse(
                request.RequestId, request.Observation.AgentId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == "safe_idle" ? 1d : 0d)));
    }

    private sealed class PreferredCandidateProvider(string candidateId) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == candidateId) ??
                request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId,
                request.Observation.AgentId, Kind, ProviderEpoch, request.Observation.RunEpoch,
                request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected.Id, 1,
                request.Observation.Candidates.ToDictionary(candidate => candidate.Id,
                    candidate => candidate.Id == selected.Id ? 1d : 0d)));
        }
    }
}
