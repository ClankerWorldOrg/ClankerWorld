using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.World;

/// <summary>
/// The selected deterministic map attempt and its canonical manifest lock.
/// </summary>
public sealed record WorldMap(
    int Width,
    int Height,
    int GenerationAttempt,
    IReadOnlyList<TerrainTile> Tiles,
    IReadOnlyList<MapObject> MapObjects,
    IReadOnlyList<MapResource> Resources,
    string ManifestDigest)
{
    public const byte MountainElevationThreshold = 215;
    public const byte PeakElevationThreshold = 245;

    /// <summary>
    /// Provisional foot cost of river water that can only be waded as part of a
    /// two-tile crossing: a third of dry-ground speed, slower than the half
    /// speed of a one-tile river. The wading speed has not been tuned yet.
    /// </summary>
    public const int TwoTileWadingFootCost = 300;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? ClimateZones { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? ElevationLevels { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? HydrologyKinds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? SurfaceKinds { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? VegetationKinds { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WrapsEastWest { get; init; }

    /// <summary>
    /// Built bridge decks over river tiles, keyed by water tile. They come from
    /// the world's saved bridges, not from generation: they are never part of
    /// the manifest, the terrain digest or the saved map JSON. A deck is walked
    /// only along its axis, at dry-ground cost, between its two banks.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<GridPoint, BridgeAxis>? BridgeDecks { get; init; }

    // Keep the index outside the record: a cache field would silently change
    // record equality and could be copied into a `with` map with new tiles.
    private static readonly ConditionalWeakTable<WorldMap, byte[]> TerrainIndexes = new();
    private static readonly ConditionalWeakTable<WorldMap, HashSet<GridPoint>> StartingSiteReachability = new();
    private static readonly ConditionalWeakTable<WorldMap, bool[]> HillMasks = new();

    public bool Contains(GridPoint point) =>
        point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

    public bool IsPassable(GridPoint point) =>
        Contains(point) && (IsOpenGroundAt(point) || IsMountainAt(point) || IsOneTileRiverCrossing(point) ||
            IsTwoTileRiverCrossing(point) || IsBridgeDeck(point));

    /// <summary>
    /// The cost of entering a tile on foot. A river tile costs what its
    /// narrowest crossing costs: half speed where one tile of water separates
    /// dry banks, and <see cref="TwoTileWadingFootCost"/> where it takes two.
    /// </summary>
    public int FootTravelCost(GridPoint point) => !IsPassable(point)
        ? throw new ArgumentOutOfRangeException(nameof(point), "The tile cannot be crossed on foot.")
        : IsBridgeDeck(point) ? 100
        : IsRiverAt(point) ? IsOneTileRiverCrossing(point) ? 200 : TwoTileWadingFootCost
        : IsMountainAt(point) ? 200 : 100;

    public bool IsBridgeDeck(GridPoint point) => BridgeDecks is { } decks && decks.ContainsKey(point);

    /// <summary>River water, as opposed to lake, ocean or land. Bridge decks are still river water.</summary>
    public bool IsRiverWater(GridPoint point) => Contains(point) && IsRiverAt(point);

    /// <summary>Wraps a column across an enabled east/west seam; rows never wrap.</summary>
    public GridPoint WrapColumn(GridPoint point) => WrapsEastWest && Width > 0
        ? new GridPoint((point.X % Width + Width) % Width, point.Y)
        : point;

    public int FootDistance(GridPoint origin, GridPoint destination)
    {
        var horizontal = Math.Abs(origin.X - destination.X);
        if (WrapsEastWest)
            horizontal = Math.Min(horizontal, Width - horizontal);
        return Math.Max(horizontal, Math.Abs(origin.Y - destination.Y));
    }

    public int FootRouteHeuristicCost(GridPoint origin, GridPoint destination)
    {
        var horizontal = Math.Abs(origin.X - destination.X);
        if (WrapsEastWest)
            horizontal = Math.Min(horizontal, Width - horizontal);
        var vertical = Math.Abs(origin.Y - destination.Y);
        var diagonal = Math.Min(horizontal, vertical);
        return checked(diagonal * 141 + (Math.Max(horizontal, vertical) - diagonal) * 100);
    }

    public bool IsDiagonalFootStep(GridPoint origin, GridPoint destination)
    {
        if (!Contains(origin) || !Contains(destination)) return false;
        var horizontal = Math.Abs(origin.X - destination.X);
        if (WrapsEastWest) horizontal = Math.Min(horizontal, Width - horizontal);
        return horizontal == 1 && Math.Abs(origin.Y - destination.Y) == 1;
    }

    public bool CanFootStep(GridPoint origin, GridPoint destination)
    {
        if (!IsPassable(origin) || !IsPassable(destination) || FootDistance(origin, destination) != 1)
            return false;
        if (BridgeDecks is { } decks)
        {
            var originIsDeck = decks.TryGetValue(origin, out var originAxis);
            var destinationIsDeck = decks.TryGetValue(destination, out var destinationAxis);
            if (originIsDeck || destinationIsDeck)
            {
                // A bridge is walked end to end along its axis: never sideways
                // into the river beside it, and never on a diagonal.
                if (IsDiagonalFootStep(origin, destination)) return false;
                var axis = origin.Y == destination.Y ? BridgeAxis.EastWest : BridgeAxis.NorthSouth;
                return (!originIsDeck || originAxis == axis) && (!destinationIsDeck || destinationAxis == axis) &&
                    (originIsDeck || IsDryBank(origin)) && (destinationIsDeck || IsDryBank(destination));
            }
        }
        var originIsRiver = IsRiverAt(origin);
        var destinationIsRiver = IsRiverAt(destination);
        if (originIsRiver || destinationIsRiver)
        {
            // A river is waded bank to bank along one straight crossing of one
            // or two river tiles, not along the channel or diagonally through
            // the water.
            if (IsDiagonalFootStep(origin, destination)) return false;
            var dx = destination.X - origin.X;
            if (WrapsEastWest)
            {
                if (dx == Width - 1) dx = -1;
                else if (dx == 1 - Width) dx = 1;
            }
            var dy = destination.Y - origin.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            // Between the two river tiles of a crossing, with dry banks
            // straight behind and ahead.
            if (originIsRiver && destinationIsRiver)
                return IsTwoTileLine(origin, dx, dy);
            // Between a bank and the water: the crossing runs on from the
            // bank through one river tile, or two, to a dry bank opposite.
            var (river, bank) = originIsRiver ? (origin, destination) : (destination, origin);
            var (outX, outY) = originIsRiver ? (dx, dy) : (-dx, -dy);
            var beyond = Step(river, -outX, -outY);
            return IsDryBank(bank) && (IsDryBank(beyond) || IsTwoTileLine(river, -outX, -outY));
        }
        if (!IsDiagonalFootStep(origin, destination)) return true;
        // Both orthogonal shoulders must be traversable. A diagonal cannot
        // squeeze around a wall, peak, deep river, or map edge.
        return IsPassable(new GridPoint(destination.X, origin.Y)) &&
            IsPassable(new GridPoint(origin.X, destination.Y));
    }

    public int FootStepCost(GridPoint origin, GridPoint destination) =>
        !CanFootStep(origin, destination)
            ? throw new ArgumentOutOfRangeException(nameof(destination), "The foot step is illegal.")
            : LegalFootStepCost(origin, destination);

    // Only for a step just returned by FootNeighbors on this same map. Public
    // callers still validate the complete step, including river and bridge axes.
    internal int LegalFootStepCost(GridPoint origin, GridPoint destination) =>
        checked(FootTravelCost(destination) * (IsDiagonalFootStep(origin, destination) ? 141 : 100) / 100);

    private static readonly (int X, int Y)[] FootNeighborOffsets =
    [
        (0, -1), (1, 0), (0, 1), (-1, 0),
        (1, -1), (1, 1), (-1, 1), (-1, -1),
    ];

    public IEnumerable<GridPoint> FootNeighbors(GridPoint point)
    {
        foreach (var next in FootNeighborsForSearch(point))
            yield return next;
    }

    // Layout searches visit many tiles. Keep their enumerator on the stack
    // rather than allocating the public IEnumerable iterator for each tile.
    internal FootNeighborEnumerator FootNeighborsForSearch(GridPoint point) => new(this, point);

    // Reachability discards visited positions before asking the canonical
    // walking or swimming rule whether the remaining edge is legal.
    internal FootNeighborEnumerator NeighborPositionsForSearch(GridPoint point) => new(this, point, footSteps: false);

    internal struct FootNeighborEnumerator
    {
        private readonly WorldMap map;
        private readonly GridPoint origin;
        private readonly bool footSteps;
        private int offset;

        internal FootNeighborEnumerator(WorldMap map, GridPoint point, bool footSteps = true)
        {
            if (!map.Contains(point))
                throw new ArgumentOutOfRangeException(nameof(point));
            this.map = map;
            origin = point;
            this.footSteps = footSteps;
            offset = 0;
            Current = default;
        }

        public readonly FootNeighborEnumerator GetEnumerator() => this;

        public GridPoint Current { get; private set; }

        public bool MoveNext()
        {
            while (offset < FootNeighborOffsets.Length)
            {
                var index = offset++;
                var next = Neighbor(index);
                // A one- or two-column wrap repeats offsets. Suppress a tile
                // even if its earlier occurrence was illegal, as before.
                if (map.WrapsEastWest && map.Width < 3)
                {
                    var repeated = false;
                    for (var previous = 0; previous < index; previous++)
                    {
                        if (Neighbor(previous) != next) continue;
                        repeated = true;
                        break;
                    }
                    if (repeated) continue;
                }
                // Walking checks the live map for each step; raw adjacency leaves
                // legality to its search. Neither caches walkability or bridge axes.
                if (footSteps ? !map.CanFootStep(origin, next) : !map.Contains(next)) continue;
                Current = next;
                return true;
            }
            return false;
        }

        private readonly GridPoint Neighbor(int index)
        {
            var (dx, dy) = FootNeighborOffsets[index];
            var x = origin.X + dx;
            if (map.WrapsEastWest) x = (x % map.Width + map.Width) % map.Width;
            return new GridPoint(x, origin.Y + dy);
        }
    }

    /// <summary>Whether a tile is dry land of any kind, including mountains.</summary>
    public bool IsLand(GridPoint point) => Contains(point) && IsLandAt(point);

    // Construction eligibility is separate from travel: future mountain
    // paths must not silently become build sites when traversal is expanded.
    public bool IsBuildable(GridPoint point) => Contains(point) && IsOpenGroundAt(point) &&
        SurfaceAt(point) is not (SurfaceKind.Rock or SurfaceKind.Water);

    public ClimateZone? ClimateAt(GridPoint point) =>
        Contains(point) && ClimateZones is { } zones && zones.Length == Width * Height
            ? (ClimateZone)zones[point.Y * Width + point.X] : null;

    public byte? ElevationAt(GridPoint point) => LayerAt(ElevationLevels, point);
    public WaterKind? HydrologyAt(GridPoint point) => LayerAt(HydrologyKinds, point) is { } value
        ? (WaterKind)value : null;
    public SurfaceKind? SurfaceAt(GridPoint point) => LayerAt(SurfaceKinds, point) is { } value
        ? (SurfaceKind)value : null;
    public VegetationCover? VegetationAt(GridPoint point) => LayerAt(VegetationKinds, point) is { } value
        ? (VegetationCover)value : null;

    /// <summary>
    /// Whether the tile lies in the hill band at a mountain's base. Hills are a
    /// visual layer: they walk and build like the ground beneath them. Maps
    /// without elevation and water layers have no hills.
    /// </summary>
    public bool IsHillAt(GridPoint point)
    {
        if (!Contains(point) || ElevationLevels?.Length != Width * Height ||
            HydrologyKinds?.Length != Width * Height) return false;
        return HillMasks.GetValue(this, static map => TerrainPlacementRules.ClassifyHills(
            map.ElevationLevels!, map.HydrologyKinds!, map.Width, map.Height, map.WrapsEastWest))[point.Y * Width + point.X];
    }

    public TerrainKind? TerrainKindAt(GridPoint point)
    {
        if (!Contains(point)) return null;
        var kind = TerrainAt(point);
        return kind == byte.MaxValue ? null : (TerrainKind)kind;
    }

    private byte? LayerAt(byte[]? layer, GridPoint point) =>
        Contains(point) && layer?.Length == Width * Height
            ? layer[point.Y * Width + point.X] : null;

    private static readonly ConditionalWeakTable<WorldMap, int[]> FootComponents = new();

    public bool IsReachableOnFoot(GridPoint origin, GridPoint destination)
    {
        if (!Contains(origin) || !Contains(destination) || !IsPassable(origin) || !IsPassable(destination)) return false;
        var components = FootComponents.GetValue(this, static map =>
        {
            var labels = new int[checked(map.Width * map.Height)];
            var component = 0;
            foreach (var tile in map.Tiles)
            {
                var index = tile.Position.Y * map.Width + tile.Position.X;
                if (labels[index] != 0 || !map.IsPassable(tile.Position)) continue;
                component++;
                foreach (var point in MapAcceptance.ReachableFrom(map, tile.Position))
                    labels[point.Y * map.Width + point.X] = component;
            }
            return labels;
        });
        return components[origin.Y * Width + origin.X] == components[destination.Y * Width + destination.X];
    }

    public bool IsReachableFromStartingSiteOnFoot(GridPoint point) => Contains(point) &&
        StartingSiteReachability.GetValue(this, static map =>
            MapAcceptance.ReachableFrom(map,
                map.MapObjects.FirstOrDefault(item => item.Id == "storage")?.Position ??
                map.Resources.First(item => item.Id == "berry-patch").Position)).Contains(point);

    private static bool IsOpenGround(byte kind) => kind is
        (byte)TerrainKind.Meadow or (byte)TerrainKind.Sand or
        (byte)TerrainKind.Forest or (byte)TerrainKind.Snow;

    private bool IsOpenGroundAt(GridPoint point)
    {
        if (!IsLandAt(point) || IsMountainAt(point) || IsPeakAt(point)) return false;
        return HydrologyAt(point) is not null || ElevationAt(point) is not null ||
            SurfaceAt(point) is not null || IsOpenGround(TerrainAt(point));
    }

    private bool IsLandAt(GridPoint point) => HydrologyAt(point) is { } water
        ? water == WaterKind.Land
        : SurfaceAt(point) is SurfaceKind.Water ? false
        : TerrainAt(point) switch
        {
            (byte)TerrainKind.Water or (byte)TerrainKind.River or
                (byte)TerrainKind.Lake or (byte)TerrainKind.Ocean => false,
            _ => true,
        };

    private bool IsRiverAt(GridPoint point) => HydrologyAt(point) is { } water
        ? water == WaterKind.River
        : TerrainAt(point) == (byte)TerrainKind.River;

    private bool IsMountainAt(GridPoint point)
    {
        if (!IsLandAt(point)) return false;
        return ElevationAt(point) is { } elevation
            ? elevation >= MountainElevationThreshold && elevation < PeakElevationThreshold
            : TerrainAt(point) == (byte)TerrainKind.Mountain;
    }

    private bool IsPeakAt(GridPoint point) => ElevationAt(point) is { } elevation
        ? elevation >= PeakElevationThreshold
        : TerrainAt(point) == (byte)TerrainKind.Peak;

    private bool IsOneTileRiverCrossing(GridPoint point)
    {
        if (!IsRiverAt(point))
            return false;
        var west = new GridPoint(point.X - 1, point.Y);
        var east = new GridPoint(point.X + 1, point.Y);
        var north = new GridPoint(point.X, point.Y - 1);
        var south = new GridPoint(point.X, point.Y + 1);
        return IsDryBank(west) && IsDryBank(east) ||
            IsDryBank(north) && IsDryBank(south);
    }

    /// <summary>
    /// River water that is either tile of a straight two-tile crossing: dry
    /// bank, two river tiles, dry bank, east-west or north-south. A third
    /// water tile, a lake or the sea in that line means it is not one.
    /// Bridge decks do not change this: they decide only which steps a deck allows.
    /// </summary>
    private bool IsTwoTileRiverCrossing(GridPoint point) =>
        IsRiverAt(point) &&
        (IsTwoTileLine(point, 1, 0) || IsTwoTileLine(Step(point, -1, 0), 1, 0) ||
            IsTwoTileLine(point, 0, 1) || IsTwoTileLine(Step(point, 0, -1), 0, 1));

    // A tile that also lies on a two-tile line across the other axis is a
    // corner of a river one tile thick that runs diagonally. Wading through it
    // would turn inside the water and walk along the channel, so neither line
    // through such a tile counts as a crossing.
    private bool IsTwoTileLine(GridPoint first, int dx, int dy)
    {
        var second = Step(first, dx, dy);
        return IsBankToBankPair(first, dx, dy) &&
            !HasBankToBankPair(first, dy, dx) && !HasBankToBankPair(second, dy, dx);
    }

    private bool HasBankToBankPair(GridPoint point, int dx, int dy) =>
        IsBankToBankPair(point, dx, dy) || IsBankToBankPair(Step(point, -dx, -dy), dx, dy);

    private bool IsBankToBankPair(GridPoint first, int dx, int dy)
    {
        var second = Step(first, dx, dy);
        return IsRiverWater(first) && IsRiverWater(second) &&
            IsDryBank(Step(first, -dx, -dy)) && IsDryBank(Step(second, dx, dy));
    }

    private GridPoint Step(GridPoint point, int dx, int dy) =>
        WrapColumn(new GridPoint(point.X + dx, point.Y + dy));

    private bool IsDryBank(GridPoint point)
    {
        if (WrapsEastWest && point.Y >= 0 && point.Y < Height)
            point = new GridPoint((point.X % Width + Width) % Width, point.Y);
        if (!Contains(point) || !IsLandAt(point) || IsPeakAt(point)) return false;
        return ElevationAt(point) is not null || SurfaceAt(point) is not null ||
            IsOpenGround(TerrainAt(point)) || IsMountainAt(point);
    }

    private byte TerrainAt(GridPoint point) =>
        TerrainIndexes.GetValue(this, static map =>
        {
            var indexed = new byte[checked(map.Width * map.Height)];
            Array.Fill(indexed, byte.MaxValue);
            foreach (var tile in map.Tiles)
                if (map.Contains(tile.Position))
                    indexed[tile.Position.Y * map.Width + tile.Position.X] = checked((byte)tile.Terrain);
            return indexed;
        })[point.Y * Width + point.X];

    public MapObject GetObject(string id) =>
        MapObjects.Single(mapObject => string.Equals(mapObject.Id, id, StringComparison.Ordinal));

    public MapResource GetResource(string id) =>
        Resources.Single(resource => string.Equals(resource.Id, id, StringComparison.Ordinal));
}
