using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IEnumerable<AnimalSystem.AnimalAgreement> AnimalAgreements(string actor) => animalSystem.AnimalAgreements(animalWorldAdapter, actor);
    private bool HasAnimalTransferSpace(string household, PlacedBuilding yard, AnimalState animal, string? ignoringOffer = null) => animalSystem.HasAnimalTransferSpace(animalWorldAdapter, household, yard, animal, ignoringOffer);
    private bool ValidAnimalOffer(AnimalTradeOffer offer) => animalSystem.ValidAnimalOffer(animalWorldAdapter, offer);
    private void AddAnimalPermissionAndTradeCandidates(List<CognitionCandidate> candidates, string actor) => animalSystem.AddAnimalPermissionAndTradeCandidates(animalWorldAdapter, candidates, actor);
    private bool ApplyAnimalConsentDecision(SocietyCognitionDispatchResult decision)
    {
        if (!animalSystem.TryPlanAnimalConsentDecision(animalWorldAdapter, decision, out var plan)) return false;
        if (plan is not null)
            TryRegisteredAction(ActionPhase.FreshAdmission, new(plan.Actor, agents[plan.Actor], plan.CandidateId, false, plan));
        return true;
    }
}
