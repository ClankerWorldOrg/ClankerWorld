using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<AnimalSystem.AnimalSupplyChoice> AnimalSupplyChoices(string actor, bool ignoreActiveTrip = false) => animalSystem.AnimalSupplyChoices(animalWorldAdapter, actor, ignoreActiveTrip);
    private bool MaySupplyAnimalYard(string actor, PlacedBuilding yard) => animalSystem.MaySupplyAnimalYard(animalWorldAdapter, actor, yard);
    private IEnumerable<AnimalSystem.AnimalSupplyChoice> AnimalSupplyChoicesAtYard(string actor, string household, PlacedBuilding yard) => animalSystem.AnimalSupplyChoicesAtYard(animalWorldAdapter, actor, household, yard);
    private void AddAnimalSupplyCandidates(List<CognitionCandidate> candidates, string actor) => animalSystem.AddAnimalSupplyCandidates(animalWorldAdapter, candidates, actor);
    private bool ApplyAnimalSupplyCandidate(string actor, string id) => animalSystem.ApplyAnimalSupplyCandidate(animalWorldAdapter, actor, id);
    private void FinishAnimalSupplyTrip(string actor) => animalSystem.FinishAnimalSupplyTrip(actor);
}
