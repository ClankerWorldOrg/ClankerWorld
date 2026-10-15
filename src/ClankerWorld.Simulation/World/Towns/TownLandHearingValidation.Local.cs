using System.Diagnostics.CodeAnalysis;

namespace ClankerWorld.Simulation.World;

public static partial class TownLandHearingValidation
{
    internal static void ValidateLocal(string townId, [NotNull] TownLandRecords? state)
    {
        Check(state is not null && state.Sequence >= 0 && state.Cases is not null && state.OriginalRights is not null && state.Adjustments is not null && state.Transfers is not null,
            "Saved land hearings must contain their case file and permission history.");
        Check(state.Cases.All(c => c is not null) && state.OriginalRights.All(r => r is not null) && state.Adjustments.All(a => a is not null),
            "Saved land hearings cannot contain null records.");
        Check(HasRecords(state), "Saved land hearings must contain their case file and permission history.");
        foreach (var right in state.OriginalRights) ValidateLocalRight(townId, right);
        foreach (var adjustment in state.Adjustments)
        {
            foreach (var right in adjustment.PriorRights) ValidateLocalRight(townId, right);
            foreach (var right in adjustment.ResultRights) ValidateLocalRight(townId, TownLandHearingRules.Snapshot(right));
        }
        // Evidence may point into another case's historical rights. Check every file's
        // shape before those lookups, while leaving semantic validation to one pass.
        foreach (var item in state.Cases) ValidateLocalCaseFile(townId, item, state.Sequence);
        foreach (var item in state.Cases) ValidateLocalCaseHistory(townId, item);
        foreach (var item in state.Cases) ValidateLocalRulings(state, townId, item);
        foreach (var item in state.Cases) ValidateLocalReopening(state, townId, item);
        TownLandTransferValidation.ValidateLocal(townId, state);
        TownPropertyValidation.ValidateLocalFiles(state, townId);
        ValidateLocalAdjustments(townId, state);
        Check(!state.Cases.Where(c => c.Status == "pending").SelectMany(c => TownLandHearingRules.CurrentRevision(c).Tiles)
            .GroupBy(tile => tile).Any(group => group.Count() > 1), "Pending land hearings cannot overlap their plots.");
        Unique(state.Cases.Select(c => c.Id));
        Unique(state.Cases.SelectMany(c => c.ReopenRequests).Select(r => r.Id));
        Unique(state.OriginalRights.Select(r => r.Id));
        Unique(state.Adjustments.Select(a => a.Id));
        Check(state.Cases.Count + state.Adjustments.Count + state.Transfers.Count <= state.Sequence, "Saved hearing numbering cannot precede its records.");
    }

    private static void ValidateLocalCaseFile(string townId, TownLandCase item, long sequence)
    {
        Check(Generated(item.Id, "land-case:", townId, sequence) && item.TownId == townId && item.Kind is "dispute" or "expiry" or "property" && item.Status is "pending" or "settled" &&
            item.FiledTick >= 0 && item.Revisions is { Count: > 0 } && item.Filings is { Count: > 0 } &&
            item.Evidence is not null && item.Responses is not null && item.Reads is not null && item.Rulings is not null &&
            item.JudgeHistory is not null && item.JudgeConsents is not null && item.ContestHistory is not null && item.ReopenRequests is not null && item.DirectStakeIds is not null,
            "A saved land case must retain its complete file.");
        Check(item.Revisions.All(r => r is not null) && item.Filings.All(r => r is not null) && item.Evidence.All(r => r is not null) &&
            item.Responses.All(r => r is not null) && item.Reads.All(r => r is not null) && item.Rulings.All(r => r is not null && r.Judge is not null) &&
            item.JudgeHistory.All(r => r is not null && r.Judge is not null) && item.JudgeConsents.All(r => r is not null) && item.ContestHistory.All(r => r is not null) &&
            item.ReopenRequests.All(r => r is not null), "A saved land case cannot contain null file entries.");
        foreach (var revision in item.Revisions)
            Check(revision.Tiles is { Count: > 0 } && revision.Parties is { Count: > 0 } && revision.Parties.All(p => p is not null) &&
                revision.RightVersions is not null && revision.RightVersions.All(r => r is not null && r.Right is not null),
                "A saved land revision cannot omit its plot, parties or prior rights.");
        Check(item.Revisions.Select(r => r.Number).SequenceEqual(Enumerable.Range(1, item.Revisions.Count)) &&
            item.Revisions.Select(r => r.PublishedTick).SequenceEqual(item.Revisions.Select(r => r.PublishedTick).Order()) &&
            item.Key == TownLandHearingRules.CaseKey(townId, item.Kind, item.Revisions[0].Tiles), "Saved land notice revisions and case identity must be canonical.");
    }

