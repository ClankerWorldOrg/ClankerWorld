using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static string NonviolentToken(string text) => CivicSystem.NonviolentToken(text);
    private static string NonviolentReportToken(TownViolationAllegation report) => CivicSystem.NonviolentReportToken(report);
    private static string NonviolentOfferToken(TownRemedyOffer offer) => CivicSystem.NonviolentOfferToken(offer);
    private static TownCaseRevision NonviolentRevision(TownViolationCase item) => CivicSystem.NonviolentRevision(item);
    private static string NonviolentCaseToken(TownViolationCase item) => CivicSystem.NonviolentCaseToken(item);
    private static string NonviolentActionToken(TownViolationCase item) => CivicSystem.NonviolentActionToken(item);
    private TownCaseParty[] NonviolentParties(TownRuntimeState town, TownViolationCase item) => CivicSystem.NonviolentParties(civicWorld, town, item);
    private bool NonviolentMayInspect(TownRuntimeState town, TownViolationCase item, string actor) => CivicSystem.NonviolentMayInspect(civicWorld, town, item, actor);
    private bool NonviolentReadCurrent(TownViolationCase item, string actor) => CivicSystem.NonviolentReadCurrent(civicWorld, item, actor);
    private bool NonviolentKnows(TownRuntimeState town, TownViolationCase item, string actor) => CivicSystem.NonviolentKnows(civicWorld, town, item, actor);
    private bool NonviolentConflict(TownRuntimeState town, TownViolationCase item, string actor) => CivicSystem.NonviolentConflict(civicWorld, town, item, actor);
    private bool NonviolentJudgeValid(TownRuntimeState town, TownViolationCase item, TownCaseJudge judge) => CivicSystem.NonviolentJudgeValid(civicWorld, town, item, judge);
    private static bool TownNonviolentElectionBusy(TownRuntimeState town) => CivicSystem.TownNonviolentElectionBusy(town);
    private bool MayVisitNonviolentCase(string actor, TownRuntimeState town) => CivicSystem.MayVisitNonviolentCase(civicWorld, actor, town);
    private (TownGovernanceState Council, TownNonviolentState Nonviolent) AdvanceTownNonviolent(TownRuntimeState town, TownGovernanceState council, TownGovernmentState government) => CivicSystem.AdvanceTownNonviolent(civicWorld, town, council, government);
    private static TownGovernanceState PostNonviolentNotice(TownGovernanceState council, TownViolationCase item) => CivicSystem.PostNonviolentNotice(council, item);
    private void NonviolentEvent(string kind, TownRuntimeState town, string subject, string actor) => CivicSystem.NonviolentEvent(civicWorld, kind, town, subject, actor);
    private static string NonviolentText(string? text) => CivicSystem.NonviolentText(text);
    private static string NonviolentExcerpt(string text, int limit) => CivicSystem.NonviolentExcerpt(text, limit);
    private string NonviolentKnownAllegationText(TownRuntimeState town, TownViolationAllegation allegation) => CivicSystem.NonviolentKnownAllegationText(civicWorld, town, allegation);
    private string NonviolentEvidenceChoices(TownRuntimeState town, TownViolationCase item, string actor) => CivicSystem.NonviolentEvidenceChoices(civicWorld, town, item, actor);
}
