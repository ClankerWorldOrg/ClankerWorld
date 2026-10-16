namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private (TownCouncilState Council, TownLandRecords LandRecords) AdvanceTownLandHearings(
        TownRuntimeState town, TownCouncilState council, TownGovernmentState government) => townSystem.AdvanceTownLandHearings(civicWorld, town, council, government);
    private (TownCouncilState Council, TownLandRecords LandRecords) OpenLandHearing(TownRuntimeState town,
        TownCouncilState council, TownLandRecords hearings, TownLandCaseFiling filing, IReadOnlyList<GridPoint> tiles,
        string? filingHousehold = null, string? townRepresentative = null, TownPropertyRequest? propertyRequest = null, TownPropertySnapshot? propertySnapshot = null) => townSystem.OpenLandHearing(civicWorld, town, council, hearings, filing, tiles, filingHousehold, townRepresentative, propertyRequest, propertySnapshot);
    private static TownCouncilState PostLandHearingNotice(TownCouncilState council, TownLandCase item) => TownSystem.PostLandHearingNotice(council, item);
    private void LandHearingEvent(string kind, TownRuntimeState town, TownLandCase item, string? actor, string status) => TownSystem.LandHearingEvent(civicWorld, kind, town, item, actor, status);
}
