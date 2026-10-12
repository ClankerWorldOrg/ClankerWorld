using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandTransferAction(
        TownRuntimeState town, string actor, string action, string subject, string choice,
        IReadOnlyList<CognitionLandTile>? landTiles, CognitionLandHearingChoice? hearingChoice, TownGovernanceState council) => civicSystem.ApplyTownLandTransferAction(civicWorld, town, actor, action, subject, choice, landTiles, hearingChoice, council);
}
