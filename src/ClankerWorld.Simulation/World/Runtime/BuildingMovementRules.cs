using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

/// <summary>Building walls and their single outdoor doorway, shared by traveler searches.</summary>
internal sealed class BuildingMovementRules
{
    private readonly Dictionary<GridPoint, Doorway> interiors = [];

    internal BuildingMovementRules(IReadOnlyList<PlacedBuilding> buildings, IReadOnlyList<BuildingDefinition> definitions)
    {
        var byId = definitions.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
        foreach (var building in buildings)
        {
            var definition = BuildingStorageRules.EffectiveDefinition(byId[building.DefinitionId], building);
            // Buildings without a Road use the same south-facing door as their drawing.
            var entrance = EntranceFor(definition, building);
            var doorway = new Doorway(entrance);
            foreach (var point in WorldContentSimulationRules.Footprint(definition, building.Position))
                interiors.Add(point, doorway);
        }
    }

    internal static GridPoint EntranceFor(BuildingDefinition effectiveDefinition, PlacedBuilding building) =>
        building.Entrance ?? new GridPoint(building.Position.X + effectiveDefinition.Width / 2,
            building.Position.Y + effectiveDefinition.Height);

    internal bool CanStep(WorldMap map, GridPoint from, GridPoint to)
    {
        interiors.TryGetValue(from, out var origin);
        interiors.TryGetValue(to, out var destination);
        if (!ReferenceEquals(origin, destination))
        {
            // A shared building edge is two walls, never an outdoor doorway.
            if (origin is not null && destination is not null || map.IsDiagonalFootStep(from, to)) return false;
            return origin is not null ? origin.Entrance == to : destination!.Entrance == from;
        }
        // Inside one footprint, both shoulders are inside too. Outside, do not
        // cut diagonally past a building corner, even beside its entrance.
        return origin is not null || !map.IsDiagonalFootStep(from, to) ||
            !interiors.ContainsKey(new GridPoint(to.X, from.Y)) &&
            !interiors.ContainsKey(new GridPoint(from.X, to.Y));
    }

    // Ranged interactions can finish outdoors. Inside a footprint, finish at
    // the requested tile rather than parking on its internal approach.
    internal bool CanStop(GridPoint point, GridPoint destination) =>
        !interiors.ContainsKey(point) || point == destination;

    private sealed record Doorway(GridPoint Entrance);
}
