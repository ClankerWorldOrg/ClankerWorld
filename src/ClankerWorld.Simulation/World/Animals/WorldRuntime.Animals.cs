using ClankerWorld.Simulation.Cognition;
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
    private int AnimalYardCapacity(PlacedBuilding yard) => AnimalSystem.AnimalYardCapacity(animalWorldAdapter, yard);
    private bool HasAnimalSpace(string household, PlacedBuilding yard) => animalSystem.HasAnimalSpace(animalWorldAdapter, household, yard);
    private GridPoint? FreeAnimalYardTile(PlacedBuilding yard, string? animalId = null, GridPoint? near = null) => animalSystem.FreeAnimalYardTile(animalWorldAdapter, yard, animalId, near);
    private void SeedWildAnimals() => animalSystem.SeedWildAnimals(animalWorldAdapter);
    private void AdvanceAnimals(long tick) => animalSystem.AdvanceAnimals(animalWorldAdapter, tick);
    private void ReconcileAnimalCustody() => animalSystem.ReconcileAnimalCustody(animalWorldAdapter);
    private IEnumerable<AnimalSystem.AnimalChoice> AnimalChoices(string actor) => animalSystem.AnimalChoices(animalWorldAdapter, actor);
    private bool MayLeadAnimal(string actor, AnimalState animal) => animalSystem.MayLeadAnimal(animalWorldAdapter, actor, animal);
    private void AddAnimalCandidates(List<CognitionCandidate> candidates, string actor) => animalSystem.AddAnimalCandidates(animalWorldAdapter, candidates, actor);
    private bool ApplyAnimalCandidate(string actor, string candidateId, ActionPlan? plan = null) => animalSystem.ApplyAnimalCandidate(animalWorldAdapter, actor, candidateId, plan);
    private void AddMilkCandidate(List<CognitionCandidate> candidates, string actor) => animalSystem.AddMilkCandidate(animalWorldAdapter, candidates, actor);
    private List<(InventoryLot Lot, int Quantity)>? AnimalCareInputs(string actor, AnimalState animal, int feed, int water) => AnimalSystem.AnimalCareInputs(animalWorldAdapter, actor, animal, feed, water);
    private bool ApplyAnimalConsentDecision(SocietyCognitionDispatchResult decision)
    {
        if (!animalSystem.TryPlanAnimalConsentDecision(animalWorldAdapter, decision, out var plan)) return false;
        if (plan is not null)
            TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
        return true;
    }
    private static bool IsAnimalOrder(string action) => AnimalSystem.IsAnimalOrder(action);
    private OrderTask? ParseAnimalOrder(string text, string actor) => animalSystem.ParseAnimalOrder(animalWorldAdapter, text, actor);
    private CognitionCandidate? AnimalOrderCandidate(OwnerQueuedInstruction instruction) => animalSystem.AnimalOrderCandidate(animalWorldAdapter, instruction);
    private string AnimalOrderBlockedReason(OwnerQueuedInstruction instruction) => animalSystem.AnimalOrderBlockedReason(animalWorldAdapter, instruction);
    private void ExecuteAnimalOrder(OwnerQueuedInstruction instruction) => animalSystem.ExecuteAnimalOrder(animalWorldAdapter, instruction);
    private void CancelAnimalSupplyForOrder(OwnerQueuedInstruction instruction) => animalSystem.CancelAnimalSupplyForOrder(animalWorldAdapter, instruction);
    private bool MaySupplyAnimalYard(string actor, PlacedBuilding yard) => animalSystem.MaySupplyAnimalYard(animalWorldAdapter, actor, yard);
    private AnimalState? RidingAnimal(string actor) => animalSystem.RidingAnimal(animalWorldAdapter, actor);
    private int HorseCargoCapacity(string actor) => animalSystem.HorseCargoCapacity(animalWorldAdapter, actor);
    private void MoveAttachedAnimal(string actor, GridPoint from, GridPoint to) => animalSystem.MoveAttachedAnimal(animalWorldAdapter, actor, from, to);
    private AnimalState AdvanceWildAnimalCare(AnimalState animal, long tick) => animalSystem.AdvanceWildAnimalCare(animalWorldAdapter, animal, tick);
    internal static IOrderedEnumerable<MapResource> WildAnimalForageSources(WorldMap map, EcologyState ecology, AnimalState animal) => AnimalSystem.WildAnimalForageSources(map, ecology, animal);
}
