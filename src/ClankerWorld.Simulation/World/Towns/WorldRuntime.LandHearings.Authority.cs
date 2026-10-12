using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool LandHearingAdult(string actor) => TownSystem.LandHearingAdult(civicWorld, actor);
    private TownLandCaseParty[] LandHearingParties(TownRuntimeState town,
        IReadOnlyList<GridPoint> tiles, TownLandCase? item = null, string? filingHousehold = null,
        string? townRepresentative = null) => townSystem.LandHearingParties(civicWorld, town, tiles, item, filingHousehold, townRepresentative);
    private bool LandHearingTownFilingAuthority(TownRuntimeState town, string actor, string? authorityId) => TownSystem.LandHearingTownFilingAuthority(civicWorld, town, actor, authorityId);
    private HashSet<string> LandHearingDirectStakes(TownLandCase item) => TownSystem.LandHearingDirectStakes(civicWorld, item);
    private bool LandHearingJudgeConflict(TownRuntimeState town, TownLandCase item, string actor) => townSystem.LandHearingJudgeConflict(civicWorld, town, item, actor);
    private bool LandHearingJudgeValid(TownRuntimeState town, TownLandCase item, TownLandCaseJudge judge) => townSystem.LandHearingJudgeValid(civicWorld, town, item, judge);
    private bool LandHearingMayInspect(TownRuntimeState town, TownLandCase item, string actor) => townSystem.LandHearingMayInspect(civicWorld, town, item, actor);
    private bool LandHearingReadCurrent(TownLandCase item, string actor) => TownSystem.LandHearingReadCurrent(civicWorld, item, actor);
    private bool LandHearingReadRuling(TownLandCase item, string actor) => TownSystem.LandHearingReadRuling(civicWorld, item, actor);
    private bool MayVisitLandHearing(string actor, TownRuntimeState town) => townSystem.MayVisitLandHearing(civicWorld, actor, town);
    private bool HasOpenLandHearingPlot(string townId, IEnumerable<GridPoint> tiles) => townSystem.HasOpenLandHearingPlot(civicWorld, townId, tiles);
    private static bool TownLandHearingElectionBusy(TownRuntimeState town) => TownSystem.TownLandHearingElectionBusy(town);
}
