using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private static readonly string[] NonviolentFindingChoices = CivicSystem.NonviolentFindingChoices;
    private void AddChildNonviolentCandidates(List<CognitionCandidate> candidates, string actor) => CivicSystem.AddChildNonviolentCandidates(civicWorld, candidates, actor);
    private void AddTownNonviolentCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => CivicSystem.AddTownNonviolentCandidates(civicWorld, candidates, actor, town);
    private static string NonviolentTermsText(IEnumerable<TownRemedyTerm> terms) => CivicSystem.NonviolentTermsText(terms);
    private string NonviolentDaysText(long ticks) => CivicSystem.NonviolentDaysText(civicWorld, ticks);
    private string NonviolentOfferTiming(TownRemedyOffer offer) => CivicSystem.NonviolentOfferTiming(civicWorld, offer);
    private string NonviolentRemedyReferences(TownRuntimeState town, string actor) => CivicSystem.NonviolentRemedyReferences(civicWorld, town, actor);
}
