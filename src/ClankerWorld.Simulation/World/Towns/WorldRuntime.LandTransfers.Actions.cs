using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private (TownCouncilState Council, TownLandRecords LandRecords) ApplyTownLandTransferAction(
        TownRuntimeState town, string actor, string action, string subject, string choice,
        IReadOnlyList<CognitionLandTile>? landTiles, CognitionLandHearingChoice? hearingChoice, TownCouncilState council) => townSystem.ApplyTownLandTransferAction(civicWorld, town, actor, action, subject, choice, landTiles, hearingChoice, council);
}
