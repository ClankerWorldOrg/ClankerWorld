using System.Globalization;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static bool ValidCouncilGeneratedId(string id, string prefix, long sequence) => DerivedId.MatchesNumbered(id, prefix, sequence);
    internal static bool UniqueCouncilIds(IEnumerable<string> ids) => ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count();

    internal static void ValidateLocalCouncil(string townId, TownCouncilState state)
    {
        if (state.Members is null || state.Candidates is null || state.Proposals is null ||
            state.ElectionHistory is null || state.Notices is null || state.Knowledge is null ||
            state.Form is not ("all_adult" or "representative" or "leader") || state.Fallback is not ("initial" or "none" or "demographic" or "candidates" or "arrangement") ||
            state.Revision < 0 || state.Sequence < 0 || state.RetryTick < 0 || state.RetryCircumstances is null ||
            state.RetryCircumstances.Length > 64 || !UniqueCouncilIds(state.Members) ||
            state.Form == "representative" && (state.Members.Count > TownCouncilRules.Seats || state.TermEndTick is null) ||
            state.Form == "leader" && (state.Members.Count != 1 || state.TermEndTick is not null) ||
            state.TermEndTick is < 0 || state.Candidates.Any(c => c is null ||
                !c.FullTerm && c.RemainderTermEndTick is null || c.RemainderTermEndTick is < 0) ||
            !UniqueCouncilIds(state.Candidates.Select(c => c.AgentId)) || state.Proposals.Any(p => p is null) ||
            !UniqueCouncilIds(state.Proposals.Select(p => p.Id)) || state.ElectionHistory.Any(e => e is null) ||
            !UniqueCouncilIds(state.ElectionHistory.Select(e => e.Id).Concat(state.Election is { } live ? [live.Id] : [])) ||
            state.Notices.Any(n => n is null) || !UniqueCouncilIds(state.Notices.Select(n => n.Id)) ||
            state.Knowledge.Any(k => k is null) ||
            state.Knowledge.Select(k => (k.AgentId, k.NoticeId)).Distinct().Count() != state.Knowledge.Count)
            throw new InvalidDataException("A Town's saved council, candidates or civic ledger is invalid.");
        foreach (var proposal in state.Proposals)
        {
            if (!ValidCouncilGeneratedId(proposal.Id, townId + ":proposal:", state.Sequence) ||
                string.IsNullOrWhiteSpace(proposal.RequestKey) || proposal.Kind is not ("law" or "admission" or "land_claim" or "land_use" or "land_hearing" or "project" or "law_case") ||
                proposal.Kind == "land_use" && string.IsNullOrWhiteSpace(proposal.SubjectId) ||
                proposal.Kind == "admission" && (proposal.SubjectId is null) ||
                proposal.Kind is "law" or "land_claim" or "land_hearing" or "project" or "law_case" && proposal.SubjectId is not null ||
                proposal.Kind == "land_hearing" && (!TownLandGovernmentFilingRules.IsValid(proposal.LandHearingRequest, proposal.OpenedTick) ||
                    proposal.RequestKey != TownLandGovernmentFilingRules.RequestKey(proposal.LandHearingRequest!)) ||
                proposal.Kind != "land_hearing" && proposal.LandHearingRequest is not null ||
                proposal.Kind == "law_case" && (!TownNonLandGovernmentFilingRules.IsValid(proposal.NonLandRequest, proposal.OpenedTick) ||
                    proposal.RequestKey != TownNonLandGovernmentFilingRules.RequestKey(proposal.NonLandRequest!)) ||
                proposal.Kind != "law_case" && proposal.NonLandRequest is not null ||
                proposal.Kind == "land_claim" && proposal.LandClaimTiles is not { Count: > 0 } ||
                proposal.Kind != "land_claim" && proposal.LandClaimTiles is not null ||
                string.IsNullOrWhiteSpace(proposal.Text) || proposal.Text.Length > TownCouncilRules.MaximumProposalText || proposal.Text.Any(char.IsControl) ||
                proposal.Circumstances is null || proposal.Circumstances.Length > 512 || proposal.CouncilRevision < 0 || proposal.CouncilRevision > state.Revision ||
                proposal.OpenedTick < 0 || proposal.DeadlineTick <= proposal.OpenedTick ||
                proposal.Voters is null || proposal.Votes is null || !UniqueCouncilIds(proposal.Voters) ||
                proposal.RequiredYes < 1 || proposal.RequiredYes > Math.Max(2, proposal.Voters.Count) ||
                // Zero/one-voter histories can come from either governing
                // form; other rosters determine the threshold unambiguously.
                proposal.Voters.Count >= 2 && proposal.RequiredYes != proposal.Voters.Count / 2 + 1 ||
                proposal.Votes.Any(v => v is null || !proposal.Voters.Contains(v.AgentId, StringComparer.Ordinal) ||
                    proposal.Kind == "admission" && v.AgentId == proposal.SubjectId) || !UniqueCouncilIds(proposal.Votes.Select(v => v.AgentId)) ||
                proposal.Status is not ("pending" or "passed" or "rejected" or "cancelled" or "withdrawn") ||
                proposal.Status == "pending" && (proposal.SettledTick is not null ||
                    proposal.CouncilRevision != state.Revision || !proposal.Voters.SequenceEqual(state.Members) ||
                    proposal.RequiredYes != (state.Form == "representative" ? 2 : state.Members.Count / 2 + 1) ||
                    proposal.Votes.Count(v => v.Yes) >= proposal.RequiredYes) ||
                proposal.Status != "pending" && (proposal.SettledTick is null || proposal.SettledTick < proposal.OpenedTick) ||
                proposal.Status == "passed" && proposal.Votes.Count(v => v.Yes) < proposal.RequiredYes ||
                proposal.Status == "rejected" && proposal.Votes.Count(v => v.Yes) >= proposal.RequiredYes)
                throw new InvalidDataException("A Town's saved proposal or final votes are invalid.");
            if (proposal.Kind == "project")
            {
                TownProjectRules.ValidatePayload(proposal.Project);
                if (proposal.RequestKey != TownProjectRules.RequestKey(proposal.Project!) ||
                    proposal.Text != TownProjectRules.ProposalText(proposal.Project!))
                    throw new InvalidDataException("A Town's construction proposal differs from its named site and budget.");
            }
            else if (proposal.Project is not null)
                throw new InvalidDataException("A law or admission proposal cannot contain construction authority.");
        }
        if (state.Proposals.Where(p => p.Status == "pending").GroupBy(p => p.RequestKey, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new InvalidDataException("Equivalent pending Town proposals must share a single window.");
        foreach (var election in state.ElectionHistory.Concat(state.Election is { } current ? [current] : []))
        {
            var active = state.Election?.Id == election.Id;
            if (!ValidCouncilGeneratedId(election.Id, townId + ":election:", state.Sequence) ||
                election.Kind is not ("initial" or "regular" or "replacement") ||
                election.Stage is not ("main" or "runoff" or "ready" or "completed" or "failed" or "cancelled") ||
                active != (election.Stage is "main" or "runoff" or "ready") || election.OpenedTick < 0 ||
                election.DeadlineTick <= election.OpenedTick || election.TermEndTick < 0 || election.Seats is < 1 or > TownCouncilRules.Seats ||
                election.Voters is null || election.Candidates is null || election.Ballots is null || election.Supported is null ||
                election.SettledSeats is null || election.DrawOrder is null || !UniqueCouncilIds(election.Voters) || !UniqueCouncilIds(election.Candidates) ||
                !UniqueCouncilIds(election.Supported) || !UniqueCouncilIds(election.SettledSeats) || !UniqueCouncilIds(election.DrawOrder) ||
                election.Ballots.Any(b => b is null || b.Choices is null || !election.Voters.Contains(b.AgentId, StringComparer.Ordinal) ||
                    b.Choices.Count > election.Seats || !UniqueCouncilIds(b.Choices) || b.Choices.Any(id => !election.Candidates.Contains(id, StringComparer.Ordinal))) ||
                !UniqueCouncilIds(election.Ballots.Select(b => b.AgentId)) ||
                election.SettledSeats.Any(id => !election.Supported.Contains(id, StringComparer.Ordinal)) ||
                election.DrawOrder.Any(id => !election.Supported.Contains(id, StringComparer.Ordinal)) ||
                active && (election.Stage == "ready" && election.SettledSeats.Count != TownCouncilRules.Seats ||
                    election.Candidates.Concat(election.SettledSeats).Any(id => !state.Candidates.Any(c => c.AgentId == id &&
                        (c.FullTerm || election.Kind == "replacement" && c.RemainderTermEndTick == election.TermEndTick)))))
                throw new InvalidDataException("A Town's saved election, consent, ballots or recorded draw is invalid.");
        }
        for (var index = 0; index < state.Notices.Count; index++)
        {
            var notice = state.Notices[index];
            if (notice.Id != "notice:" + (index + 1).ToString(CultureInfo.InvariantCulture) || string.IsNullOrWhiteSpace(notice.SubjectId) ||
                notice.Kind is not ("council" or "candidate" or "nomination" or "election" or "runoff" or "result" or "proposal" or "cancelled" or
                    "law" or "government" or "mayor" or "land_use" or "land_hearing" or "land_transfer" or "law_case" or "remedy" or "nonviolent_hearing") ||
                string.IsNullOrWhiteSpace(notice.Text) || notice.Text.Length > 32768 || notice.PostedTick < 0)
                throw new InvalidDataException("A saved Town civic notice is invalid.");
        }
        ValidateLocalCivicKnowledge(state);
    }

    private static void ValidateLocalCivicKnowledge(TownCouncilState state)
    {
        var notices = state.Notices.ToDictionary(notice => notice.Id, StringComparer.Ordinal);
        var receipts = state.Knowledge.ToDictionary(receipt => (receipt.AgentId, receipt.NoticeId));
        foreach (var receipt in state.Knowledge)
            if (string.IsNullOrWhiteSpace(receipt.NoticeId) ||
                !notices.TryGetValue(receipt.NoticeId, out var notice) || notice.PostedTick > receipt.LearnedTick ||
                receipt.LearnedTick < 0 || receipt.SourceAgentId is { } source &&
                    (source == receipt.AgentId ||
                     !receipts.TryGetValue((source, receipt.NoticeId), out var sourceReceipt) ||
                     sourceReceipt.LearnedTick > receipt.LearnedTick))
                throw new InvalidDataException("Saved civic knowledge requires an actual notice or informed relay.");

        // Same-tick relays are valid, but every chain must reach a direct read.
        // Each receipt enters one path at most once; indexed lookups keep long
        // saved ledgers linear and avoid recursion for long relay chains.
        var rooted = new HashSet<(string AgentId, string NoticeId)>();
        foreach (var receipt in state.Knowledge)
        {
            var key = (receipt.AgentId, receipt.NoticeId);
            if (rooted.Contains(key)) continue;
            var visiting = new HashSet<(string AgentId, string NoticeId)>();
            while (!rooted.Contains(key))
            {
                if (!visiting.Add(key))
                    throw new InvalidDataException("Saved civic relays must lead back to an actual notice read.");
                var current = receipts[key];
                if (current.SourceAgentId is not { } source) break;
                key = (source, current.NoticeId);
            }
            rooted.UnionWith(visiting);
        }
    }

}
