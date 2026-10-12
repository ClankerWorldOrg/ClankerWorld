using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string NonLandToken(string text) => TownSystem.NonLandToken(text);
    private static string NonLandReportToken(TownViolationAllegation report) => TownSystem.NonLandReportToken(report);
    private static string NonLandOfferToken(TownRemedyOffer offer) => TownSystem.NonLandOfferToken(offer);
    private static TownCaseRevision NonLandRevision(TownNonLandCase item) => TownSystem.NonLandRevision(item);
    private static string NonLandCaseToken(TownNonLandCase item) => TownSystem.NonLandCaseToken(item);
    private static string NonLandActionToken(TownNonLandCase item) => TownSystem.NonLandActionToken(item);
    private TownCaseParty[] NonLandParties(TownRuntimeState town, TownNonLandCase item) => TownSystem.NonLandParties(civicWorld, town, item);
    private bool NonLandMayInspect(TownRuntimeState town, TownNonLandCase item, string actor) => TownSystem.NonLandMayInspect(civicWorld, town, item, actor);
    private bool NonLandReadCurrent(TownNonLandCase item, string actor) => TownSystem.NonLandReadCurrent(civicWorld, item, actor);
    private bool NonLandKnows(TownRuntimeState town, TownNonLandCase item, string actor) => TownSystem.NonLandKnows(civicWorld, town, item, actor);
    private bool NonLandConflict(TownRuntimeState town, TownNonLandCase item, string actor) => TownSystem.NonLandConflict(civicWorld, town, item, actor);
    private bool NonLandJudgeValid(TownRuntimeState town, TownNonLandCase item, TownCaseJudge judge) => TownSystem.NonLandJudgeValid(civicWorld, town, item, judge);
    private static bool TownNonLandElectionBusy(TownRuntimeState town) => TownSystem.TownNonLandElectionBusy(town);
    private bool MayVisitNonLandCase(string actor, TownRuntimeState town) => TownSystem.MayVisitNonLandCase(civicWorld, actor, town);
    private (TownCouncilState Council, TownNonLandHearingState NonLandHearings) AdvanceTownNonLand(TownRuntimeState town, TownCouncilState council, TownGovernmentState government) => TownSystem.AdvanceTownNonLand(civicWorld, town, council, government);
    private static TownCouncilState PostNonLandNotice(TownCouncilState council, TownNonLandCase item) => TownSystem.PostNonLandNotice(council, item);
    private void NonLandEvent(string kind, TownRuntimeState town, string subject, string actor) => TownSystem.NonLandEvent(civicWorld, kind, town, subject, actor);
    private static string NonLandText(string? text) => TownSystem.NonLandText(text);
    private static string NonLandExcerpt(string text, int limit) => TownSystem.NonLandExcerpt(text, limit);
    private string NonLandKnownAllegationText(TownRuntimeState town, TownViolationAllegation allegation) => TownSystem.NonLandKnownAllegationText(civicWorld, town, allegation);
    private string NonLandEvidenceChoices(TownRuntimeState town, TownNonLandCase item, string actor) => TownSystem.NonLandEvidenceChoices(civicWorld, town, item, actor);
}
