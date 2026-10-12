using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void AddTownLandHearingCandidates(List<CognitionCandidate> candidates, string actor, TownRuntimeState town) => townSystem.AddTownLandHearingCandidates(civicWorld, candidates, actor, town);
    private string LandHearingCaseChoices(TownRuntimeState town, TownLandCase item, string actor) => townSystem.LandHearingCaseChoices(civicWorld, town, item, actor);
    private (TownCouncilState Council, TownLandRecords LandRecords) ApplyTownLandHearingAction(TownRuntimeState town,
        string actor, string action, string subject, string choice, string? proposalText, IReadOnlyList<CognitionLandTile>? landTiles,
        CognitionLandHearingChoice? hearingChoice, TownCouncilState council, TownGovernmentState government) => townSystem.ApplyTownLandHearingAction(civicWorld, town, actor, action, subject, choice, proposalText, landTiles, hearingChoice, council, government);
    private TownLandRequestedOutcome LandHearingOutcome(string kind, CognitionLandHearingChoice? choice) => TownSystem.LandHearingOutcome(civicWorld, kind, choice);
    private static string LandHearingText(string? text) => TownSystem.LandHearingText(text);
}
