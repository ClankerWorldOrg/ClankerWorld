using System.Reflection;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GroundOccupancyTests
{
    [Fact]
    public async Task GrownTownKeepsEachOldObstacleUnionAndItsEnumerationOrder()
    {
        using var scenario = await TownProjectScenario.ApprovedAsync(initialTownStock: true);
        Assert.True(scenario.World.WorldTick > 0);
        Assert.Equal("passed", Assert.Single(scenario.World.Towns[0].Council!.Proposals).Status);
        CheckQueries(scenario.World);
        scenario.Policy.Supply = true;
        await scenario.UntilAsync(() => scenario.Project.Stage == "completed", 320);
        Assert.Contains(scenario.World.WorldSimulation.Buildings,
            building => building.InstanceId == scenario.Project.CompletedBuildingId);
        CheckQueries(scenario.World);
        var before = WorldRuntimeCodec.Encode(scenario.World.ExportState());
        scenario.Reload();
        CheckQueries(scenario.World);
        Assert.Equal(before, WorldRuntimeCodec.Encode(scenario.World.ExportState()));
    }

    private static void CheckQueries(WorldRuntime world)
    {
        var state = world.ExportState();
        var map = state.Map;
        var content = state.WorldContent!;
        var simulation = state.WorldSimulation!;
        var towns = ReadField<TownSystem>(world, "townSystem").State.Towns;
        var liveFields = ReadField<FarmingSystem>(world, "farmingSystem").State.Fields;
        var liveRoads = ReadField<TransportSystem>(world, "transportSystem").Roads;
        var liveBridges = ReadField<TransportSystem>(world, "transportSystem").Bridges;
        var ground = new GroundOccupancy(map, content, simulation, liveFields,
            liveRoads, liveBridges, towns, state.AnimalWorld.Animals);
        var definitions = content.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        Assert.Equal(definitions.ToArray(), content.BuildingDefinitions.ToArray());
        var projects = towns.SelectMany(town => town.Projects)
            .Where(project => project.Stage is not ("completed" or "cancelled")).ToArray();
        var old = new Dictionary<string, IEnumerable<GridPoint>>(StringComparer.Ordinal)
        {
            ["resources"] = map.Resources.Select(item => item.Position),
            ["objects"] = map.MapObjects.Select(item => item.Position),
            ["fields"] = liveFields.Select(item => item.Position),
            ["roads"] = liveRoads,
            ["bridges"] = liveBridges.Select(bridge => bridge.Entrances).SelectMany(entrances => entrances),
            ["buildings"] = simulation.Buildings.SelectMany(building =>
                WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)),
            ["original"] = simulation.Buildings.SelectMany(building =>
                WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building.Position)),
            ["protected"] = simulation.Buildings.SelectMany(building =>
                PortNavigationRules.ProtectedBuildingTiles(map, definitions[building.DefinitionId], building)),
            ["expansions"] = (simulation.BuildingExpansions ?? []).Where(job =>
                    job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused)
                .SelectMany(job => Enumerable.Range(0, job.TargetFootprint.Height).SelectMany(y =>
                    Enumerable.Range(0, job.TargetFootprint.Width).Select(x =>
                        new GridPoint(job.TargetPosition.X + x, job.TargetPosition.Y + y)))),
            ["projects"] = projects.SelectMany(project => TownProjectRules.Footprint(project.Plan)),
            ["sites"] = projects.SelectMany(project => TownProjectRules.Footprint(project.Plan))
                .Concat(projects.Where(project => project.Plan.BoatPortId is null).Select(project => project.Plan.Entrance))
                .Concat(projects.Where(project => project.Plan.BoatPortId is null &&
                        TownProjectRules.DefinitionFor(project.Plan.DefinitionId) is { } definition && PortNavigationRules.IsPort(definition))
                    .SelectMany(project => PortNavigationRules.Geometry(map,
                        TownProjectRules.DefinitionFor(project.Plan.DefinitionId)!, project.Plan.Site).DockingTiles)),
            ["markets"] = towns.SelectMany(town => town.Markets).Where(market => market.RemovedTick is null)
                .SelectMany(market => MarketContent.SiteTiles(market.Site)),
            ["slots"] = towns.SelectMany(town => town.Markets).Where(market => market.RemovedTick is null)
                .SelectMany(market => Enumerable.Range(0, MarketContent.MaximumStalls)
                    .Select(slot => MarketContent.StallSite(market.Site, slot))),
            ["animals"] = state.AnimalWorld.Animals.Where(animal => animal.DiedTick is null).Select(animal => animal.Position),
        };
        var current = new Dictionary<string, IEnumerable<GridPoint>>(StringComparer.Ordinal)
        {
            ["resources"] = ground.Resources,
            ["objects"] = ground.MapObjects,
            ["fields"] = ground.Fields,
            ["roads"] = ground.Roads,
            ["bridges"] = ground.BridgeEntrances,
            ["buildings"] = ground.BuildingFootprints,
            ["original"] = ground.OriginalBuildingFootprints,
            ["protected"] = ground.ProtectedBuildingTiles(),
            ["expansions"] = ground.ExpansionSites,
            ["projects"] = ground.ProjectFootprints(),
            ["sites"] = ground.ProtectedProjectSites(),
            ["markets"] = ground.MarketSites,
            ["slots"] = ground.MarketStallSlots,
            ["animals"] = ground.AnimalPositions(),
        };
        foreach (var key in old.Keys) Assert.Equal(old[key].ToArray(), current[key].ToArray());
        // These are the pre-refactor recipes, including their deliberately different layer orders.
        foreach (var recipe in Recipes)
        {
            var names = recipe.Split(' ');
            var expected = names.SelectMany(name => old[name]).ToHashSet();
            var actual = GroundOccupancy.Combine(names.Select(name => current[name]).ToArray());
            Assert.True(expected.SetEquals(actual), recipe);
            Assert.Equal(expected.ToArray(), actual.ToArray());
        }
        HashSet<GridPoint> Expected(string recipe) => recipe.Split(' ').SelectMany(name => old[name]).ToHashSet();
        Assert.Equal(Expected("resources fields objects buildings expansions projects slots").ToArray(),
            Invoke<HashSet<GridPoint>>(world, "RoadOccupiedTiles").ToArray());
        Assert.Equal(old["roads"].Concat(old["bridges"]).ToArray(),
            Invoke<IEnumerable<GridPoint>>(world, "RoadAndBridgeTiles").ToArray());
        var planting = Invoke<(HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied)>(world, "PlantingObstacles");
        Assert.Equal(Expected("buildings expansions sites markets").ToArray(), planting.Buildings.ToArray());
        Assert.Equal(Expected("objects resources fields").ToArray(), planting.Occupied.ToArray());
        Assert.Equal(Expected("resources objects fields roads bridges buildings expansions sites").ToArray(),
            Invoke<HashSet<GridPoint>>(world, "PortObstacles", null, null, false).ToArray());
        Assert.Equal(Expected("resources objects fields roads bridges protected expansions sites").ToArray(),
            Invoke<HashSet<GridPoint>>(world, "PortObstacles", null, null, true).ToArray());
        Assert.Equal(old["sites"].ToArray(), Invoke<IEnumerable<GridPoint>>(world, "TownProjectProtectedSites", (object?)null).ToArray());
        Assert.Equal(old["markets"].ToArray(), Invoke<IEnumerable<GridPoint>>(world, "MarketSiteTiles").ToArray());
        var placement = Invoke<object>(world, "GatherBuildingPlacementTiles", (object?)null);
        var expectedPlacement = Expected("objects resources roads bridges fields sites expansions buildings");
        expectedPlacement.UnionWith(simulation.Buildings.Where(building => PortNavigationRules.IsPort(definitions[building.DefinitionId]))
            .SelectMany(building => PortNavigationRules.Geometry(map, definitions[building.DefinitionId], building.Position).DockingTiles));
        Assert.Equal(expectedPlacement.ToArray(), ReadProperty<HashSet<GridPoint>>(placement, "Occupied").ToArray());
        Assert.Equal(old["markets"].ToHashSet().ToArray(), ReadProperty<HashSet<GridPoint>>(placement, "Market").ToArray());
        var siteWorld = Invoke<object>(world, "CreateTownProjectSiteWorld", towns[0], null, false);
        Assert.Equal(Expected("resources objects fields buildings expansions sites").ToArray(),
            ReadProperty<Lazy<HashSet<GridPoint>>>(siteWorld, "Occupied").Value.ToArray());
        var ports = Invoke<object>(world, "CreatePortProjectSiteWorld", (object?)null);
        Assert.Equal(Expected("resources objects fields roads bridges expansions sites").ToArray(),
            ReadProperty<HashSet<GridPoint>>(ports, "Common").ToArray());
        foreach (var animal in state.AnimalWorld.Animals)
            Assert.Equal(state.AnimalWorld.Animals.Where(other => other.DiedTick is null && other.Id != animal.Id)
                .Select(other => other.Position).ToArray(), ground.AnimalPositions(animal.Id).ToArray());
        foreach (var project in projects)
            Assert.Equal(projects.Where(other => other.Id != project.Id).SelectMany(other =>
                TownProjectRules.Footprint(other.Plan)).ToArray(), ground.ProjectFootprints(project.Id).ToArray());
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(world.ExportState()));
    }

    [Fact]
    public void ReadViewsKeepOriginalAndExpandedGeometrySeparateAndDoNotHideMissingDefinitions()
    {
        using var world = NormalPathWorld.CreateGenerated("ground-geometry-reader", _ => new DeterministicDecisionProvider());
        var state = world.ExportState();
        var content = state.WorldContent!;
        var original = state.WorldSimulation!.Buildings[0];
        var definition = content.Buildings.Single(item => item.CanonicalId == original.DefinitionId);
        // A component-only reader control: this geometry is never restored, committed or claimed as paid gameplay.
        var expanded = original with { Footprint = new(definition.Width + 1, definition.Height + 1, 2) };
        var simulation = state.WorldSimulation with { Buildings = [expanded] };
        var ground = new GroundOccupancy(state.Map, content, simulation);
        Assert.Equal(WorldContentSimulationRules.Footprint(definition, original.Position).ToArray(),
            ground.OriginalBuildingFootprints.ToArray());
        Assert.Equal(WorldContentSimulationRules.Footprint(definition, expanded).ToArray(),
            ground.BuildingFootprints.ToArray());
        Assert.True(ground.BuildingFootprints.Count() > ground.OriginalBuildingFootprints.Count());
        var unavailable = expanded with { DefinitionId = "unavailable-definition" };
        var missing = new GroundOccupancy(state.Map, content, simulation with { Buildings = [unavailable] });
        Assert.Equal(new[] { unavailable.Position }, missing.PlantingBuildingFootprints.ToArray());
        Assert.Throws<InvalidDataException>(() => missing.CheckedBuildingFootprints.ToArray());
        Assert.Throws<InvalidDataException>(() => missing.OriginalBuildingFootprints.ToArray());
        Assert.Empty(missing.KnownBuildingFootprints(_ => true));
        Assert.Equal(WorldRuntimeCodec.Encode(state), WorldRuntimeCodec.Encode(world.ExportState()));
    }

    private static readonly string[] Recipes =
    [
        "objects resources", // Initial Town layout and declarative placement.
        "objects resources roads bridges fields sites markets expansions buildings", // Town layout basis, before its live overlays.
        "objects resources roads bridges fields sites expansions buildings", // Building placement.
        "objects resources fields roads bridges buildings expansions", // Settlement resources.
        "buildings expansions sites markets", // Planting's building obstacles.
        "objects resources fields", // Planting's other obstacles.
        "resources objects roads buildings expansions", // Saved fields.
        "resources fields objects buildings expansions projects slots", // Roads and traffic bridge banks.
        "resources objects original", // Saved bridge banks retain original footprints.
        "buildings resources roads bridges", // Wild animal seeding.
        "buildings", // Saved road repair.
        "resources objects fields buildings expansions sites", // Town project sites.
        "resources objects fields protected expansions", // Saved Town projects, before other-project overlay.
        "resources objects fields roads bridges buildings expansions sites", // Port obstacles.
        "resources objects fields roads bridges protected expansions sites", // Port obstacles with protected docking tiles.
        "resources objects fields roads bridges expansions sites", // Port construction's common obstacles.
    ];

    private static T Invoke<T>(WorldRuntime world, string method, params object?[] arguments) =>
        (T)typeof(WorldRuntime).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, arguments)!;

    private static T ReadField<T>(WorldRuntime world, string name) =>
        (T)typeof(WorldRuntime).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;

    private static T ReadProperty<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
}
