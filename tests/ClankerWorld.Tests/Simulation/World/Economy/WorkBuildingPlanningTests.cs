using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorkBuildingPlanningTests
{
    private static readonly Lazy<Task<byte[]>> Baseline = new(async () =>
    {
        using var world = NormalPathWorld.CreateGenerated("probe-a", _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var tick = 0; tick < 12; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        return WorldRuntimeCodec.Encode(world.ExportState());
    });

    [Theory]
    [InlineData("farmhouse")]
    [InlineData("silo")]
    [InlineData("blacksmith")]
    [InlineData("tailor")]
    [InlineData("clinic")]
    [InlineData("restaurant")]
    [InlineData("animal-yard")]
    public async Task EveryWorkBuildingNeedsReachableInputsBeyondItsConstructionStock(string kind)
    {
        var (state, actor, household, building) = await Fixture(kind);
        Assert.Empty(Offers(state, actor, building));
        state = Supply(state, actor, household, kind);
        Assert.NotEmpty(Offers(state, actor, building));
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)));
        restored.Validate();
    }

    [Theory]
    [InlineData("reserved")]
    [InlineData("promised")]
    [InlineData("broken")]
    [InlineData("foreign")]
    [InlineData("unreachable")]
    public async Task RecipeInputsRespectSharedGoodsRules(string boundary)
    {
        var (state, actor, household, building) = await Fixture("tailor");
        state = Supply(state, actor, household, "tailor");
        var inventory = state.Society.Society.Inventory;
        var fiber = inventory.GetLot("work-input-fiber");
        if (boundary == "reserved")
            inventory = InventoryAuthority.Reserve(inventory, "work-input-reservation", household, fiber.Id, 3, "other_work", long.MaxValue);
        else if (boundary == "promised")
        {
            inventory = inventory with
            {
                Lots = inventory.Lots.Select(lot => lot.Id == fiber.Id
                ? lot with { OwnerId = actor, StorageBuildingId = null } : lot).ToArray()
            };
            inventory = InventoryAuthority.AssignDelivery(inventory, fiber.Id, "first-town-house-a");
        }
        else
            inventory = inventory with
            {
                Lots = inventory.Lots.Select(lot => lot.Id != fiber.Id ? lot : boundary switch
                {
                    "broken" => lot with { ConditionBasisPoints = 0 },
                    "foreign" => lot with { OwnerId = "household:second", StorageBuildingId = "first-town-house-b" },
                    _ => lot with { StorageBuildingId = null, GroundPosition = new(FarIsland(state).X, FarIsland(state).Y) },
                }).ToArray(),
            };
        state = WithInventory(state, inventory);
        Assert.Empty(Offers(state, actor, building));
    }

    [Fact]
    public async Task SeedSiteNearTownQualifiesButAnExhaustedOrDistantSiteDoesNot()
    {
        var (state, actor, _, building) = await Fixture("farmhouse");
        var source = state.Map.Resources.Single(item => item.Id == "grain-seed-patch");
        state = state with
        {
            Resources = state.Resources.Select(item => item.ResourceId == source.Id ? item with { State = ResourceState.Available } : item).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(item => item.Id == source.Id
                        ? item with { Quantity = item.Capacity, State = EcologyResourceState.Available } : item).ToArray(),
                },
            },
        };
        Assert.NotEmpty(Offers(state, actor, building));
        var townOrigin = state.Towns!.Single().OriginSite!.Value;
        var distant = state.Map.Resources.First(item => item.Kind == FarmFieldRules.GrainSeed &&
            state.Map.FootDistance(townOrigin, item.Position) > 12);
        state = state with
        {
            Resources = state.Resources.Select(item => item with
            { State = item.ResourceId == distant.Id ? ResourceState.Available : ResourceState.Depleted }).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(item => item with
                    {
                        Quantity = item.Id == distant.Id ? item.Capacity : 0,
                        State = item.Id == distant.Id ? EcologyResourceState.Available : EcologyResourceState.Depleted
                    }).ToArray(),
                },
            },
        };
        Assert.Empty(Offers(state, actor, building));
    }

    [Fact]
    public async Task ExistingHouseholdFieldQualifiesASiloWithoutAnySeeds()
    {
        var (state, actor, household, building) = await Fixture("silo");
        var fertility = new LandFertility(state.Map, state.WorldSeed);
        var ground = new GroundOccupancy(state.Map, state.WorldContent!, state.WorldSimulation!, roads: state.RoadTiles!);
        var occupied = GroundOccupancy.Combine(ground.Resources, ground.MapObjects, ground.Roads, ground.BuildingFootprints);
        var origin = state.Agents.Single(person => person.AgentId == actor).Position;
        var point = state.Map.Tiles.Select(tile => tile.Position).First(tile => fertility.CanFarm(tile) &&
            !occupied.Contains(tile) && state.Map.IsReachableOnFoot(origin, tile));
        Assert.Empty(Offers(state, actor, building));
        Assert.NotEmpty(Offers(state with { Fields = [new(point, household, FarmFieldStage.Prepared)] }, actor, building));
    }

    [Fact]
    public async Task AnimalYardRequiresAnimalsOnTheSameIslandAndWaitsForExistingYardCapacity()
    {
        var (state, actor, household, building) = await Fixture("animal-yard");
        Assert.Empty(Offers(state, actor, building));
        state = Supply(state, actor, household, "animal-yard");
        Assert.NotEmpty(Offers(state, actor, building));
        var animal = Assert.Single(state.AnimalWorld.Animals);
        var far = FarIsland(state);
        Assert.Empty(Offers(state with { AnimalWorld = state.AnimalWorld with { Animals = [animal with { Position = far }] } }, actor, building));
        var otherHousehold = "household:second";
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in building.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "other-yard-cost-" + cost.ResourceId, cost.ResourceId,
                otherHousehold, cost.Amount, storageBuildingId: "first-town-house-b");
        using var placement = WorldRuntime.Restore(WithInventory(state, inventory));
        var site = Offers(state, actor, building).First().SitePosition!.Value;
        var placed = placement.PlaceBuilding("other-yard", building.CanonicalId, site, otherHousehold);
        Assert.True(placed.Applied, placed.Failure);
        Assert.Empty(Offers(placement.ExportState(), actor, building));
        var full = placement.ExportState();
        var yard = placement.WorldSimulation.Buildings.Single(item => item.InstanceId == "other-yard");
        var tiles = WorldContentSimulationRules.Footprint(building, yard).ToArray();
        full = full with
        {
            AnimalWorld = full.AnimalWorld with
            {
                Animals = new[] { animal }.Concat(tiles.Select((point, index) => new AnimalState(
                    "yard-resident-" + index, "Hen " + index, "chicken", "female", animal.BornTick, point,
                    "yard-herd", otherHousehold, yard.InstanceId))).ToArray(),
            },
        };
        Assert.NotEmpty(Offers(full, actor, building));
    }

    [Fact]
    public async Task RestaurantProtectsTownMealsEvenWhenRecipeIngredientsAreAvailable()
    {
        var (state, actor, household, building) = await Fixture("restaurant");
        state = Supply(state, actor, household, "restaurant");
        var inventory = state.Society.Society.Inventory;
        var food = inventory.GetLot("work-input-food");
        inventory = inventory with { Lots = inventory.Lots.Select(lot => lot.Id == food.Id ? lot with { Quantity = 6 } : lot).ToArray() };
        // Two edible recipe inputs plus six meals leave exactly the four residents' daily meals.
        Assert.Empty(Offers(WithInventory(state, inventory), actor, building));
        Assert.NotEmpty(Offers(state, actor, building));
    }

    [Fact]
    public async Task DelayedChoiceCannotStartAfterItsInputsAreReservedForOtherWork()
    {
        var (state, actor, household, building) = await Fixture("tailor");
        var house = state.WorldSimulation!.Buildings.Single(item => item.HouseholdId == household && item.InstanceId.Contains("house-", StringComparison.Ordinal));
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "cloth-for-planning", "cloth", household, 1, storageBuildingId: house.InstanceId);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = house.Position } : person).ToArray(),
        };
        var provider = new ScriptedProvider();
        var held = provider.Hold();
        using var world = WorldRuntime.Restore(state, id => id == actor ? provider : new ActionCoverageRecorder(chooseIdle: true));
        var harness = ModelWorkHarness.For(world);
        Assert.True((await harness.TickAsync()).Advanced);
        Assert.True(held.IsEntered);
        var choice = held.Request.Observation.Candidates.First(item => TownConstructionCandidateIds.TryParse(item.Id, out var selected) &&
            selected.IsBuilding && selected.DefinitionId == building.CanonicalId);
        var bandage = world.WorldContent.Recipes.Single(recipe => recipe.LocalId == "house-bandages");
        Assert.True(world.StartProduction(bandage.CanonicalId, house.InstanceId, actor).Applied);
        held.Reply(choice.Id);
        await harness.WaitForDecisionAsync(actor);
        var step = await harness.TickUntilAdmittedAsync(actor, 4);
        Assert.DoesNotContain(world.Agents, person => person.Project?.CandidateId == choice.Id);
        Assert.DoesNotContain(world.WorldSimulation.Buildings, placed => placed.DefinitionId == building.CanonicalId);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.True(step.Advanced);
    }

    [Fact]
    public async Task ContinuingProjectBlocksWithoutSpendingCostsAndReplaysAfterReload()
    {
        var (state, actor, household, building) = await Fixture("tailor");
        state = Supply(state, actor, household, "tailor");
        var site = Offers(state, actor, building).First().SitePosition!.Value;
        var candidate = TownConstructionCandidateIds.Building(building.CanonicalId, site);
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "future-fiber-held", household,
            "work-input-fiber", 3, "other_work", long.MaxValue);
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Project = new(candidate, building.DisplayName, state.Society.Society.WorldTick, "working", WorkDone: 1, LastTransitionTick: state.Society.Society.WorldTick) } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        var before = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var project = world.Agents.Single(person => person.AgentId == actor).Project!;
        Assert.Equal("blocked", project.Stage);
        Assert.Equal(1, project.WorkDone);
        Assert.Contains("inputs", project.Blocker!, StringComparison.Ordinal);
        foreach (var cost in building.BuildCosts)
            Assert.Equal(cost.Amount, world.Society.Inventory.GetLot("work-cost-" + cost.ResourceId).Quantity);
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    private static async Task<(WorldRuntimeState State, string Actor, string Household, BuildingDefinition Building)> Fixture(string kind)
    {
        var state = WorldRuntimeCodec.Decode(await Baseline.Value);
        var household = kind == "farmhouse" ? "household:second" : "household:first";
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var building = state.WorldContent!.Buildings.Where(item => item.Tags.Contains(kind)).OrderBy(item => item.Width * item.Height).First();
        var house = state.WorldSimulation!.Buildings.Single(item => item.HouseholdId == household && item.InstanceId.Contains("house-", StringComparison.Ordinal));
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [], Offers = [] };
        foreach (var cost in building.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "work-cost-" + cost.ResourceId, cost.ResourceId, household, cost.Amount, storageBuildingId: house.InstanceId);
        state = WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Fields = [],
            AnimalWorld = new(true, [], []),
            Agents = state.Agents.Select(person => person with
            {
                Equipment = null,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(),
            }).ToArray(),
            Resources = state.Resources.Select(item => item with { State = ResourceState.Depleted }).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(item => item with
                    { Quantity = 0, State = EcologyResourceState.Depleted, NextRegenerationDay = 100 }).ToArray(),
                },
            },
        };
        return (state, actor, household, building);
    }

    private static WorldRuntimeState Supply(WorldRuntimeState state, string actor, string household, string kind)
    {
        var house = state.WorldSimulation!.Buildings.Single(item => item.HouseholdId == household && item.InstanceId.Contains("house-", StringComparison.Ordinal));
        var inventory = state.Society.Society.Inventory;
        void Add(string item, int quantity) => inventory = InventoryAuthority.AddLot(inventory, "work-input-" + item, item, household, quantity, storageBuildingId: house.InstanceId);
        switch (kind)
        {
            case "farmhouse": case "silo": Add("grain_seed", 1); break;
            case "blacksmith": Add("wood", 3); break;
            case "tailor": Add("fiber", 3); break;
            case "clinic":
                Add("medicinal_herbs", 2); Add("wood", 1); Add("water_jug", 1);
                inventory = InventoryAuthority.AddLot(inventory, "work-input-water", "fresh_water", household, 1, storageBuildingId: house.InstanceId, containerLotId: "work-input-water_jug");
                break;
            case "restaurant": Add("bread", 1); Add("cultivated_greens", 1); Add("wood", 1); Add("food", 10); break;
            case "animal-yard":
                var born = -(long)AnimalRules.Definition("chicken").AdultDays * state.WorldSystems!.Config.TicksPerDay;
                state = state with { AnimalWorld = new(true, [new("planning-hen", "Hen", "chicken", "female", born, position, "planning-herd")], []) };
                break;
        }
        return WithInventory(state, inventory);
    }

    private static WorldRuntimeState WithInventory(WorldRuntimeState state, InventoryCheckpoint inventory) => state with
    { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };

    private static TownConstructionCandidateSelection[] Offers(WorldRuntimeState state, string actor, BuildingDefinition building)
    {
        using var world = WorldRuntime.Restore(state, _ => new ActionCoverageRecorder(chooseIdle: true));
        world.SubmitInstruction(new("work-plan-refresh", "owner:planning-test", actor, OwnerInstructionKind.Suggestion,
            "Consider the work this household needs."));
        return world.PrepareChoiceTrialOffers().Where(observation => observation.AgentId == actor).SelectMany(observation => observation.Candidates)
            .Where(candidate => TownConstructionCandidateIds.TryParse(candidate.Id, out var selection) &&
                selection.IsBuilding && selection.DefinitionId == building.CanonicalId)
            .Select(candidate => TownConstructionCandidateIds.TryParse(candidate.Id, out var selection) ? selection :
                throw new InvalidOperationException("A filtered construction choice must parse.")).ToArray();
    }

    private static GridPoint FarIsland(WorldRuntimeState state)
    {
        var home = state.WorldSimulation!.Buildings.Single(item => item.InstanceId == "first-town-house-a").Position;
        return state.Map.Tiles.Select(tile => tile.Position).First(point => state.Map.IsBuildable(point) && !state.Map.IsReachableOnFoot(home, point) &&
            state.Map.FootDistance(home, point) > 12 && state.Agents.All(person => person.Position != point));
    }
}
