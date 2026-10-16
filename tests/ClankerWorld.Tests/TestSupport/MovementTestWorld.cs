using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class MovementTestWorld
{
    internal static WorldRuntimeState ClearOtherBuildingAccess(WorldRuntimeState state, string? actor = null)
    {
        var definitions = state.WorldContent!.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        var access = state.WorldSimulation!.Buildings.SelectMany(building =>
            WorldContentSimulationRules.Footprint(definitions[building.DefinitionId], building)
                .Append(BuildingMovementRules.EntranceFor(BuildingStorageRules.EffectiveDefinition(definitions[building.DefinitionId], building), building)))
            .ToHashSet();
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
            var free = OpenGround(state, person.AgentId, person.Position);
            state = state with { Agents = state.Agents.Select(item => item.AgentId == person.AgentId ? item with { Position = free } : item).ToArray() };
        }
        return state;
    }
}
