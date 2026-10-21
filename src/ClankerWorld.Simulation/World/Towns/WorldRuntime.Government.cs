using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddTownLawCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => TownSystem.AddTownLawCandidates(civicWorld, candidates, actor, town);
}
