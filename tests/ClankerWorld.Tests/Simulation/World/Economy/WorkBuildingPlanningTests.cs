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
            inventory = InventoryAuthority.AssignDelivery(inventory, fiber.Id, "first-town-blacksmith");
        else
            inventory = inventory with
            {
                Lots = inventory.Lots.Select(lot => lot.Id != fiber.Id ? lot : boundary switch
                {
                    "broken" => lot with { ConditionBasisPoints = 0 },
                    "foreign" => lot with { OwnerId = "household:second" },
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
        var far = FarIsland(state);
        state = state with { Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = far } : person).ToArray() };
        Assert.Empty(Offers(state, actor, building));
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
        using var placement = WorldRuntime.Restore(state);
        var otherHousehold = "household:second";
        var site = Offers(state, actor, building).First().SitePosition!.Value;
        Assert.True(placement.PlaceBuilding("other-yard", building.CanonicalId, site, otherHousehold).Applied);
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
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "cloth-for-planning", "cloth", household, 2, storageBuildingId: house.InstanceId);
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
                Equipment = null, Project = null, LastDecisionContext = null, FullnessBasisPoints = 10_000,
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
                var position = state.Agents.Single(person => person.AgentId == actor).Position;
                state = state with { AnimalWorld = new(true, [new("planning-hen", "Hen", "chicken", "female", -3600, position, "planning-herd")], []) };
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
