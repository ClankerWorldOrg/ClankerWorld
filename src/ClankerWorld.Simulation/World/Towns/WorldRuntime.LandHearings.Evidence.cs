using System.Globalization;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string LandHearingLawToken(TownLaw law, TownLawVersion version) => CivicSystem.LandHearingLawToken(law, version);
    private (TownLaw Law, TownLawVersion Version)[] LandHearingApplicableLaws(TownRuntimeState town, TownLandCase item) => civicSystem.LandHearingApplicableLaws(civicWorld, town, item);
    private TownLandHearingState LandHearingInspectRecords(TownRuntimeState town, TownLandHearingState hearings,
        string caseId, string actor, TownGovernanceState council) => civicSystem.LandHearingInspectRecords(civicWorld, town, hearings, caseId, actor, council);
    /// <summary>The plot's current rights, title, applicable law versions and rulings not yet in this notice's file.</summary>
    private (string Id, string Version, string Text)[] LandHearingUnreadRecords(TownRuntimeState town, TownLandCase item) => civicSystem.LandHearingUnreadRecords(civicWorld, town, item);
    private static string LandHearingRightText(HouseholdLandUseRight right) => CivicSystem.LandHearingRightText(right);
    private static string LandHearingTitleText(TownLandTitleRecord title) => CivicSystem.LandHearingTitleText(title);
    private static string LandHearingRulingText(TownLandRuling ruling) => CivicSystem.LandHearingRulingText(ruling);
    private static string LandHearingEvidenceId(TownLandCase item, string actor, string kind, string source) => CivicSystem.LandHearingEvidenceId(item, actor, kind, source);
    private TownLandHearingState LandHearingObserve(TownLandHearingState hearings, TownLandCase item,
        string actor, TownGovernanceState council) => CivicSystem.LandHearingObserve(civicWorld, hearings, item, actor, council);
}
