using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly string[] NonLandFindingChoices = TownSystem.NonLandFindingChoices;
    private void AddChildNonLandCandidates(List<CognitionCandidate> candidates, string actor) => townSystem.AddChildNonLandCandidates(civicWorld, candidates, actor);
    private void AddTownNonLandCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => townSystem.AddTownNonLandCandidates(civicWorld, candidates, actor, town);
    private static string NonLandTermsText(IEnumerable<TownRemedyTerm> terms) => TownSystem.NonLandTermsText(terms);
    private string NonLandDaysText(long ticks) => TownSystem.NonLandDaysText(civicWorld, ticks);
    private string NonLandOfferTiming(TownRemedyOffer offer) => TownSystem.NonLandOfferTiming(civicWorld, offer);
    private string NonLandRemedyReferences(TownRuntimeState town, string actor) => townSystem.NonLandRemedyReferences(civicWorld, town, actor);
}
