using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void GenerateRoadBetweenTowns(TownRuntimeState town, PlacedBuilding firstBuilding) =>
        TransportSystem.GenerateRoadBetweenTowns(RoadWorld, town, firstBuilding);
}
