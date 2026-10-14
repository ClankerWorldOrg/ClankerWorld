namespace ClankerWorld.Simulation.World;

/// <summary>
/// Answers whether many origins can reach one destination without constructing
/// their routes. Foot and swimming steps are reversible, even when their costs
/// differ by direction, so one flood from the destination answers them all.
/// </summary>
internal sealed class UnoccupiedReachabilitySearch
{
    private readonly WorldMap map;
    private readonly HashSet<GridPoint> occupied;
    private readonly Func<GridPoint, IEnumerable<GridPoint>>? neighbors;
    private readonly bool[] reached;
    private readonly Queue<GridPoint> open = new();

    internal UnoccupiedReachabilitySearch(WorldMap map, HashSet<GridPoint> occupied,
        GridPoint destination, int interactionRange,
        Func<GridPoint, IEnumerable<GridPoint>>? neighbors = null)
    {
        this.map = map;
        this.occupied = occupied;
        this.neighbors = neighbors;
        Destination = destination;
        InteractionRange = interactionRange;
        reached = new bool[checked(map.Width * map.Height)];
        var top = Math.Max(0L, (long)destination.Y - interactionRange);
        var bottom = Math.Min(map.Height - 1L, (long)destination.Y + interactionRange);
        for (var y = (int)top; y <= bottom; y++)
            for (var x = 0; x < map.Width; x++)
            {
                var point = new GridPoint(x, y);
                if (map.FootDistance(point, destination) > interactionRange || occupied.Contains(point)) continue;
                reached[Index(point)] = true;
                open.Enqueue(point);
            }
    }

    internal WorldMap Map => map;
    internal IReadOnlySet<GridPoint> Occupied => occupied;
    internal GridPoint Destination { get; }
    internal int InteractionRange { get; }
    internal int ExpandedTiles { get; private set; }

    internal bool CanReachFrom(GridPoint origin)
    {
        // A route can start on an occupied or impassable tile, and a zero-step
        // interaction succeeds there too. Occupied goals are otherwise closed.
        if (map.FootDistance(origin, Destination) <= InteractionRange) return true;
        while (!reached[Index(origin)] && open.TryDequeue(out var current))
        {
            ExpandedTiles++;
            if (neighbors is null)
            {
                foreach (var next in map.FootNeighborsForSearch(current)) Visit(current, next);
            }
            else
            {
                foreach (var next in neighbors(current)) Visit(current, next);
            }
        }
        return reached[Index(origin)];
    }

    private void Visit(GridPoint current, GridPoint next)
    {
        if (reached[Index(next)] || map.IsDiagonalFootStep(current, next) &&
            (occupied.Contains(new GridPoint(next.X, current.Y)) ||
             occupied.Contains(new GridPoint(current.X, next.Y)))) return;
        reached[Index(next)] = true;
        // Starting on an occupied tile is legal; passing through it is not.
        if (!occupied.Contains(next)) open.Enqueue(next);
    }

    private int Index(GridPoint point) => point.Y * map.Width + point.X;
}
