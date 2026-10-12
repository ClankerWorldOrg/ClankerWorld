using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly string[] NonviolentFindingChoices = CivicSystem.NonviolentFindingChoices;
    private void AddChildNonviolentCandidates(List<CognitionCandidate> candidates, string actor) => civicSystem.AddChildNonviolentCandidates(civicWorld, candidates, actor);
    private void AddTownNonviolentCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => civicSystem.AddTownNonviolentCandidates(civicWorld, candidates, actor, town);
    private static string NonviolentTermsText(IEnumerable<TownRemedyTerm> terms) => CivicSystem.NonviolentTermsText(terms);
    private string NonviolentDaysText(long ticks) => CivicSystem.NonviolentDaysText(civicWorld, ticks);
    private string NonviolentOfferTiming(TownRemedyOffer offer) => CivicSystem.NonviolentOfferTiming(civicWorld, offer);
    private string NonviolentRemedyReferences(TownRuntimeState town, string actor) => civicSystem.NonviolentRemedyReferences(civicWorld, town, actor);
}
