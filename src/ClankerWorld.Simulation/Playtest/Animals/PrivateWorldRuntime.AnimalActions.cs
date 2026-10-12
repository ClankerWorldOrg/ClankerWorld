using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private IEnumerable<AnimalSystem.AnimalChoice> AnimalChoices(string actor) => animalSystem.AnimalChoices(animalWorldAdapter, actor);
    private bool MayLeadAnimal(string actor, AnimalState animal) => animalSystem.MayLeadAnimal(animalWorldAdapter, actor, animal);
    private void AddAnimalCandidates(List<CognitionCandidate> candidates, string actor) => animalSystem.AddAnimalCandidates(animalWorldAdapter, candidates, actor);
    private bool ApplyAnimalCandidate(string actor, string candidateId) => animalSystem.ApplyAnimalCandidate(animalWorldAdapter, actor, candidateId);
    private IEnumerable<InventoryLot> AnimalSuppliesAtHand(string actor, AnimalState animal) => AnimalSystem.AnimalSuppliesAtHand(animalWorldAdapter, actor, animal);
    private List<(InventoryLot Lot, int Quantity)>? AnimalCareInputs(string actor, AnimalState animal, int feed, int water) => AnimalSystem.AnimalCareInputs(animalWorldAdapter, actor, animal, feed, water);
    private bool AnimalFeedMayBeSpent(string actor, AnimalState animal, InventoryLot lot, int quantity) => AnimalSystem.AnimalFeedMayBeSpent(animalWorldAdapter, actor, animal, lot, quantity);
    private bool AnimalFoodReserveRemaining(string actor, int quantity, string? stockHousehold = null) => AnimalSystem.AnimalFoodReserveRemaining(animalWorldAdapter, actor, quantity, stockHousehold);
    private void ConsumeAnimalInputs(string actor, string purpose, List<(InventoryLot Lot, int Quantity)> inputs) => AnimalSystem.ConsumeAnimalInputs(animalWorldAdapter, actor, purpose, inputs);
    private InventoryLot? CarriedMilk(string actor) => AnimalSystem.CarriedMilk(animalWorldAdapter, actor);
    private InventoryLot? WildAnimalHide(AnimalState animal) => AnimalSystem.WildAnimalHide(animalWorldAdapter, animal);
    private void CollectAnimalProduct(string actor, AnimalState animal) => animalSystem.CollectAnimalProduct(animalWorldAdapter, actor, animal);
    private void LeadAnimalHome(string actor, AnimalState animal) => animalSystem.LeadAnimalHome(animalWorldAdapter, actor, animal);
}
