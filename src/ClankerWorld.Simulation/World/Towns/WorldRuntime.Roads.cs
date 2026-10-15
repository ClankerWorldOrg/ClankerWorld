using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void RepairSavedRoadFootprints() =>
        TransportSystem.RepairSavedRoadFootprints(RoadWorld);

    private int RoadStepCost(GridPoint from, GridPoint to) =>
        RoadAdjustedStepCost(from, to, map.FootStepCost(from, to));

    // Route searches obtain legal edges from FootNeighbors immediately before
    // costing them; movement and other callers keep the validating entry point.
    private int LegalRoadStepCost(GridPoint from, GridPoint to) =>
        RoadAdjustedStepCost(from, to, map.LegalFootStepCost(from, to));

    private int RoadAdjustedStepCost(GridPoint from, GridPoint to, int cost) => TransportSystem.RoadAdjustedStepCost(transportSystem, from, to, cost);
    private bool IsRoadSurface(GridPoint point) => TransportSystem.IsRoadSurface(transportSystem, point);

    /// <summary>
    /// Joins a building that has joined a Town to the Road network and returns
    /// the new Road tiles. A building beside a Road faces it and needs no new
    /// Road; otherwise a new street runs from its best entrance to the nearest
    /// Road, inside the Town border where it can, crossing a river up to two
    /// tiles wide on a new bridge where its route needs one. Either way,
    /// streets near its door then run on past it
    /// (<see cref="ExtendStreetsPastDoors"/>). Each proposal is validated whole
    /// before any Road tile or bridge is saved.
    /// </summary>
    private List<GridPoint> GenerateRoadToBuilding(PlacedBuilding building, HashSet<GridPoint>? border = null) =>
        TransportSystem.GenerateRoadToBuilding(RoadWorld, building, border);

    /// <summary>
    /// Streets run on about three tiles past the last door on them, leaving free
    /// frontage for the next building. Each dead end fewer tiles than that past
    /// its nearest door carries on in its own direction where the land allows,
    /// keeping clear of other streets. A street heading straight at a river up
    /// to two tiles wide may cross it on a new bridge. Nothing is saved here:
    /// the caller validates and commits the returned tiles and crossings.
    /// </summary>
    private (List<GridPoint> Tiles, List<RiverCrossing> Crossings) ExtendStreetsPastDoors(PlacedBuilding building,
        HashSet<GridPoint> occupied) =>
        TransportSystem.ExtendStreetsPastDoors(RoadWorld, building, occupied);

    private static int RoadHeading(WorldMap map, GridPoint from, GridPoint to,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending) =>
        TransportSystem.RoadHeading(map, from, to, existing, pending);

    /// <summary>
    /// A new bridge for a street running on straight at a river: a legal
    /// crossing of one or two river tiles whose far bank is clear, that shares
    /// no water with another new crossing and joins banks no bridge joins yet.
    /// </summary>
    private RiverCrossing? RunOnCrossing(GridPoint from, int direction, HashSet<GridPoint> occupied,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending) =>
        TransportSystem.RunOnCrossing(RoadWorld, from, direction, occupied, existing, pending);

    /// <summary>
    /// Road tiles joined to a tile: its street neighbours, plus the far end of
    /// any bridge that has Road at both ends, so a street that crosses a river
    /// is not mistaken for two dead ends.
    /// </summary>
    private static IEnumerable<GridPoint> LinkedRoads(WorldMap map, IReadOnlySet<GridPoint> roads, GridPoint tile,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending) =>
        TransportSystem.LinkedRoads(map, roads, tile, existing, pending);

    /// <summary>Road steps from a tile to the nearest door, looking no further than a street's run-on.</summary>
    private static int? StepsToDoor(WorldMap map, IReadOnlySet<GridPoint> roads, GridPoint start, HashSet<GridPoint> doors,
        IReadOnlyList<RiverCrossing> existing, IReadOnlyList<RiverCrossing> pending) =>
        TransportSystem.StepsToDoor(map, roads, start, doors, existing, pending);

    private void SetBuildingEntrance(string instanceId, GridPoint entrance)
    {
        constructionSystem.Buildings.RecordEntrance(instanceId, entrance);
    }



    private static void ValidateRoads(IReadOnlyList<GridPoint> roads, WorldMap map, FounderSetupState? setup) =>
        TransportSystem.ValidateRoads(roads, map, setup);
}