    private static void ValidateLocalCaseHistory(string townId, TownLandCase item)
    {
        if (item.Judge is { } judge) ValidateLocalJudge(judge, item);
        foreach (var term in item.JudgeHistory) ValidateLocalJudge(term.Judge, item);
        foreach (var ruling in item.Rulings) ValidateLocalJudge(ruling.Judge, item);
        foreach (var request in item.ReopenRequests)
            if (request.AssessedBy is { } assessor) ValidateLocalJudge(assessor, item);
        foreach (var contest in item.ContestHistory) ValidateLocalContest(contest, item);
        if (item.Contest is { } live) ValidateLocalContest(live, item);
        Check(CanonicalPeople(item.DirectStakeIds), "Personal stakes must name known agents.");
        foreach (var revision in item.Revisions)
        {
            Check(TownSystem.IsLocallyValidPlot(revision.Tiles, revision.PublishedTick) && revision.PublishedTick >= item.FiledTick &&
                TownLandHearingRules.IsValidOutcome(revision.RequestedOutcome, item.FiledTick),
                "A saved land notice needs its complete titled plot and full response day.");
            Unique(revision.Parties.Select(p => p.Id));
            Check(revision.Parties.Select(p => p.Id).SequenceEqual(revision.Parties.Select(p => p.Id).Order(StringComparer.Ordinal)), "Case parties must be in canonical order.");
            foreach (var party in revision.Parties)
                Check(Id(party.Id) && CanonicalPeople(party.AdultIds) &&
                    (party.Kind == "household" && party.HouseholdId is not null && (party.TownId is null || party.TownId == townId) && party.RepresentativeId is null ||
                        party.Kind == "town" && party.TownId == townId && party.HouseholdId is null && party.AdultIds.Count == 0),
                    "A saved case party must identify its actual household adults or recorded Town representative.");
            Unique(revision.RightVersions.Select(r => r.Id));
            foreach (var right in revision.RightVersions) ValidateLocalRight(townId, right);
            Check(revision.RightVersions.All(right => right.Right.Tiles.Any(revision.Tiles.Contains)), "An inspected prior right must concern the noticed plot.");
        }
        foreach (var filing in item.Filings)
            Check(filing.Kind is "dispute" or "expiry" or "town" && TownLandHearingRules.ValidText(filing.Text) && filing.Tick >= item.FiledTick &&
                TownLandHearingRules.IsValidOutcome(filing.RequestedOutcome, filing.Tick) && (filing.Kind == "expiry" ? filing.AgentId is null : filing.AgentId is not null),
                "A saved filing must retain an eligible source and bounded request.");
        foreach (var evidence in item.Evidence)
            Check(TownLandHearingRules.ValidEvidence(evidence), "Saved evidence requires a real informed submission with source and time.");
        foreach (var response in item.Responses)
            Check(item.Revisions.Any(r => r.Number == response.Revision && r.Parties.Any(p => p.Id == response.PartyId)) &&
                response.Kind is "answer" or "waive" && TownLandHearingRules.ValidText(response.Text),
                "A saved response must be the noticed party's own informed answer.");
        foreach (var consent in item.JudgeConsents)
            Check(consent.Tick >= item.FiledTick && (consent.WithdrawnTick is null || consent.WithdrawnTick >= consent.Tick),
                "Saved case candidacy must retain personal consent for this case.");
        foreach (var term in item.JudgeHistory)
            Check(term.EndedTick >= term.Judge.AssignedTick && TownLandHearingRules.ValidText(term.Reason), "A saved case assignment needs its ending history.");
        for (var readIndex = 0; readIndex < item.Reads.Count; readIndex++)
        {
            var read = item.Reads[readIndex];
            var earlierReads = item.Reads.Take(readIndex).ToArray();
            var revision = item.Revisions.SingleOrDefault(r => r.Number == read.Revision);
            Check(revision is not null && read.ReadTick >= revision.PublishedTick &&
                read.EvidenceIds is not null && Canonical(read.EvidenceIds) && read.EvidenceIds.All(id => item.Evidence.Any(e => e.Id == id && e.SubmittedTick <= read.ReadTick)) &&
                Canonical(read.ReopenRequestIds) && read.ReopenRequestIds.All(id => item.ReopenRequests.Any(r => r.Id == id && r.Tick <= read.ReadTick)),
                "A saved file read cannot include unknown or future evidence.");
            if (read.SourceAgentId is { } source)
                Check(source != read.AgentId && earlierReads.Any(r => r.Revision == read.Revision && r.AgentId == source && r.ReadTick <= read.ReadTick) &&
                    read.EvidenceIds.All(id => earlierReads.Any(r => r.Revision == read.Revision && (r.AgentId == source || r.AgentId == read.AgentId) && r.ReadTick <= read.ReadTick && r.EvidenceIds.Contains(id, StringComparer.Ordinal))) &&
                    read.ReopenRequestIds.All(id => earlierReads.Any(r => r.Revision == read.Revision && (r.AgentId == source || r.AgentId == read.AgentId) && r.ReadTick <= read.ReadTick && r.ReopenRequestIds.Contains(id, StringComparer.Ordinal))),
                    "A saved case relay cannot teach evidence its source has not read.");
        }
        Check(item.Reads.Select(r => r.ReadTick).SequenceEqual(item.Reads.Select(r => r.ReadTick).Order()), "Case reads must preserve actual chronological provenance.");
        Unique(item.Evidence.Select(e => e.Id));
        Unique(item.Responses.Select(r => r.Revision + ":" + r.PartyId + ":" + r.AgentId));
        Unique(item.JudgeConsents.Where(c => c.WithdrawnTick is null).Select(c => c.AgentId));
        Unique(item.Rulings.Select(r => r.Id));
    }
    private static void ValidateLocalRight(string townId, TownLandRightVersion snapshot) =>
        Check(snapshot is not null && snapshot.Right is { } right && snapshot.Id == right.Id && Id(right.Id) && right.TownId == townId &&
            TownLandHearingRules.ValidText(right.GrantSource, 64) && snapshot.Version == TownLandHearingRules.Version(right) &&
            TownSystem.IsLocallyValidPlot(right.Tiles, right.GrantedTick, right.AgreedEndTick),
            "A saved right snapshot must retain its exact known holder, plot, terms and version.");
    private static void ValidateLocalContest(TownLandCaseJudgeContest contest, TownLandCase item)
    {
        Check(contest is not null && Id(contest.Id) && contest.Stage is "voting" or "waiting" or "completed" or "failed" or "cancelled" && contest.Round >= 0 && contest.Interruptions >= 0 &&
            contest.OpenedTick >= item.FiledTick && contest.Rounds is not null && contest.Ballots is not null &&
            CanonicalPeople(contest.Voters) && CanonicalPeople(contest.Candidates) && CanonicalPeople(contest.TiedCandidates), "A saved case election needs actual resident candidates and ballots.");
        ValidateBallots(contest.Voters, contest.Candidates, contest.Ballots);
        if (contest.Stage == "voting") Check(contest.RoundOpenedTick is { } opened && opened >= contest.OpenedTick,
            "A case election round needs a full unpaused voting day.");
        foreach (var round in contest.Rounds)
        {
            Check(round is not null && round.Number > 0 && round.Number <= contest.Round && round.OpenedTick >= contest.OpenedTick && round.ClosedTick >= round.OpenedTick &&
                round.Result is "winner" or "tie" or "interrupted" or "failed" or "cancelled" && CanonicalPeople(round.Voters) && CanonicalPeople(round.Candidates) && CanonicalPeople(round.TiedCandidates), "A saved case election must retain each completed or interrupted round.");
            ValidateBallots(round.Voters, round.Candidates, round.Ballots);
            Check(round.Candidates.All(id => item.JudgeConsents.Any(c => c.AgentId == id && c.Tick <= round.OpenedTick && (c.WithdrawnTick is null || c.WithdrawnTick >= round.ClosedTick))),
                "A case candidate must personally consent to that case's mandate.");
        }
        Unique(contest.Rounds.Select(r => r.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (contest.Stage == "completed")
        {
            var round = contest.Rounds.Count == 0 ? null : contest.Rounds[^1];
            Check(round is { Result: "winner" } && contest.WinnerId is { } winner && round.Candidates.Contains(winner, StringComparer.Ordinal) && contest.SettledTick == round.ClosedTick &&
                round.Ballots.Any(b => b.CandidateId == winner) && round.Candidates.Where(id => id != winner).All(id =>
                    round.Ballots.Count(b => b.CandidateId == id) < round.Ballots.Count(b => b.CandidateId == winner)), "A case judge must win an actual unique-highest resident vote.");
        }
        else Check(contest.WinnerId is null, "An unfinished case election cannot invent a winner.");
    }

    private static void ValidateLocalJudge(TownLandCaseJudge judge, TownLandCase item) =>
        Check(judge is not null && Id(judge.AuthorityId) && judge.AssignedTick >= item.FiledTick &&
            (judge.Kind == "land_mayor" || judge.Kind == "case_elected" &&
                item.ContestHistory.Any(c => c.Id == judge.AuthorityId && c.Stage == "completed" && c.WinnerId == judge.AgentId && c.SettledTick <= judge.AssignedTick)),
            "A saved judge cannot invent a general or case-only mandate.");
    private static void ValidateLocalRulings(TownLandRecords state, string townId, TownLandCase item)
    {
        foreach (var ruling in item.Rulings)
        {
            var revision = item.Revisions.SingleOrDefault(r => r.Number == ruling.Revision);
            Check(Generated(ruling.Id, "land-ruling:", townId, state.Sequence) && revision is not null && ruling.Tick >= revision.PublishedTick &&
                TownLandHearingRules.IsValidOutcome(ruling.Outcome, ruling.Tick) && TownLandHearingRules.ValidText(ruling.Reasons) &&
                Canonical(ruling.EvidenceIds) && Canonical(ruling.LawIds) && ruling.AdjustmentIds is not null && ruling.Parties is { Count: > 0 } && ruling.Parties.All(p => p is not null &&
                    p.AdultIds is not null && CanonicalPeople(p.AdultIds)) &&
                !TownLandHearingRules.RequiresNewNotice(revision, revision.Tiles, revision.RightVersions.Select(r => r.Right), ruling.Parties) &&
                ruling.EvidenceIds.All(id => item.Evidence.Any(e => e.Id == id && e.SubmittedTick <= ruling.Tick)) &&
                ruling.LawIds.All(id => item.Evidence.Any(e => e.Kind == "record" && e.SourceRecordId == id && e.SubmittedTick <= ruling.Tick)) &&
                item.Reads.Any(r => r.AgentId == ruling.Judge.AgentId && r.Revision == ruling.Revision && r.ReadTick <= ruling.Tick &&
                    ruling.EvidenceIds.All(r.EvidenceIds.Contains)) &&
                !TownLandHearingRules.JudgeConflict(ruling.Judge.AgentId, null, ruling.Parties), "A saved ruling requires a reasoned, read and independent adjudication.");
            Check(ruling.Tick >= revision.DeadlineTick || ruling.Parties.All(p => p.Kind == "household" ? p.AdultIds.Count > 0 &&
                p.AdultIds.All(id => item.Responses.Any(r => r.Revision == revision.Number && r.PartyId == p.Id && r.AgentId == id && r.Tick <= ruling.Tick)) :
                item.Responses.Any(r => r.Revision == revision.Number && r.PartyId == p.Id && r.AgentId == p.RepresentativeId && r.Tick <= ruling.Tick)), "A saved ruling cannot omit the response window or actual early answers.");
            if (ruling.Outcome.Kind is "renew" or "amend" or "end" or "reclaim" or "grant")
                Check(ruling.AdjustmentIds.Count == 1 && ruling.EvidenceIds.Any(id => item.Evidence.Single(e => e.Id == id).Kind != "allegation") &&
                    (item.Property is not null || !ruling.Parties.Any(p => p.Kind == "household" && p.AdultIds.Count == 0 && state.Adjustments.Where(a => a.RulingId == ruling.Id)
                        .Any(a => TownLandHearingRules.IsAdverseChange(a.PriorRights.Select(v => v.Right), a.ResultRights, p.HouseholdId!)))) &&
                    ruling.AdjustmentIds.All(id => state.Adjustments.Any(a => a.Id == id && a.RulingId == ruling.Id && a.CaseId == item.Id)),
                    "An adverse saved ruling needs supported evidence, represented households and its exact adjustment.");
            else Check(ruling.AdjustmentIds.Count == 0, "Confirmation or rejection cannot secretly change use rights.");
        }
        Check(item.Status == "pending" ? item.SettledTick is null : item.Rulings.Count > 0 && item.SettledTick == item.Rulings[^1].Tick &&
            (item.Judge is null || item.ReopenRequests.Any(r => r.Status == "pending")), "A saved settled case must retain its ruling and closure time.");
    }
    private static void ValidateLocalReopening(TownLandRecords state, string townId, TownLandCase item)
    {
        foreach (var request in item.ReopenRequests)
        {
            Check(Generated(request.Id, "land-reopen:", townId, state.Sequence) && request.Tick >= item.FiledTick &&
                request.Kind is "material_evidence" or "procedural_error" && request.Status is "pending" or "accepted" or "rejected" &&
                Canonical(request.EvidenceIds) && request.EvidenceIds.Count > 0 && request.EvidenceIds.All(id => item.Evidence.Any(e => e.Id == id && e.SubmittedTick <= request.Tick)) &&
                TownLandHearingRules.ValidText(request.Reasons), "A saved reopening must retain identified grounds and evidence.");
            Check(request.Status == "pending" ? request.AssessedBy is null && request.AssessedTick is null && request.Assessment is null :
                request.AssessedBy is not null && request.AssessedTick >= request.Tick && TownLandHearingRules.ValidText(request.Assessment),
                "A saved reopening assessment requires recorded independent authority and reasons.");
            if (request.AssessedBy is { } assessor)
            {
                Check(item.Reads.Any(r => r.AgentId == assessor.AgentId && r.ReadTick <= request.AssessedTick &&
                    r.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal)), "A reopening assessment cannot inherit unread grounds.");
            }
            if (request.Status == "accepted")
                Check(item.Revisions.Any(r => r.PublishedTick == request.AssessedTick) &&
                    (request.Kind == "material_evidence" ? TownLandHearingRules.MaterialNewEvidence(item, request, state) : TownLandHearingRules.DemonstratedProceduralError(item, request)),
                    "An accepted reopening needs established grounds and a fresh full hearing notice.");
        }
    }
    private static void ValidateLocalAdjustments(string townId, TownLandRecords state)
    {
        foreach (var adjustment in state.Adjustments)
        {
            Check(Generated(adjustment.Id, "land-adjustment:", townId, state.Sequence) && adjustment.Tick >= 0 && adjustment.PriorRights is not null && adjustment.ResultRights is not null &&
                adjustment.Tiles is not null && adjustment.PriorRights.All(r => r is not null) && adjustment.ResultRights.All(r => r is not null),
                "A saved land adjustment is incomplete.");
            Unique(adjustment.PriorRights.Select(r => r.Id));
            Unique(adjustment.ResultRights.Select(r => r.Id));
            Check(adjustment.Tiles.Count > 0 && adjustment.Tiles.SequenceEqual(TownLandRightsRules.OrderTiles(adjustment.Tiles.Distinct())), "A land adjustment must be bounded to titled land.");
            Check(adjustment.ResultRights.SelectMany(r => r.Tiles).Distinct().Count() == adjustment.ResultRights.Sum(r => r.Tiles.Count),
                "A land adjustment cannot overlap its result permissions.");
            Check(adjustment.PriorRights.All(right => right.Right.GrantedTick <= adjustment.Tick) &&
                adjustment.ResultRights.All(right => right.GrantedTick <= adjustment.Tick), "A saved right snapshot must retain its exact known holder, plot, terms and version.");
            if (adjustment.Kind is "ruling" or "property_ruling")
            {
                var item = state.Cases.SingleOrDefault(c => c.Id == adjustment.CaseId);
                var ruling = item?.Rulings.SingleOrDefault(r => r.Id == adjustment.RulingId);
                Check(item is not null && ruling is not null && ruling.Tick == adjustment.Tick && ruling.AdjustmentIds.Contains(adjustment.Id, StringComparer.Ordinal) &&
                    adjustment.BuildingId is null && adjustment.TargetHouseholdId is null && adjustment.TransferId is null, "A ruling adjustment needs its exact durable case and ruling.");
                var revision = item.Revisions.Single(r => r.Number == ruling.Revision);
                Check(adjustment.Kind == (ruling.Outcome.Kind is "reclaim" or "grant" ? "property_ruling" : "ruling"),
                    "Physical property adjustments must retain their distinct noticed transfer authority.");
                Check(adjustment.Tiles.SequenceEqual(revision.Tiles), "A saved ruling must reproduce exactly its bounded permission change.");
            }
            else if (adjustment.Kind == "building_transfer")
            {
                Check(adjustment.CaseId is null && adjustment.RulingId is null && adjustment.TransferId is null && Id(adjustment.BuildingId) &&
                    adjustment.TargetHouseholdId is not null, "A building transfer needs its captured footprint and known beneficiary.");
            }
            else if (adjustment.Kind == "voluntary_transfer") TownLandTransferValidation.ValidateLocalAdjustment(state, adjustment);
            else throw new InvalidDataException("A saved land adjustment has an unsupported source.");
        }
        Check(state.Adjustments.Select(a => a.Tick).SequenceEqual(state.Adjustments.Select(a => a.Tick).Order()), "Land adjustments must retain chronological order.");
        Check(state.OriginalRights.All(r => state.Adjustments.Any(a => a.PriorRights.Any(p => p.Id == r.Id && p.Version == r.Version))),
            "An original permission snapshot cannot be orphaned from its adjustment chain.");
    }
}
