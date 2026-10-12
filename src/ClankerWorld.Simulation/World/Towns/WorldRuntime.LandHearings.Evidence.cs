using System.Globalization;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string LandHearingLawToken(TownLaw law, TownLawVersion version) => TownSystem.LandHearingLawToken(law, version);
    private (TownLaw Law, TownLawVersion Version)[] LandHearingApplicableLaws(TownRuntimeState town, TownLandCase item) => townSystem.LandHearingApplicableLaws(civicWorld, town, item);
    private TownLandRecords LandHearingInspectRecords(TownRuntimeState town, TownLandRecords hearings,
        string caseId, string actor, TownCouncilState council) => townSystem.LandHearingInspectRecords(civicWorld, town, hearings, caseId, actor, council);
    /// <summary>The plot's current rights, title, applicable law versions and rulings not yet in this notice's file.</summary>
    private (string Id, string Version, string Text)[] LandHearingUnreadRecords(TownRuntimeState town, TownLandCase item) => townSystem.LandHearingUnreadRecords(civicWorld, town, item);
    private static string LandHearingRightText(HouseholdLandUseRight right) => TownSystem.LandHearingRightText(right);
    private static string LandHearingTitleText(TownLandTitleRecord title) => TownSystem.LandHearingTitleText(title);
    private static string LandHearingRulingText(TownLandRuling ruling) => TownSystem.LandHearingRulingText(ruling);
    private static string LandHearingEvidenceId(TownLandCase item, string actor, string kind, string source) => TownSystem.LandHearingEvidenceId(item, actor, kind, source);
    private TownLandRecords LandHearingObserve(TownLandRecords hearings, TownLandCase item,
        string actor, TownCouncilState council) => TownSystem.LandHearingObserve(civicWorld, hearings, item, actor, council);
}
