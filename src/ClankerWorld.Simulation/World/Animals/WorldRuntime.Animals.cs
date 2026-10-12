using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private int AnimalDayTicks => AnimalSystem.AnimalDayTicks(animalWorldAdapter);
    private void StageAnimalContent() => AnimalSystem.StageAnimalContent(animalWorldAdapter);
    private static string AnimalKey(string value) => AnimalSystem.AnimalKey(value);
    private AnimalState? Animal(string id) => animalSystem.Animal(id);
    private void SetAnimal(AnimalState animal) => animalSystem.SetAnimal(animal);
    private PlacedBuilding? AnimalYard(string? household) => AnimalSystem.AnimalYard(animalWorldAdapter, household);
    private PlacedBuilding? AssignedAnimalYard(AnimalState animal) => AnimalSystem.AssignedAnimalYard(animalWorldAdapter, animal);
    private int AnimalYardCapacity(PlacedBuilding yard) => AnimalSystem.AnimalYardCapacity(animalWorldAdapter, yard);
    private int AnimalPlacesUsed(string household, string? yardId = null) => animalSystem.AnimalPlacesUsed(household, yardId);
    private bool HasAnimalSpace(string household, PlacedBuilding yard) => animalSystem.HasAnimalSpace(animalWorldAdapter, household, yard);
    private bool AnimalHouseholdMember(string actor, AnimalState animal) => AnimalSystem.AnimalHouseholdMember(animalWorldAdapter, actor, animal);
    private bool MayCareForAnimal(string actor, AnimalState animal) => AnimalSystem.MayCareForAnimal(animalWorldAdapter, actor, animal);
    private bool MayRideAnimal(string actor, AnimalState animal) => AnimalSystem.MayRideAnimal(animalWorldAdapter, actor, animal);
    private GridPoint[] YardTiles(PlacedBuilding yard) => AnimalSystem.YardTiles(animalWorldAdapter, yard);
    private GridPoint? FreeAnimalYardTile(PlacedBuilding yard, string? animalId = null, GridPoint? near = null) => animalSystem.FreeAnimalYardTile(animalWorldAdapter, yard, animalId, near);
    private void SeedWildAnimals() => animalSystem.SeedWildAnimals(animalWorldAdapter);
    private void AdvanceAnimals(long tick) => animalSystem.AdvanceAnimals(animalWorldAdapter, tick);
    private void ReconcileAnimalCustody() => animalSystem.ReconcileAnimalCustody(animalWorldAdapter);
    private bool CanStartAnimalPregnancy(AnimalState animal, long tick) => animalSystem.CanStartAnimalPregnancy(animalWorldAdapter, animal, tick);
    private void ClearAnimalProduct(AnimalState animal, bool discard) => AnimalSystem.ClearAnimalProduct(animalWorldAdapter, animal, discard);
    private void WanderAnimal(AnimalState animal, long tick) => animalSystem.WanderAnimal(animalWorldAdapter, animal, tick);
}
