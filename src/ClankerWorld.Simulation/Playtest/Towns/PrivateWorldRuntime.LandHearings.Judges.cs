namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private Dictionary<string, string?> LandHearingHouseholds() => CivicSystem.LandHearingHouseholds(civicWorld);
    private (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceLandHearingJudges(
        TownRuntimeState town, TownGovernanceState council, TownGovernmentState government, TownLandHearingState hearings) => civicSystem.AdvanceLandHearingJudges(civicWorld, town, council, government, hearings);
}
