namespace ClankerWorld.Simulation.World;

/// <summary>
/// Eight-direction A* with deterministic cardinal/diagonal costs and queue ordering.
/// </summary>
public static class DeterministicRouteFinder
{
    public static IReadOnlyList<GridPoint> Find(WorldMap map, GridPoint origin, GridPoint destination)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsPassable(origin) || !map.IsPassable(destination))
        {
            throw new InvalidOperationException("Routing endpoints must be passable.");
        }

        if (TryFind(map, origin, destination, out var route)) return route;
        throw new InvalidOperationException("No passable route exists between the requested points.");
    }

    public static bool TryFind(WorldMap map, GridPoint origin, GridPoint destination,
        out IReadOnlyList<GridPoint> route)
    {
        ArgumentNullException.ThrowIfNull(map);
        route = [];
        if (!map.IsPassable(origin) || !map.IsPassable(destination)) return false;

        var open = new PriorityQueue<RouteNode, RoutePriority>();
        var predecessor = new Dictionary<GridPoint, GridPoint>();
        var best = new Dictionary<GridPoint, RouteRecord>();
        var nodeId = 0;
        var start = new RouteNode(origin, origin, 0, nodeId++);
        open.Enqueue(start, ToPriority(map, start, destination));
        best.Add(origin, new RouteRecord(0, origin));

        while (open.TryDequeue(out var node, out _))
        {
            var record = best[node.Position];
            if (node.G != record.Cost || node.Predecessor != record.Predecessor)
            {
                continue;
            }

            if (node.Position == destination)
            {
                route = Reconstruct(predecessor, origin, destination);
                return true;
            }

            foreach (var next in map.FootNeighbors(node.Position))
            {
                if (!map.IsPassable(next))
                {
                    continue;
                }

                var candidate = new RouteRecord(checked(node.G + map.FootStepCost(node.Position, next)), node.Position);
                if (best.TryGetValue(next, out var old) && Compare(candidate, old) >= 0)
                {
                    continue;
                }

                best[next] = candidate;
                predecessor[next] = node.Position;
                var nextNode = new RouteNode(next, node.Position, candidate.Cost, nodeId++);
                open.Enqueue(nextNode, ToPriority(map, nextNode, destination));
            }
        }

        return false;
    }

    private static int Compare(RouteRecord left, RouteRecord right)
    {
        var result = left.Cost.CompareTo(right.Cost);
        if (result != 0)
        {
            return result;
        }

        result = left.Predecessor.Y.CompareTo(right.Predecessor.Y);
        return result != 0 ? result : left.Predecessor.X.CompareTo(right.Predecessor.X);
    }

    private static List<GridPoint> Reconstruct(
        Dictionary<GridPoint, GridPoint> predecessor,
        GridPoint origin,
        GridPoint destination)
    {
        var route = new List<GridPoint> { destination };
        var current = destination;
        while (current != origin)
        {
            current = predecessor[current];
            route.Add(current);
        }

        route.Reverse();
        return route;
    }

    private static RoutePriority ToPriority(WorldMap map, RouteNode node, GridPoint destination)
    {
        var heuristic = map.FootRouteHeuristicCost(node.Position, destination);
        return new RoutePriority(
            checked(node.G + heuristic),
            heuristic,
            node.G,
            node.Position.Y,
            node.Position.X,
            node.Predecessor.Y,
            node.Predecessor.X,
            node.NodeId);
    }

    private sealed record RouteNode(GridPoint Position, GridPoint Predecessor, int G, int NodeId);

    private readonly record struct RouteRecord(int Cost, GridPoint Predecessor);

    private readonly record struct RoutePriority(
        int F,
        int H,
        int G,
        int Y,
        int X,
        int PredecessorY,
        int PredecessorX,
        int NodeId) : IComparable<RoutePriority>
    {
        public int CompareTo(RoutePriority other)
        {
            var result = F.CompareTo(other.F);
            result = result != 0 ? result : H.CompareTo(other.H);
            result = result != 0 ? result : G.CompareTo(other.G);
            result = result != 0 ? result : Y.CompareTo(other.Y);
            result = result != 0 ? result : X.CompareTo(other.X);
            result = result != 0 ? result : PredecessorY.CompareTo(other.PredecessorY);
            result = result != 0 ? result : PredecessorX.CompareTo(other.PredecessorX);
            return result != 0 ? result : NodeId.CompareTo(other.NodeId);
        }
    }
}
