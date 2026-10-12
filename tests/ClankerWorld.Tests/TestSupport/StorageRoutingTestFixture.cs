using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class StorageRoutingTestFixture
{
    internal static WorldRuntimeState BlockAccess(WorldRuntime initial, GridPoint storage, int range)
    {
        var map = initial.ExportState().Map;
        var positions = map.Tiles.Where(tile => map.IsPassable(tile.Position) &&
            map.FootDistance(tile.Position, storage) <= range).Select(tile => tile.Position).ToArray();
        Assert.NotEmpty(positions);
        var placements = new Dictionary<string, GridPoint>(StringComparer.Ordinal);
        foreach (var position in positions.Where(position => !initial.Agents.Any(person => person.Position == position)))
        {
            var id = $"agent:{placements.Count + 100:D32}";
            // Agents can walk onto passable gathering sites, although Add Agent
            // requires an empty tile. Set up standing blockers without removing
            // the real resource or changing the map's reachability.
            var spare = map.Tiles.First(tile => map.IsBuildable(tile.Position) &&
                map.FootDistance(tile.Position, storage) > range &&
                !map.Resources.Any(resource => resource.Position == tile.Position) &&
                !map.MapObjects.Any(item => item.Position == tile.Position) &&
                !initial.Agents.Any(person => person.Position == tile.Position) &&
                initial.Towns.Any(town => town.BorderTiles.Contains(tile.Position))).Position;
            initial.AddAgent(id, spare);
            placements.Add(id, position);
        }
        var state = initial.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => placements.TryGetValue(person.AgentId, out var position)
                ? person with { Position = position } : person).ToArray(),
        };
        Assert.All(positions, position => Assert.Contains(state.Agents, person => person.Position == position));
        return state;
    }
}
