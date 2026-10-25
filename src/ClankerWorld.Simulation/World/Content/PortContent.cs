using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

/// <summary>Approved Port geometry and provisional construction budgets.</summary>
public static class PortContent
{
    public const string PackageId = "clankerworld-ports-v1";
    public const string PortTag = "port";
    public static IReadOnlyList<ContentQuantity> BoatCosts { get; } =
        Array.AsReadOnly(new ContentQuantity[] { new("wood", 8), new("rope", 2), new("iron", 2) });

    private static string Digest => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes("clankerworld-ports-v1:1.0.0:four-rotations-six-docks-shared-projects")));

    public static IReadOnlyList<BuildingDefinition> Definitions { get; } = Array.AsReadOnly(
        new[] { "north", "east", "south", "west" }.Select(direction => new BuildingDefinition(Digest,
            "port-" + direction, ContentVersion.Parse("1.0.0"), "Port (" + direction + ")",
            direction is "east" or "west" ? 4 : 2, direction is "east" or "west" ? 2 : 4,
            1, [new("wood", 16), new("stone", 4)], [PortTag, "port-facing-" + direction])).ToArray());

    public static ContentPackageManifest Create()
    {
        var version = ContentVersion.Parse("1.0.0");
        return StarterContent.BuildManifest(PackageId, version, Digest, Definitions, [],
            [new ContentDependency(TownHallContent.PackageId,
                new ContentVersionRange(version, ContentVersion.Parse("2.0.0")))]);
    }
}

public enum PortFacing { North, East, South, West }

public sealed record PortGeometry(
    GridPoint WorkPosition,
    IReadOnlyList<GridPoint> LandTiles,
    IReadOnlyList<GridPoint> WaterTiles,
    IReadOnlyList<GridPoint> DockingTiles,
    IReadOnlyList<GridPoint> ApproachTiles);

/// <summary>One land row and three water rows, with a clear water strip on both long sides.</summary>
public static class PortNavigationRules
{
    public static bool IsPort(BuildingDefinition definition) => definition.Tags.Contains("port", StringComparer.Ordinal);

    public static IEnumerable<GridPoint> ProtectedBuildingTiles(WorldMap map, BuildingDefinition definition, PlacedBuilding building) =>
        WorldContentSimulationRules.Footprint(definition, building)
            .Concat(IsPort(definition) ? Geometry(map, definition, building.Position).DockingTiles : []);

    public static PortFacing Facing(BuildingDefinition definition) => definition.Tags.FirstOrDefault(tag => tag.StartsWith("port-facing-", StringComparison.Ordinal)) switch
    {
        "port-facing-north" => PortFacing.North,
        "port-facing-east" => PortFacing.East,
        "port-facing-south" => PortFacing.South,
        "port-facing-west" => PortFacing.West,
        _ => throw new InvalidDataException("The Port must save one of its four cardinal orientations."),
    };

    public static PortGeometry Geometry(WorldMap? map, BuildingDefinition definition, GridPoint position)
    {
        var facing = Facing(definition);
        var horizontal = facing is PortFacing.East or PortFacing.West;
        if (definition.Width != (horizontal ? 4 : 2) || definition.Height != (horizontal ? 2 : 4))
            throw new InvalidDataException("A Port occupies two tiles across and four along its saved direction.");
        GridPoint Point(int across, int depth)
        {
            GridPoint point = facing switch
            {
                PortFacing.North => new(position.X + across, position.Y + 3 - depth),
                PortFacing.East => new(position.X + depth, position.Y + across),
                PortFacing.South => new(position.X + across, position.Y + depth),
                PortFacing.West => new(position.X + 3 - depth, position.Y + across),
                _ => throw new InvalidDataException("The Port direction is invalid."),
            };
            return map?.WrapColumn(point) ?? point;
        }
        var land = new[] { Point(0, 0), Point(1, 0) };
        var water = Enumerable.Range(1, 3).SelectMany(depth => new[] { Point(0, depth), Point(1, depth) }).ToArray();
        var docking = Enumerable.Range(1, 3).SelectMany(depth => new[] { Point(-1, depth), Point(2, depth) }).ToArray();
        return new(land[0], land, water, docking, [Point(0, -1), Point(1, -1)]);
    }

