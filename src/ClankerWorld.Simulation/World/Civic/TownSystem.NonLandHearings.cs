using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static string NonLandToken(string text) => DerivedId.Hash(text);
    internal static string NonLandReportToken(TownViolationAllegation report) => NonLandToken(report.IncidentId + "|" + report.LawId + "|" + report.LawVersion.ToString(CultureInfo.InvariantCulture));
    internal static string NonLandOfferToken(TownRemedyOffer offer) => DerivedId.Create(offer.Id + ":" + offer.Revision.ToString(CultureInfo.InvariantCulture) + ":" + offer.TermsHash);
    internal static TownCaseRevision NonLandRevision(TownNonLandCase item) => item.Revisions[^1];
    internal static string NonLandCaseToken(TownNonLandCase item) => DerivedId.Create(item.Id + ":" + NonLandRevision(item).Number.ToString(CultureInfo.InvariantCulture));
    internal static string NonLandActionToken(TownNonLandCase item) => DerivedId.Create(item.Id + ":" +
        NonLandRevision(item).Number.ToString(CultureInfo.InvariantCulture) + ":" +
        TownHearingProcedure.Digest(new { item.Evidence, item.Responses, item.Findings, item.Judge, item.ReopenRequests }));
    internal static TownCaseParty[] NonLandParties(ITownWorld world, TownRuntimeState town, TownNonLandCase item) =>
        TownNonLandPartyRules.CurrentParties(town, item, world.Society, world.WorldTick);
    internal static bool NonLandMayInspect(ITownWorld world, TownRuntimeState town, TownNonLandCase item, string actor) => world.Agents.ContainsKey(actor) && !LifeStageRules.IsInfant(world.Society.GetAgent(actor).AgeBand) &&
        (TownAdults(world, town).Contains(actor, StringComparer.Ordinal) || item.Judge?.AgentId == actor ||
            item.Filings.Any(filing => filing.AgentId == actor) ||
            town.NonLandHearings.Offers.Any(offer => offer.CaseId == item.Id && offer.Terms.Any(term => term.ContributorId == actor)) || NonLandParties(world, town, item).Any(party =>
                party.SubjectId == actor || party.RespondingAdultId == actor));
    internal static bool NonLandReadCurrent(ITownWorld world, TownNonLandCase item, string actor) => TownNonLandRules.ReadCurrent(item, actor, world.WorldTick);
    internal static bool NonLandKnows(ITownWorld world, TownRuntimeState town, TownNonLandCase item, string actor) =>
        town.Council!.Knowledge.Any(receipt => receipt.AgentId == actor && receipt.NoticeId == NonLandRevision(item).NoticeId && receipt.LearnedTick <= world.WorldTick);
    internal static bool NonLandConflict(ITownWorld world, TownRuntimeState town, TownNonLandCase item, string actor) =>
        item.Filings.Any(filing => filing.AgentId == actor) || item.DirectStakeIds.Contains(actor, StringComparer.Ordinal) ||
        NonLandParties(world, town, item).Any(party => party.SubjectId == actor || party.RespondingAdultId == actor ||
            party.HouseholdId is { } household && household == world.HouseholdFor(actor));
    internal static bool NonLandJudgeValid(ITownWorld world, TownRuntimeState town, TownNonLandCase item, TownCaseJudge judge)
    {
        if (!TownAdults(world, town).Contains(judge.AgentId, StringComparer.Ordinal) || NonLandConflict(world, town, item, judge.AgentId) ||
            town.Government is not { } government || government.Arrangement.NonLand != TownArrangementRules.Mayor) return false;
        if (judge.Kind == "case_elected") return item.JudgeConsents.Any(consent => consent.AgentId == judge.AgentId && consent.WithdrawnTick is null) &&
            item.ContestHistory.Any(contest => contest.Id == judge.AuthorityId && contest.Stage == "completed" && contest.WinnerId == judge.AgentId);
        return judge.Kind == "non_land_mayor" && TownGovernmentRules.CurrentNonLandAuthority(government, world.WorldTick) is { } authority &&
            authority.HolderId == judge.AgentId && authority.AuthorityId == judge.AuthorityId;
    }

    internal static bool TownNonLandElectionBusy(TownRuntimeState town) => town.NonLandHearings.Cases.Any(item => item.Contest is { Stage: "voting" });
    internal static bool MayVisitNonLandCase(ITownWorld world, string actor, TownRuntimeState town) => world.Agents.ContainsKey(actor) && CanWalkToCivicBoard(world, actor, town) && (town.NonLandHearings.Cases.Any(item => NonLandMayInspect(world, town, item, actor) && NonLandKnows(world, town, item, actor)) ||
            NonLandKnownReports(world, town, actor).Length > 0);
    internal static (TownCouncilState Council, TownNonLandHearingState NonLandHearings) AdvanceTownNonLand(ITownWorld world, TownRuntimeState town,
        TownCouncilState council, TownGovernmentState government)
    {
        var state = TownRemedyRules.Advance(town.NonLandHearings, world.WorldTick);
        foreach (var proposal in council.Proposals.Where(proposal => proposal.Kind == "law_case" && proposal.Status == "passed" && proposal.NonLandRequest is not null).ToArray())
        {
            if (state.Cases.Any(item => item.Filings.Any(filing => filing.AuthorityId == proposal.Id)) ||
                government.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
                !council.Members.Contains(proposal.AuthorId, StringComparer.Ordinal) || !TownAdults(world, town).Contains(proposal.AuthorId, StringComparer.Ordinal)) continue;
            var request = proposal.NonLandRequest!;
            if (state.Cases.Any(item => item.Key == TownNonLandRules.CaseKey(town.Id, request.Allegation) && item.Status != "pending")) continue;
            var report = NonLandKnownReports(world, town with { Council = council, NonLandHearings = state }, proposal.AuthorId)
                .FirstOrDefault(report => NonLandReportToken(report.Allegation) == NonLandReportToken(request.Allegation));
            if (report.Allegation is null) continue;
            (council, state) = OpenNonLandCase(world, town, council, state, request.Allegation,
                new(proposal.AuthorId, "town", world.WorldTick, request.Statement, request.Allegation.SourceEvidenceIds, proposal.Id), report.Evidence);
        }
        foreach (var item in state.Cases.Where(item => item.Status == "pending").ToArray())
        {
            var before = NonLandRevision(item).Number;
            state = TownNonLandRules.ReviseParties(state, item.Id, NonLandParties(world, town, item), world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council));
            var revised = state.Cases.Single(current => current.Id == item.Id);
            if (NonLandRevision(revised).Number != before) council = PostNonLandNotice(council, revised);
        }
        state = state with
        {
            Cases = state.Cases.Select(item => item with
            { DirectStakeIds = item.Filings.Select(filing => filing.AgentId).Concat(NonLandParties(world, town, item).Select(party => party.SubjectId)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() }).ToArray()
        };
        var parties = state.Cases.ToDictionary(item => item.Id, item => (IReadOnlyList<TownCaseParty>)NonLandParties(world, town, item), StringComparer.Ordinal);
        (state, council) = TownCaseJudgeRules.Advance(state, council, government, TownAdults(world, town), LandHearingHouseholds(world), world.WorldTick, CivicDay(world), TownLandHearingElectionBusy(town) || government.Contest is { Stage: "voting" or "ready" } || council.Election is { Stage: "ready" }, parties);
        return (council, state);
    }

    internal static TownCouncilState PostNonLandNotice(TownCouncilState council, TownNonLandCase item) =>
        TownCouncilRules.PostNotice(council, "law_case", NonLandCaseToken(item),
            "A reported act is alleged to breach a Town law. The named people may answer with caregiver support where needed. No guilt, payment or work is presumed.", NonLandRevision(item).PublishedTick);
    internal static void NonLandEvent(ITownWorld world, string kind, TownRuntimeState town, string subject, string actor) => world.AppendEvent(WorldEvents.LawCase(kind, actor, town.Id, subject), CivicBoard(world, town));
    internal static string NonLandKnownAllegationText(ITownWorld world, TownRuntimeState town, TownViolationAllegation allegation)
    {
        var version = town.Government!.Laws.Single(law => law.Id == allegation.LawId).Versions.Single(version => version.Version == allegation.LawVersion);
        return BoundedText.Excerpt(world.Society.GetAgent(allegation.SubjectId).Name, 80) + " allegedly " +
            allegation.ConductKind.Replace('_', ' ') + " at (" + allegation.Position.X + "," + allegation.Position.Y + ") at world tick " +
            allegation.ConductTick.ToString(CultureInfo.InvariantCulture) + ". Known source: " + BoundedText.Excerpt(allegation.Statement, 256) +
            ". Applicable law version " + allegation.LawVersion.ToString(CultureInfo.InvariantCulture) + ": " +
BoundedText.Excerpt(TownLawRules.Text(version.Subject, version.Rule), 512);
    }

    internal static string NonLandEvidenceChoices(ITownWorld world, TownRuntimeState town, TownNonLandCase item, string actor)
    {
        var reads = item.Reads.Where(read => read.AgentId == actor && read.Revision == NonLandRevision(item).Number && read.ReadTick <= world.WorldTick).ToArray();
        var known = reads.SelectMany(read => read.EvidenceIds).ToHashSet(StringComparer.Ordinal);
        var responses = reads.SelectMany(read => read.ResponseIds).ToHashSet(StringComparer.Ordinal);
        var findings = reads.SelectMany(read => read.FindingIds).ToHashSet(StringComparer.Ordinal);
        return (reads.Length == 0 ? "" : " Inspected allegation: " + NonLandKnownAllegationText(world, town, item.Allegation) + ".") +
            " Inspected evidence: " + string.Join("; ", item.Evidence.Where(evidence => known.Contains(evidence.Id)).TakeLast(8)
            .Select(evidence => CivicAgentToken(evidence.Id) + " (" + evidence.Kind + ", " + evidence.Acquisition + "): " + BoundedText.Excerpt(evidence.Text, 256))) +
            ". Inspected responses: " + string.Join("; ", item.Responses.Where(response => responses.Contains(TownNonLandRules.ResponseToken(response))).TakeLast(8)
                .Select(response => BoundedText.Excerpt(world.Society.GetAgent(response.AgentId).Name, 80) + " " + response.Kind + ": " + BoundedText.Excerpt(response.Text, 256))) +
            ". Inspected prior findings: " + string.Join("; ", item.Findings.Where(finding => findings.Contains(finding.Id)).TakeLast(4)
                .Select(finding => finding.Id + " " + finding.Result + "/" + finding.Consequence + ": " + BoundedText.Excerpt(finding.Reasons, 256) +
                    "; uncertainty: " + BoundedText.Excerpt(finding.Uncertainty, 160))) + ".";
    }
}
