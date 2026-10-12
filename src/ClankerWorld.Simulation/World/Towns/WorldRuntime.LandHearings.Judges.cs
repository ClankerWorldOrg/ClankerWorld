namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private Dictionary<string, string?> LandHearingHouseholds() => TownSystem.LandHearingHouseholds(civicWorld);
    private (TownCouncilState Council, TownLandRecords LandRecords) AdvanceLandHearingJudges(
        TownRuntimeState town, TownCouncilState council, TownGovernmentState government, TownLandRecords hearings) => townSystem.AdvanceLandHearingJudges(civicWorld, town, council, government, hearings);
}
