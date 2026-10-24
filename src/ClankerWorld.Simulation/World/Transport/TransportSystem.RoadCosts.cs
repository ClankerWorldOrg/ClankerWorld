
namespace ClankerWorld.Simulation.World;

internal sealed partial class TransportSystem
{
    internal static bool IsRoadSurface(TransportSystem state, GridPoint point) => state.Roads.Contains(point) || state.RoadDecks.Contains(point);
    internal static int RoadStepCost(WorldMap map, TransportSystem state, GridPoint from, GridPoint to) => RoadAdjustedStepCost(state, from, to, map.FootStepCost(from, to));
    internal static int RoadAdjustedStepCost(TransportSystem state, GridPoint from, GridPoint to, int cost) => RoadRoutePlanner.AdjustFootCost(cost, IsRoadSurface(state, from) && IsRoadSurface(state, to));
}
