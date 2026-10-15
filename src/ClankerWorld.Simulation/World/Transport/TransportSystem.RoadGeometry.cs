using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static int RoadHeading(WorldMap map, GridPoint from, GridPoint to,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        foreach (var crossing in existing.Concat(pending))
        {
            var fromA = crossing.EntranceA == from && crossing.EntranceB == to;
            var fromB = crossing.EntranceB == from && crossing.EntranceA == to;
            if (!fromA && !fromB) continue;
            // A to B is east or south. The shortest wrapped delta can point
            // backwards across the same bridge on a narrow map.
            var direction = crossing.Axis == BridgeAxis.EastWest ? 0 : 2;
            return fromA ? direction : TownStreets.Turn(direction, 4);
        }
        return TownStreets.DirectionBetween(map, from, to);
    }

    internal static IEnumerable<GridPoint> LinkedRoads(WorldMap map, IReadOnlySet<GridPoint> roads, GridPoint tile,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        foreach (var next in TownStreets.Linked(map, roads, tile)) yield return next;
        foreach (var crossing in existing.Concat(pending))
        {
            if (crossing.EntranceA == tile && roads.Contains(crossing.EntranceB)) yield return crossing.EntranceB;
            else if (crossing.EntranceB == tile && roads.Contains(crossing.EntranceA)) yield return crossing.EntranceA;
        }
    }

    internal static int? StepsToDoor(WorldMap map, IReadOnlySet<GridPoint> roads, GridPoint start, HashSet<GridPoint> doors,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending)
    {
        var steps = new Dictionary<GridPoint, int> { [start] = 0 };
        var queue = new Queue<GridPoint>([start]);
        while (queue.TryDequeue(out var current))
        {
            if (doors.Contains(current)) return steps[current];
            if (steps[current] >= TownStreets.RunOnTiles) continue;
            foreach (var next in LinkedRoads(map, roads, current, existing, pending))
                if (steps.TryAdd(next, steps[current] + 1)) queue.Enqueue(next);
        }
        return null;
    }
}
