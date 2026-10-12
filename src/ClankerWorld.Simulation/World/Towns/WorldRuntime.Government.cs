using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Advances one Town's council, then its laws, government changes and mayor's office, at the current tick.</summary>
    private (TownCouncilState Council, TownGovernmentState Government) AdvanceCivic(TownRuntimeState town, TownCouncilState council, TownGovernmentState government) => TownSystem.AdvanceCivic(civicWorld, town, council, government);
    private void AddTownLawCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => TownSystem.AddTownLawCandidates(civicWorld, candidates, actor, town);
    private static string SiteToken(GridPoint center) => TownSystem.SiteToken(center);
    private GridPoint[] SiteFromToken(string token, string townId) => TownSystem.SiteFromToken(civicWorld, token, townId);
    /// <summary>Applies a structured law action already checked against the actor's current legal candidates.</summary>
    private (TownCouncilState, TownGovernmentState) ApplyTownLawAction(TownRuntimeState town, string actor, string kind, string target, string versionToken, string? text, TownCouncilState council, TownGovernmentState government) => TownSystem.ApplyTownLawAction(civicWorld, town, actor, kind, target, versionToken, text, council, government);
}
