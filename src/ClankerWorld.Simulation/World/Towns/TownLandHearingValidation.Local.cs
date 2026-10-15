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
}