    public static bool NavigableWater(WorldMap map, GridPoint point) => map.Contains(point) && !map.IsLand(point);

    public static bool Fits(WorldMap map, BuildingDefinition definition, GridPoint position,
        IReadOnlySet<GridPoint> occupied, out string? failure, IReadOnlySet<GridPoint>? approachRoads = null)
    {
        if (position.X < 0 || position.Y < 0 || position.X + definition.Width > map.Width || position.Y + definition.Height > map.Height)
        {
            failure = "The Port footprint must fit on the map.";
            return false;
        }
        var geometry = Geometry(map, definition, position);
        if (!Geometry(null, definition, position).ApproachTiles.Any(map.Contains))
            failure = "The Port's land approach must fit on the map.";
        else if (geometry.LandTiles.Any(tile => !map.IsBuildable(tile) || occupied.Contains(tile)))
            failure = "The Port needs a clear two-tile land end.";
        else if (geometry.WaterTiles.Any(tile => !NavigableWater(map, tile) || occupied.Contains(tile)))
            failure = "Three rows of the Port must extend into clear navigable water.";
        else if (geometry.DockingTiles.Any(tile => !NavigableWater(map, tile) || occupied.Contains(tile)))
            failure = "Keep one clear docking-water tile along both long sides of the Port.";
        else if (!geometry.ApproachTiles.Any(tile => map.IsBuildable(tile) &&
                     (!occupied.Contains(tile) || approachRoads?.Contains(tile) == true)))
            failure = "The Port needs a clear land approach behind its land end.";
        else failure = null;
        return failure is null;
    }

    /// <summary>Cardinal connected water travel, including the optional east/west seam.</summary>
    public static IReadOnlyList<GridPoint> WaterRoute(WorldMap map, GridPoint origin,
        IEnumerable<GridPoint> destinations, IReadOnlySet<GridPoint> blocked)
    {
        var targets = destinations.Where(point => NavigableWater(map, point) && !blocked.Contains(point)).ToHashSet();
        if (!NavigableWater(map, origin) || targets.Count == 0 || blocked.Contains(origin)) return [];
        var predecessor = new Dictionary<GridPoint, GridPoint>();
        var visited = new HashSet<GridPoint> { origin };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(origin);
        while (queue.TryDequeue(out var point))
        {
            if (targets.Contains(point))
            {
                List<GridPoint> path = [point];
                while (predecessor.TryGetValue(point, out var previous)) { path.Add(previous); point = previous; }
                path.Reverse();
                return path;
            }
            foreach (var offset in new[] { new GridPoint(0, -1), new(1, 0), new(0, 1), new(-1, 0) })
            {
                var next = map.WrapColumn(new(point.X + offset.X, point.Y + offset.Y));
                if (!NavigableWater(map, next) || blocked.Contains(next) || !visited.Add(next)) continue;
                predecessor[next] = point;
                queue.Enqueue(next);
            }
        }
        return [];
    }
}

public sealed record BoatTripRequest(string Id, long Sequence, string PassengerId, string BoatTownId,
    string OriginPortId, string DestinationPortId, long RequestedTick, string Status = "waiting",
    string? BoatId = null, long? SettledTick = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OrderInstructionId { get; init; }
}

public sealed record BoatJourney(string RequestId, string PassengerId, string OriginPortId,
    string DestinationPortId, GridPoint ReservedDock, IReadOnlyList<GridPoint> WaterPath,
    int PathIndex, long StartedTick, long NextMoveTick, long? WaitingSinceTick = null,
    bool Returning = false);

/// <summary>One physical communal asset produced by one paid Council-approved project.</summary>
public sealed record BoatState(string Id, string TownId, string ProjectId, GridPoint Position,
    string? DockedPortId = null, BoatJourney? Journey = null,
    IReadOnlyList<string>? GroundCargoLotIds = null);

public sealed record RetiredBoatRequestRange(long FirstSequence, long LastSequence);

public sealed record BoatTransportState(long Sequence, IReadOnlyList<BoatState> Boats,
    IReadOnlyList<BoatTripRequest> Requests)
{
    [JsonRequired]
    public IReadOnlyList<RetiredBoatRequestRange> RetiredRequestRanges { get; init; } = [];

    public static BoatTransportState Empty() => new(0, [], []);
}
