using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private (TownGovernanceState Council, TownLandHearingState LandHearings) ApplyTownLandTransferAction(
        TownRuntimeState town, string actor, string action, string subject, string choice,
        IReadOnlyList<CognitionLandTile>? landTiles, CognitionLandHearingChoice? hearingChoice, TownGovernanceState council) => civicSystem.ApplyTownLandTransferAction(civicWorld, town, actor, action, subject, choice, landTiles, hearingChoice, council);
}
