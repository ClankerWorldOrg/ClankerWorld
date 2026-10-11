namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private GroundOccupancy Ground => new(map, worldContent, worldSimulation,
        fields, roadTiles, bridges, towns, animalWorld.Animals);
}
