using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static readonly string[] NonLandFindingChoices = TownSystem.NonLandFindingChoices;
    private void AddChildNonLandCandidates(List<CognitionCandidate> candidates, string actor) => townSystem.AddChildNonLandCandidates(civicWorld, candidates, actor);
}
