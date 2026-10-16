using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Server.Observation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class BuildingMovementTests
{
    [Fact]
    public async Task ScoutingChoosesLegalNeighboursAndKeepsAValidOutingAcrossReload()
    {
        var actor = NormalPathWorld.Founder1;
        var policy = new MarketRulesPolicy
        {
            Choose = (person, candidates) => candidates.FirstOrDefault(candidate => person == actor && candidate.Id == "explore") ??
                candidates.Single(candidate => candidate.Id == "safe_idle"),
        };
        using var generated = NormalPathWorld.CreateGenerated("building-scouting", policy.CreateProvider);
        Assert.True((await generated.AdvanceOneTickAsync()).Advanced);
        var state = generated.ExportState();
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        state = MovementTestWorld.ClearOtherBuildingAccess(state, actor);
        state = WeatherTestFixture.WithWeather(state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = house.Position,
                FullnessBasisPoints = 10_000,
                Survival = new(),
                TravelCooldownTicks = 0,
                Project = null,
                Exploration = null,
                LastDecisionContext = null,
            } : person).ToArray(),
        }, WeatherKind.Clear);
        var rules = new BuildingMovementRules(state.WorldSimulation!.Buildings, state.WorldContent!.Buildings);
        using var world = WorldRuntime.Restore(state, policy.CreateProvider);
        using var replay = WorldRuntime.Restore(state, policy.CreateProvider);
        var previous = house.Position;
        var moved = false;
        for (var tick = 0; tick < 32; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            var person = world.Agents.Single(person => person.AgentId == actor);
            if (person.Position != previous)
            {
                if (!moved) Assert.Equal(house.Entrance, person.Position);
                Assert.True(rules.CanStep(state.Map, previous, person.Position));
                moved = true;
            }
            previous = person.Position;
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Equal(saved, WorldRuntimeCodec.Encode(replay.ExportState()));
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), policy.CreateProvider);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        Assert.True(moved);
    }

    [Fact]
    public async Task NeighbouringBuildingsRequireAnOutdoorWalkBetweenDoorways()
    {
        using var generated = NormalPathWorld.CreateGenerated("building-foot-routes", _ => new VariantActionProvider());
        var state = generated.ExportState();
        var actor = NormalPathWorld.Founder1;
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var definitions = state.WorldContent!.Buildings.Where(definition => definition.LocalId is "tailor-shop-1x1" or "clinic-1x1")
            .OrderBy(definition => definition.LocalId, StringComparer.Ordinal).ToArray();
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definitions.SelectMany(definition => definition.BuildCosts).GroupBy(cost => cost.ResourceId))
            inventory = InventoryAuthority.AddLot(inventory, "walk-build-" + cost.Key, cost.Key, household,
                cost.Sum(item => item.Amount));
        using var setup = WorldRuntime.Restore(BuildingVariantTestWorld.WithInventory(state, inventory), _ => new VariantActionProvider());
        var blocked = state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building))
            .Concat(state.Map.Resources.Select(item => item.Position)).Concat(state.Map.MapObjects.Select(item => item.Position))
            .Concat(setup.RoadTiles).Concat(state.Towns!.SelectMany(town => town.BorderTiles)).ToHashSet();
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            Enumerable.Range(-2, 6).All(dx => Enumerable.Range(-2, 6).All(dy =>
            {
                var tile = new GridPoint(point.X + dx, point.Y + dy);
                return state.Map.IsBuildable(tile) && !blocked.Contains(tile);
            })));
        var destination = new GridPoint(site.X + 1, site.Y);
        var first = setup.PlaceBuilding("walk-first", definitions[0].CanonicalId, site, household);
        Assert.True(first.Applied, first.Failure);
        var second = setup.PlaceBuilding("walk-second", definitions[1].CanonicalId, destination, household);
        Assert.True(second.Applied, second.Failure);
        state = setup.ExportState();
        state = WeatherTestFixture.WithWeather(state with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = site, FullnessBasisPoints = 10_000, TravelCooldownTicks = 0, Project = null, LastDecisionContext = null } : person).ToArray(),
        }, WeatherKind.Clear);
        using var world = WorldRuntime.Restore(BuildingVariantTestWorld.Strict(state), _ => new VariantActionProvider());
        var receipt = world.SubmitInstruction(new("between-buildings", "owner:test", actor, OwnerInstructionKind.Order,
            FormattableString.Invariant($"Move to tile ({destination.X}, {destination.Y})")));
        using var replay = WorldRuntime.Restore(BuildingVariantTestWorld.Strict(world.ExportState()), _ => new VariantActionProvider());
        var positions = new List<GridPoint> { site };
        var sawPlannedRoute = false;
        for (var tick = 0; tick < 32 && positions[^1] != destination; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
            positions.Add(world.Agents.Single(person => person.AgentId == actor).Position);
            var route = new OwnerWorldObservationStore(world).GetSnapshot().Agents.Single(person => person.Id == actor).PlannedRoute;
            if (route is not null)
            {
                sawPlannedRoute = true;
                var planned = route.Steps.Select(point => new GridPoint(point.X, point.Y)).Prepend(positions[^1]).ToArray();
                Assert.Equal(destination, planned[^1]);
                Assert.DoesNotContain(site, planned);
                Assert.All(planned.Zip(planned.Skip(1)).Where(edge => edge.Second == destination),
                    edge => Assert.Equal(new GridPoint(destination.X, destination.Y + 1), edge.First));
            }
        }
        Assert.True(sawPlannedRoute);
        Assert.Equal(destination, positions[^1]);
        Assert.Contains(new GridPoint(site.X, site.Y + 1), positions);
        Assert.Contains(new GridPoint(destination.X, destination.Y + 1), positions);
        Assert.Equal("finished", world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Task!.Status);
    }

    [Theory]
    [InlineData(2, 1, false)]
    [InlineData(2, 4, false)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 4, true)]
    [InlineData(1, 2, true)]
    public void FootAndSwimmingSearchesUseTheSameDoorwayAndNeverCrossAnotherBuilding(int doorX, int doorY, bool swimming)
    {
        var map = Meadow();
        var definition = Definition();
        PlacedBuilding[] buildings = [new("work", definition.CanonicalId, new(2, 2), 0, Entrance: new(doorX, doorY)),
            new("neighbour", definition.CanonicalId, new(4, 2), 0, Entrance: new(4, 4))];
        var rules = new BuildingMovementRules(buildings, [definition]);
        bool CanStep(GridPoint from, GridPoint to) => rules.CanStep(map, from, to);
        Func<GridPoint, IEnumerable<GridPoint>>? neighbors = swimming ? point => SwimmingRules.Neighbors(map, point) : null;
        var origin = new GridPoint(2, 2);
        var destination = new GridPoint(4, 2);
        using var forward = new UnoccupiedRouteSearch(map, origin, [], map.FootStepCost, neighbors, CanStep);
        var route = forward.RouteTo(destination, 0);
        Assert.NotEmpty(route);
        Assert.Contains(new GridPoint(doorX, doorY), route);
        Assert.Contains(new GridPoint(4, 4), route);
        Assert.DoesNotContain(route.Zip(route.Skip(1)), edge => edge.First.X == 3 && edge.Second.X == 4 &&
            edge.First.Y is 2 or 3 && edge.Second.Y is 2 or 3);
        // An unrelated building cannot shorten an outdoor journey, even when its
        // footprint lies directly between the endpoints.
        using var outdoor = new UnoccupiedRouteSearch(map, new(0, 2), [], map.FootStepCost, neighbors, CanStep);
        Assert.DoesNotContain(outdoor.RouteTo(new(6, 2), 0), point =>
            point.Y is 2 or 3 && point.X is >= 2 and <= 5);
        HashSet<GridPoint> occupied = [new(doorX, doorY)];
        var reverse = new UnoccupiedReachabilitySearch(map, occupied, destination, 0, neighbors, CanStep);
        Assert.False(reverse.CanReachFrom(origin));
        using var blocked = new UnoccupiedRouteSearch(map, origin, occupied, map.FootStepCost, neighbors, CanStep);
        Assert.Empty(blocked.RouteTo(destination, 0));
        var reachable = new UnoccupiedReachabilitySearch(map, [], destination, 0, neighbors, CanStep);
        foreach (var tile in map.Tiles)
        {
            using var search = new UnoccupiedRouteSearch(map, tile.Position, [], map.FootStepCost, neighbors, CanStep);
            Assert.Equal(search.RouteTo(destination, 0).Count > 0, reachable.CanReachFrom(tile.Position));
        }
    }

    [Fact]
    public void AnEntranceInsideTheNeighbourDoesNotOpenTheirSharedWall()
    {
        var map = Meadow();
        var definition = Definition();
        var first = new PlacedBuilding("work", definition.CanonicalId, new(2, 2), 0, Entrance: new(4, 2));
        var open = new BuildingMovementRules([first], [definition]);
        using var control = new UnoccupiedRouteSearch(map, new(2, 2), [], map.FootStepCost,
            canStep: (from, to) => open.CanStep(map, from, to));
        Assert.NotEmpty(control.RouteTo(new(4, 2), 0));
        var rules = new BuildingMovementRules([
            first,
            new("neighbour", definition.CanonicalId, new(4, 2), 0, Entrance: new(4, 4))], [definition]);
        bool CanStep(GridPoint from, GridPoint to) => rules.CanStep(map, from, to);
        using var search = new UnoccupiedRouteSearch(map, new(2, 2), [], map.FootStepCost, canStep: CanStep);
        Assert.Empty(search.RouteTo(new(4, 2), 0));
        Assert.False(new UnoccupiedReachabilitySearch(map, [], new(4, 2), 0, canStep: CanStep).CanReachFrom(new(2, 2)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnInteractionApproachCannotEndInsideAnUnrelatedBuilding(bool swimming)
    {
        var map = Meadow();
        var definition = Definition();
        var rules = new BuildingMovementRules([
            new("work", definition.CanonicalId, new(2, 2), 0, Entrance: new(1, 2))], [definition]);
        bool CanStep(GridPoint from, GridPoint to) => rules.CanStep(map, from, to);
        Func<GridPoint, IEnumerable<GridPoint>>? neighbors = swimming ? point => SwimmingRules.Neighbors(map, point) : null;
        var destination = new GridPoint(4, 2);
        using var search = new UnoccupiedRouteSearch(map, new(0, 2), [], map.FootStepCost, neighbors, CanStep, rules.CanStop);
        var route = search.RouteTo(destination, 1);
        Assert.NotEmpty(route);
        Assert.InRange(map.FootDistance(route[^1], destination), 0, 1);
        Assert.DoesNotContain(route, point => point.X is 2 or 3 && point.Y is 2 or 3);
        var reverse = new UnoccupiedReachabilitySearch(map, [], destination, 1, neighbors, CanStep, rules.CanStop);
        foreach (var tile in map.Tiles)
        {
            using var forward = new UnoccupiedRouteSearch(map, tile.Position, [], map.FootStepCost, neighbors, CanStep, rules.CanStop);
            Assert.Equal(forward.RouteTo(destination, 1).Count > 0, reverse.CanReachFrom(tile.Position));
        }
    }

    [Fact]
    public void AnInteractionApproachDoesNotStopShortInsideTheDestinationBuilding()
    {
        var map = Meadow();
        var definition = Definition();
        var rules = new BuildingMovementRules([
            new("work", definition.CanonicalId, new(2, 2), 0, Entrance: new(3, 4))], [definition]);
        using var search = new UnoccupiedRouteSearch(map, new(3, 5), [], map.FootStepCost,
            canStep: (from, to) => rules.CanStep(map, from, to), canStop: rules.CanStop);
        var route = search.RouteTo(new(2, 2), 1);
        Assert.NotEmpty(route);
        Assert.True(route[^1] == new GridPoint(2, 2) ||
            route[^1].X is < 2 or > 3 || route[^1].Y is < 2 or > 3);
        Assert.InRange(map.FootDistance(route[^1], new(2, 2)), 0, 1);
        Assert.False(rules.CanStop(new(3, 3), new(2, 2)));
    }

    [Fact]
    public void ExpandedFootprintsBlockTheirNewWallsAndOutdoorDiagonalCorners()
    {
        var map = Meadow();
        var definition = Definition(1, 1);
        var building = new PlacedBuilding("expanded", definition.CanonicalId, new(2, 2), 0,
            Footprint: new BuildingFootprintRevision(2, 2, 1));
        var rules = new BuildingMovementRules([building], [definition]);
        Assert.False(rules.CanStep(map, new(3, 2), new(4, 2)));
        Assert.False(rules.CanStep(map, new(1, 2), new(2, 1)));
        Assert.False(rules.CanStep(map, new(2, 1), new(1, 2)));
        Assert.True(rules.CanStep(map, new(3, 3), new(3, 4)));
        Assert.True(rules.CanStep(map, new(3, 4), new(3, 3)));
    }

    private static BuildingDefinition Definition(int width = 2, int height = 2) => new("sha256:" + new string('a', 64), "work", ContentVersion.Parse("1.0.0"),
        "Work", width, height, 1, [], ["workshop"]);

    private static WorldMap Meadow() => new(8, 6, 0,
        [.. from y in Enumerable.Range(0, 6) from x in Enumerable.Range(0, 8)
            select new TerrainTile(new(x, y), TerrainKind.Meadow)], [], [], string.Empty);
}
