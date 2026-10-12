using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private IEnumerable<AnimalSystem.AnimalAgreement> AnimalAgreements(string actor) => animalSystem.AnimalAgreements(animalWorldAdapter, actor);
    private bool HasAnimalTransferSpace(string household, PlacedBuilding yard, AnimalState animal, string? ignoringOffer = null) => animalSystem.HasAnimalTransferSpace(animalWorldAdapter, household, yard, animal, ignoringOffer);
    private bool ValidAnimalOffer(AnimalTradeOffer offer) => animalSystem.ValidAnimalOffer(animalWorldAdapter, offer);
    private void AddAnimalPermissionAndTradeCandidates(List<CognitionCandidate> candidates, string actor) => animalSystem.AddAnimalPermissionAndTradeCandidates(animalWorldAdapter, candidates, actor);
    private bool ApplyAnimalConsentDecision(SocietyCognitionDispatchResult decision) => animalSystem.ApplyAnimalConsentDecision(animalWorldAdapter, decision);
}
