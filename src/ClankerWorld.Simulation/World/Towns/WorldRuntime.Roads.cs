namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{

    private int RoadStepCost(GridPoint from, GridPoint to) =>
        RoadRoutePlanner.AdjustFootCost(map.FootStepCost(from, to), TransportSystem.IsRoadSurface(this.transportSystem, from) && TransportSystem.IsRoadSurface(this.transportSystem, to));

    // Route searches validate walking edges immediately before costing them;
    // movement and other callers keep the validating entry point.
    private int LegalRoadStepCost(GridPoint from, GridPoint to) =>
        RoadAdjustedStepCost(from, to, map.LegalFootStepCost(from, to));

    private int RoadAdjustedStepCost(GridPoint from, GridPoint to, int cost) => TransportSystem.RoadAdjustedStepCost(transportSystem, from, to, cost);

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

    private void SetBuildingEntrance(string instanceId, GridPoint entrance)
    {
        constructionSystem.Buildings.RecordEntrance(instanceId, entrance);
    }
}
