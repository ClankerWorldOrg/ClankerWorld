using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GridPoint[] BuildingRoadEntrances(PlacedBuilding building, HashSet<GridPoint> occupied) =>
        TransportSystem.BuildingRoadEntrances(RoadWorld, building, occupied);

    private void GenerateRoadBetweenTowns(TownRuntimeState town, PlacedBuilding firstBuilding) =>
        TransportSystem.GenerateRoadBetweenTowns(RoadWorld, town, firstBuilding);
}
