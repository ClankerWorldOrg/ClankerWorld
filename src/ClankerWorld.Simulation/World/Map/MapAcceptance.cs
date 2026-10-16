using System.Runtime.CompilerServices;

namespace ClankerWorld.Simulation.World;

public sealed record MapValidationResult(bool IsValid, string? Failure)
{
    public static MapValidationResult Valid { get; } = new(true, null);

    public static MapValidationResult Invalid(string failure) => new(false, failure);
}

/// <summary>
/// The first-world generated-map acceptance checks used by the seed corpus.
/// </summary>
public static class MapAcceptance
{
    private static readonly ConditionalWeakTable<WorldMap, StartingSiteTraversalCache> StartingSiteTraversals = new();

    private sealed class StartingSiteTraversalCache
    {
        private readonly Lock gate = new();
        private WorldMap? topology;
        private GridPoint origin;
        private HashSet<GridPoint>? reachable;

        internal HashSet<GridPoint> Get(WorldMap map, GridPoint startingPoint)
        {
            lock (gate)
            {
                if (topology is not null && origin == startingPoint && SameWalkability(map, topology))
                    return reachable!;

                // Arrays and read-only collection views may still be borrowed.
                // Compare actual contents before reuse, and traverse owned data
                // so an old terrain index cannot hide a caller's tile edit.
                var snapshot = map with
                {
                    Tiles = map.Tiles.ToArray(),
                    HydrologyKinds = map.HydrologyKinds?.ToArray(),
                    ElevationLevels = map.ElevationLevels?.ToArray(),
                    SurfaceKinds = map.SurfaceKinds?.ToArray(),
                    BridgeDecks = map.BridgeDecks?.ToDictionary(),
                    ClimateZones = null,
                    VegetationKinds = null,
                    MapObjects = [],
                    Resources = [],
                    ManifestDigest = string.Empty,
                };
                var fresh = ReachableFrom(snapshot, startingPoint);
                topology = snapshot;
                origin = startingPoint;
                reachable = fresh;
                return fresh;
            }
        }
    }

    private static bool SameWalkability(WorldMap map, WorldMap snapshot)
    {
        if (map.Width != snapshot.Width || map.Height != snapshot.Height ||
            map.WrapsEastWest != snapshot.WrapsEastWest || map.Tiles.Count != snapshot.Tiles.Count ||
            !SameLayer(map.HydrologyKinds, snapshot.HydrologyKinds) ||
            !SameLayer(map.ElevationLevels, snapshot.ElevationLevels) ||
            !SameLayer(map.SurfaceKinds, snapshot.SurfaceKinds)) return false;
        for (var index = 0; index < map.Tiles.Count; index++)
            if (map.Tiles[index] != snapshot.Tiles[index]) return false;
        if (map.BridgeDecks is null) return snapshot.BridgeDecks is null;
        if (snapshot.BridgeDecks is null || map.BridgeDecks.Count != snapshot.BridgeDecks.Count) return false;
        foreach (var (point, axis) in map.BridgeDecks)
            if (!snapshot.BridgeDecks.TryGetValue(point, out var savedAxis) || axis != savedAxis) return false;
        return true;
    }

    private static bool SameLayer(byte[]? current, byte[]? snapshot) =>
        current is null ? snapshot is null : snapshot is not null && current.AsSpan().SequenceEqual(snapshot);

    public static MapValidationResult Validate(WorldMap map, bool allowEmptyStartingSite = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Width <= 0 || map.Height <= 0 || map.GenerationAttempt < 0 ||
            map.GenerationAttempt >= SmallTemperateMapGenerator.MaximumAttempts)
        {
            return MapValidationResult.Invalid("The map dimensions or generation attempt are invalid.");
        }

        if (map.Tiles.Count != map.Width * map.Height ||
            map.Tiles.Select(tile => tile.Position).Distinct().Count() != map.Tiles.Count ||
            map.Tiles.Any(tile => !map.Contains(tile.Position)))
        {
            return MapValidationResult.Invalid("The logical grid is not a complete bounded rectangle.");
        }

        if (map.ClimateZones is { } zones &&
            (zones.Length != map.Width * map.Height || zones.Any(zone => !Enum.IsDefined((ClimateZone)zone))))
            return MapValidationResult.Invalid("The climate layer is invalid.");
        if (map.ElevationLevels is { } elevation && elevation.Length != map.Width * map.Height ||
            map.HydrologyKinds is { } hydrology &&
                (hydrology.Length != map.Width * map.Height || hydrology.Any(value => !Enum.IsDefined((WaterKind)value))) ||
            map.SurfaceKinds is { } surfaces &&
                (surfaces.Length != map.Width * map.Height || surfaces.Any(value => !Enum.IsDefined((SurfaceKind)value))) ||
            map.VegetationKinds is { } vegetation &&
                (vegetation.Length != map.Width * map.Height || vegetation.Any(value => !Enum.IsDefined((VegetationCover)value))))
            return MapValidationResult.Invalid("A generated geography layer is invalid.");

        var founder = map.MapObjects.SingleOrDefault(mapObject =>
            string.Equals(mapObject.Kind, "founder", StringComparison.Ordinal));
        if (!allowEmptyStartingSite && (founder is null || !map.IsBuildable(founder.Position)))
        {
            return MapValidationResult.Invalid("The founder must occupy passable ground.");
        }
        if (allowEmptyStartingSite && founder is not null)
            return MapValidationResult.Invalid("An empty starting site cannot contain a founder marker.");

