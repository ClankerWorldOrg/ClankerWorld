using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Advances one Town's council, then its laws, government changes and mayor's office, at the current tick.</summary>
    private (TownGovernanceState Council, TownGovernmentState Government) AdvanceCivic(TownRuntimeState town, TownGovernanceState council, TownGovernmentState government) => CivicSystem.AdvanceCivic(civicWorld, town, council, government);
    private void AddTownLawCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => CivicSystem.AddTownLawCandidates(civicWorld, candidates, actor, town);
    private static string SiteToken(GridPoint center) => CivicSystem.SiteToken(center);
    private GridPoint[] SiteFromToken(string token, string townId) => CivicSystem.SiteFromToken(civicWorld, token, townId);
    /// <summary>Applies a structured law action already checked against the actor's current legal candidates.</summary>
    private (TownGovernanceState, TownGovernmentState) ApplyTownLawAction(TownRuntimeState town, string actor, string kind, string target, string versionToken, string? text, TownGovernanceState council, TownGovernmentState government) => CivicSystem.ApplyTownLawAction(civicWorld, town, actor, kind, target, versionToken, text, council, government);
}
