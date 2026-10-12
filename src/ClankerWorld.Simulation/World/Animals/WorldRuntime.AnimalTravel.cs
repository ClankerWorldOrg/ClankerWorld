using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private AnimalState? RidingAnimal(string actor) => animalSystem.RidingAnimal(animalWorldAdapter, actor);
    private int HorseCargoCapacity(string actor) => animalSystem.HorseCargoCapacity(animalWorldAdapter, actor);
    private void SaddleAnimal(string actor, AnimalState animal) => animalSystem.SaddleAnimal(animalWorldAdapter, actor, animal);
    private void EndAnimalRide(AnimalState animal, string reason) => animalSystem.EndAnimalRide(animalWorldAdapter, animal, reason);
    private void MoveAnimal(AnimalState animal, GridPoint next) => animalSystem.MoveAnimal(animalWorldAdapter, animal, next);
    private void MoveAttachedAnimal(string actor, GridPoint from, GridPoint to) => animalSystem.MoveAttachedAnimal(animalWorldAdapter, actor, from, to);
}
