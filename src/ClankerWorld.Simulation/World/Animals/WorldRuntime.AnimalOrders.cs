using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static bool IsAnimalOrder(string action) => AnimalSystem.IsAnimalOrder(action);
    private OrderTask? ParseAnimalOrder(string text, string actor) => animalSystem.ParseAnimalOrder(animalWorldAdapter, text, actor);
    private CognitionCandidate? AnimalOrderCandidate(OwnerQueuedInstruction instruction) => animalSystem.AnimalOrderCandidate(animalWorldAdapter, instruction);
    private bool HasAnimalCareOrderSupplies(string actor, AnimalState animal) => animalSystem.HasAnimalCareOrderSupplies(animalWorldAdapter, actor, animal);
    private string AnimalOrderBlockedReason(OwnerQueuedInstruction instruction) => animalSystem.AnimalOrderBlockedReason(animalWorldAdapter, instruction);
    private void ExecuteAnimalOrder(OwnerQueuedInstruction instruction) => animalSystem.ExecuteAnimalOrder(animalWorldAdapter, instruction);
    private void CancelAnimalSupplyForOrder(OwnerQueuedInstruction instruction) => animalSystem.CancelAnimalSupplyForOrder(animalWorldAdapter, instruction);
}