        var requiredKinds = new[] { "shelter", "storage", "cooking" };
        // Generated settlements use real buildings; their authored camp objects
        // may be placed individually after founding. Legacy camps require the trio.
        if (!allowEmptyStartingSite && requiredKinds.Any(kind => !map.MapObjects.Any(mapObject =>
                string.Equals(mapObject.Kind, kind, StringComparison.Ordinal))))
        {
            return MapValidationResult.Invalid("A required starting-site placement is missing.");
        }

        if (map.MapObjects.Select(mapObject => mapObject.Id).Distinct(StringComparer.Ordinal).Count() != map.MapObjects.Count ||
            map.MapObjects.Select(mapObject => mapObject.Position).Distinct().Count() != map.MapObjects.Count ||
            map.MapObjects.Any(mapObject => !map.IsBuildable(mapObject.Position)))
        {
            return MapValidationResult.Invalid("Starting-site objects are duplicated or overlap impassable terrain.");
        }

        if (map.Resources.Select(resource => resource.Id).Distinct(StringComparer.Ordinal).Count() != map.Resources.Count ||
            map.Resources.Any(resource => !IsValidResourcePosition(map, resource) ||
                (resource.TreeKind is not null &&
                    resource.TreeKind switch
                    {
                        "broadleaf" or "conifer" => resource.Kind != "construction",
                        "orchard" => resource.Kind != "fruit",
                        _ => true,
                    }) ||
                resource.NaturalObjectKind is { } naturalObjectKind &&
                    !IsValidNaturalObjectKind(resource.Kind, resource.TreeKind, naturalObjectKind)) ||
            map.Resources.Where(resource => resource.TreeKind is not null || resource.NaturalObjectKind is not null)
                .GroupBy(resource => resource.Position).Any(group => group.Count() > 1))
        {
            return MapValidationResult.Invalid("Resource placements are invalid.");
        }

        if (map.Resources.Any(resource => TerrainPlacementRules.IsOrdinaryVegetation(resource) &&
                map.SurfaceAt(resource.Position) is { } surface &&
                !TerrainPlacementRules.CanHoldOrdinaryVegetation(surface)))
            return MapValidationResult.Invalid("A tree or plant stands on sand or other ground that cannot hold it.");

        if (map.VegetationKinds is not null && map.Tiles.Any(tile =>
                map.VegetationAt(tile.Position) == VegetationCover.Cactus &&
                (map.SurfaceAt(tile.Position) != SurfaceKind.Sand || map.ClimateAt(tile.Position) != ClimateZone.Dry ||
                    map.HydrologyAt(tile.Position) != WaterKind.Land || map.ElevationAt(tile.Position) >= WorldMap.MountainElevationThreshold)))
            return MapValidationResult.Invalid("Cacti must stand on desert sand.");

        if (!map.Resources.Any(resource => resource.IsRenewable &&
                string.Equals(resource.Kind, "food", StringComparison.Ordinal)) ||
            !map.Resources.Any(resource => string.Equals(resource.Kind, "construction", StringComparison.Ordinal)))
        {
            return MapValidationResult.Invalid("Reachable food or construction resources are missing.");
        }

        var startingPoint = founder?.Position ??
            map.MapObjects.FirstOrDefault(item => item.Id == "storage")?.Position ??
            map.Resources.FirstOrDefault(item => item.Id == "berry-patch")?.Position;
        if (startingPoint is null)
            return MapValidationResult.Invalid("The starting area has no reachable food resource.");
        var reachable = StartingSiteTraversals.GetValue(map, static _ => new()).Get(map, startingPoint.Value);
        var starterResources = allowEmptyStartingSite
            ? map.Resources.Where(resource => resource.Id is "berry-patch" or "timber-tree" or "grain-seed-patch" or "wild-greens-patch" or "medicinal-herb-patch")
            : map.Resources;
        if (map.MapObjects.Any(mapObject => !reachable.Contains(mapObject.Position)) ||
            starterResources.Any(resource => !reachable.Contains(resource.Position)))
        {
            return MapValidationResult.Invalid("A required starting-site route crosses an impassable boundary.");
        }

        if (!string.Equals(MapManifestCodec.Digest(map), map.ManifestDigest, StringComparison.Ordinal))
        {
            return MapValidationResult.Invalid("The manifest digest does not match the selected map.");
        }

        return MapValidationResult.Valid;
    }

    internal static bool IsValidResourcePosition(WorldMap map, MapResource resource) =>
        resource.NaturalObjectKind is "stone_outcrop" or "iron_outcrop" or "gold_outcrop" or "diamond_outcrop"
            ? map.IsPassable(resource.Position)
            : map.IsBuildable(resource.Position);

    private static bool IsValidNaturalObjectKind(string resourceKind, string? treeKind, string naturalObjectKind) =>
        treeKind is null && naturalObjectKind switch
        {
            "berry_bush" or "wild_greens" => resourceKind == "food",
            "fiber_plant" or "reeds" => resourceKind == "fiber",
            "stone_outcrop" => resourceKind == "stone",
            "fallen_wood" => resourceKind == "wood",
            "iron_outcrop" => resourceKind == "iron_ore",
            "gold_outcrop" => resourceKind == "gold_ore",
            "diamond_outcrop" => resourceKind == "diamond",
            "clay_bank" => resourceKind == "clay",
            "wild_seed_patch" => resourceKind is "grain_seed" or "cultivated_green_seed",
            "medicinal_herb_patch" => resourceKind == "medicinal_herbs",
            "fertile_soil" => resourceKind == "fertile_land",
            _ => false,
        };

    internal static HashSet<GridPoint> ReachableFrom(WorldMap map, GridPoint origin)
    {
        var visited = new HashSet<GridPoint> { origin };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(origin);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in map.FootNeighbors(current))
            {
                if (map.IsPassable(next) && visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return visited;
    }

}
