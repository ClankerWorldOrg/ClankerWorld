using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddChildNonLandCandidates(List<CognitionCandidate> candidates, string actor) => townSystem.AddChildNonLandCandidates(civicWorld, candidates, actor);
}
