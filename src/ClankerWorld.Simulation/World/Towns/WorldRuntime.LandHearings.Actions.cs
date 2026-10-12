using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddTownLandHearingCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => civicSystem.AddTownLandHearingCandidates(civicWorld, candidates, actor, town);
    private string LandHearingCaseChoices(TownRuntimeState town, TownLandCase item, string actor) => civicSystem.LandHearingCaseChoices(civicWorld, town, item, actor);
    private (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandHearingAction(TownRuntimeState town,
        string actor, string action, string subject, string choice, string? proposalText, IReadOnlyList<CognitionLandTile>? landTiles,
        CognitionLandHearingChoice? hearingChoice, TownGovernanceState council, TownGovernmentState government) => civicSystem.ApplyTownLandHearingAction(civicWorld, town, actor, action, subject, choice, proposalText, landTiles, hearingChoice, council, government);
    private TownLandRequestedOutcome LandHearingOutcome(string kind, CognitionLandHearingChoice? choice) => CivicSystem.LandHearingOutcome(civicWorld, kind, choice);
    private static string LandHearingText(string? text) => CivicSystem.LandHearingText(text);
}
