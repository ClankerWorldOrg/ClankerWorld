namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private (TownCouncilState Council, TownLandRecords LandRecords) OpenLandHearing(TownRuntimeState town,
        TownCouncilState council, TownLandRecords hearings, TownLandCaseFiling filing, IReadOnlyList<GridPoint> tiles,
        string? filingHousehold = null, string? townRepresentative = null, TownPropertyRequest? propertyRequest = null, TownPropertySnapshot? propertySnapshot = null) => townSystem.OpenLandHearing(civicWorld, town, council, hearings, filing, tiles, filingHousehold, townRepresentative, propertyRequest, propertySnapshot);
}
