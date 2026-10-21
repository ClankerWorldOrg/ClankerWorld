using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class MovementTestWorld
{
    internal static GridPoint BuildingSite(WorldRuntimeState state, BuildingDefinition definition, string actor,
        IEnumerable<GridPoint>? unavailable = null, Func<GridPoint, bool>? allowed = null)
    {
        var ground = new GroundOccupancy(state.Map, state.WorldContent!, state.WorldSimulation!,
            state.Fields, state.RoadTiles, state.Bridges, state.Towns);
        var occupied = GroundOccupancy.Combine(ground.Resources, ground.MapObjects, ground.ProtectedBuildingTiles(),
            ground.RoadsAndBridgeEntrances, ground.Fields, ground.ExpansionSites, ground.MarketSites,
            state.Agents.Where(person => person.AgentId != actor).Select(person => person.Position));
        if (unavailable is not null) occupied.UnionWith(unavailable);
        var roads = ground.Roads.ToHashSet();
        return state.Map.Tiles.Last(tile => (allowed?.Invoke(tile.Position) ?? true) &&
            WorldContentSimulationRules.Footprint(definition, tile.Position)
            .All(point => state.Map.IsBuildable(point) && !occupied.Contains(point)) &&
            BuildingMovementRules.FindEntrance(state.Map, definition, tile.Position, false, occupied, roads) is not null).Position;
    }

    internal static WorldRuntimeState ClearOtherBuildingAccess(WorldRuntimeState state, string? actor = null)
    {
        var definitions = state.WorldContent!.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var access = state.WorldSimulation!.Buildings.SelectMany(building =>
        {
            var entrance = BuildingMovementRules.EntranceFor(BuildingStorageRules.EffectiveDefinition(definitions[building.DefinitionId], building), building);
            return WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)
                .Concat(state.Map.FootNeighbors(entrance)).Append(entrance);
        }).ToHashSet();
        return ClearOtherAccess(state, actor, access);
    }

    internal static UnoccupiedRouteSearch Routes(WorldRuntimeState state, GridPoint origin, HashSet<GridPoint> occupied)
    {
        var rules = new BuildingMovementRules(state.WorldSimulation!.Buildings, state.WorldContent!.Buildings);
        return new UnoccupiedRouteSearch(state.Map, origin, occupied, state.Map.FootStepCost,
            canStep: (from, to) => rules.CanStep(state.Map, from, to), canStop: rules.CanStop);
    }

    internal static GridPoint OpenGround(WorldRuntimeState state, string actor, GridPoint near, int range = int.MaxValue,
        Func<GridPoint, bool>? allowed = null)
    {
        var definitions = state.WorldContent!.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var occupied = state.WorldSimulation!.Buildings.SelectMany(building =>
            WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)
                .Append(BuildingMovementRules.EntranceFor(BuildingStorageRules.EffectiveDefinition(definitions[building.DefinitionId], building), building)))
            .Concat(state.Agents.Where(person => person.AgentId != actor).Select(person => person.Position))
            .Concat(state.Map.Resources.Select(item => item.Position)).Concat(state.Map.MapObjects.Select(item => item.Position)).ToHashSet();
        return state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsBuildable(point) &&
            state.Map.FootDistance(point, near) <= range && !occupied.Contains(point) && (allowed?.Invoke(point) ?? true))
            .OrderBy(point => state.Map.FootDistance(point, near)).ThenBy(point => point.Y).ThenBy(point => point.X).First();
    }

    internal static WorldRuntimeState ClearOtherAccess(WorldRuntimeState state, string? actor, IReadOnlySet<GridPoint> access)
    {
        foreach (var person in state.Agents.Where(person => person.AgentId != actor && access.Contains(person.Position)).ToArray())
        {
            var free = OpenGround(state, person.AgentId, person.Position, allowed: point => !access.Contains(point));
            state = state with { Agents = state.Agents.Select(item => item.AgentId == person.AgentId ? item with { Position = free } : item).ToArray() };
        }
        return state;
    }
}
