namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GroundOccupancy Ground => new(map, worldContent, worldSimulation,
        fields, roadTiles, bridges, towns, animalWorld.Animals);
}
