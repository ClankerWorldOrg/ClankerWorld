using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private bool LandHearingAdult(string actor) => CivicSystem.LandHearingAdult(civicWorld, actor);
    private TownLandCaseParty[] LandHearingParties(TownRuntimeState town,
        IReadOnlyList<GridPoint> tiles, TownLandCase? item = null, string? filingHousehold = null,
        string? townRepresentative = null) => civicSystem.LandHearingParties(civicWorld, town, tiles, item, filingHousehold, townRepresentative);
    private bool LandHearingTownFilingAuthority(TownRuntimeState town, string actor, string? authorityId) => CivicSystem.LandHearingTownFilingAuthority(civicWorld, town, actor, authorityId);
    private HashSet<string> LandHearingDirectStakes(TownLandCase item) => CivicSystem.LandHearingDirectStakes(civicWorld, item);
    private bool LandHearingJudgeConflict(TownRuntimeState town, TownLandCase item, string actor) => civicSystem.LandHearingJudgeConflict(civicWorld, town, item, actor);
    private bool LandHearingJudgeValid(TownRuntimeState town, TownLandCase item, TownLandCaseJudge judge) => civicSystem.LandHearingJudgeValid(civicWorld, town, item, judge);
    private bool LandHearingMayInspect(TownRuntimeState town, TownLandCase item, string actor) => civicSystem.LandHearingMayInspect(civicWorld, town, item, actor);
    private bool LandHearingReadCurrent(TownLandCase item, string actor) => CivicSystem.LandHearingReadCurrent(civicWorld, item, actor);
    private bool LandHearingReadRuling(TownLandCase item, string actor) => CivicSystem.LandHearingReadRuling(civicWorld, item, actor);
    private bool MayVisitLandHearing(string actor, TownRuntimeState town) => civicSystem.MayVisitLandHearing(civicWorld, actor, town);
    private bool HasOpenLandHearingPlot(string townId, IEnumerable<GridPoint> tiles) => civicSystem.HasOpenLandHearingPlot(civicWorld, townId, tiles);
    private static bool TownLandHearingElectionBusy(TownRuntimeState town) => CivicSystem.TownLandHearingElectionBusy(town);
}
