using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownLayoutFootCostTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(false, 5)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 5)]
    public void LayoutCostsKeepIndependentRouteCostsAndExactStopping(bool withoutTown, int selection)
    {
        using var world = NormalPathWorld.CreateGenerated("town-project-real-donation", _ => new DeterministicDecisionProvider());
        var state = world.ExportState();
        var before = WorldRuntimeCodec.Encode(state);
        var map = state.Map;
        var person = state.Agents[0];
        var origin = person.Position;
        var town = withoutTown ? null : Assert.Single(world.Towns);
        var ground = new GroundOccupancy(map, state.WorldContent!, state.WorldSimulation!);
        var sites = GroundOccupancy.Combine(ground.MapObjects, ground.Resources, ground.BuildingFootprints);
        if (selection == 5)
            origin = map.Tiles.Last(tile => map.IsBuildable(tile.Position) && !sites.Contains(tile.Position) &&
                state.Agents.All(agent => agent.Position != tile.Position)).Position;
        var bounds = TownLayoutContext.CandidateBounds(map, town).ToHashSet();
        GridPoint? selected = null;
        KeyValuePair<GridPoint, int>[] expected;
        // Component query inputs only; no altered authority state is installed in the world.
        if (selection is 2 or 3 or 4)
        {
            if (selection == 2) { selected = origin; sites.Add(origin); }
            if (selection == 3) selected = map.Tiles.First(tile => map.IsBuildable(tile.Position) &&
                !sites.Contains(tile.Position) && !bounds.Contains(tile.Position)).Position;
            if (selection == 4) sites.UnionWith(bounds);
            expected = [new(origin, 0)];
        }
        else
        {
            var occupied = state.Agents.Where(other => other.AgentId != person.AgentId)
                .Select(other => other.Position).ToHashSet();
            var buildings = new BuildingMovementRules(state.WorldSimulation!.Buildings, state.WorldContent!.Buildings);
            var roads = (state.RoadTiles ?? []).Concat((state.Bridges ?? [])
                .Where(bridge => bridge.Trigger == BridgeTriggers.Road).SelectMany(bridge => bridge.Span)).ToHashSet();
            int StepCost(GridPoint from, GridPoint to) => RoadRoutePlanner.AdjustFootCost(
                map.FootStepCost(from, to), roads.Contains(from) && roads.Contains(to));
            Func<GridPoint, GridPoint, int> stepCost = StepCost;
            using var routes = new UnoccupiedRouteSearch(map, origin, occupied, stepCost,
                canStep: (from, to) => buildings.CanStep(map, from, to));
            var costs = new Dictionary<GridPoint, int> { [origin] = 0 };
            foreach (var tile in map.Tiles)
            {
                var route = routes.RouteTo(tile.Position, 0);
                var cost = 0;
                for (var index = 1; index < route.Count; index++)
                {
                    if (!costs.TryGetValue(route[index], out var nextCost))
                    {
                        nextCost = checked(cost + stepCost(route[index - 1], route[index]));
                        costs.Add(route[index], nextCost);
                    }
                    cost = nextCost;
                }
            }
            var ordered = costs.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X).ToArray();
            var indices = ordered.Select((pair, index) => (pair.Key, index)).ToDictionary(pair => pair.Key, pair => pair.index);
            var viable = ordered.Where(pair => map.IsBuildable(pair.Key) && !sites.Contains(pair.Key)).ToArray();
            var stop = ordered.Length - 1;
            if (selection == 1)
            {
                selected = viable.Last(pair => withoutTown || bounds.Contains(pair.Key)).Key;
                stop = indices[selected.Value];
                if (withoutTown) Assert.True(viable.Length > 32);
            }
            else if (withoutTown && viable.Length >= 32) stop = indices[viable[31].Key];
            else if (!withoutTown)
            {
                var anchors = bounds.Where(point => map.IsBuildable(point) && !sites.Contains(point)).ToArray();
                if (anchors.All(costs.ContainsKey)) stop = anchors.Length == 0 ? 0 : anchors.Max(point => indices[point]);
            }
            expected = ordered.Take(stop + 1).ToArray();
        }
        var actual = world.FindUnoccupiedFootCosts(person.AgentId, origin, town, sites, selected);
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(before, WorldRuntimeCodec.Encode(world.ExportState()));
    }
}
