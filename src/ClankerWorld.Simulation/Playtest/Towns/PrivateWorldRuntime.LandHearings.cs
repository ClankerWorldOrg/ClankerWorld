using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private (TownGovernanceState Council, TownLandHearingState LandHearings) AdvanceTownLandHearings(
        TownRuntimeState town, TownGovernanceState council, TownGovernmentState government) => civicSystem.AdvanceTownLandHearings(civicWorld, town, council, government);
    private (TownGovernanceState Council, TownLandHearingState LandHearings) OpenLandHearing(TownRuntimeState town,
        TownGovernanceState council, TownLandHearingState hearings, TownLandCaseFiling filing, IReadOnlyList<GridPoint> tiles,
        string? filingHousehold = null, string? townRepresentative = null, TownPropertyRequest? propertyRequest = null) => civicSystem.OpenLandHearing(civicWorld, town, council, hearings, filing, tiles, filingHousehold, townRepresentative, propertyRequest);
    private static TownGovernanceState PostLandHearingNotice(TownGovernanceState council, TownLandCase item) => CivicSystem.PostLandHearingNotice(council, item);
    private void LandHearingEvent(string kind, TownRuntimeState town, TownLandCase item, string? actor, string status) => CivicSystem.LandHearingEvent(civicWorld, kind, town, item, actor, status);
}
