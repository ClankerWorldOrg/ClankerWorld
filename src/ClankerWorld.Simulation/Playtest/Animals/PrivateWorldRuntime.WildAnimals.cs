using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private AnimalState AdvanceWildAnimalCare(AnimalState animal, long tick) => animalSystem.AdvanceWildAnimalCare(animalWorldAdapter, animal, tick);
    internal static IOrderedEnumerable<MapResource> WildAnimalForageSources(SeededMap map, EcologyState ecology, AnimalState animal) => AnimalSystem.WildAnimalForageSources(map, ecology, animal);
    private AnimalState MoveWildAnimalToward(AnimalState animal, GridPoint destination, long tick, int range) => animalSystem.MoveWildAnimalToward(animalWorldAdapter, animal, destination, tick, range);
    private HashSet<GridPoint> WildAnimalRouteOccupants(AnimalState animal) => AnimalSystem.WildAnimalRouteOccupants(animalWorldAdapter, animal);
}
