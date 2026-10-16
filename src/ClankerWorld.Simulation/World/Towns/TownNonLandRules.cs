using System.Globalization;

namespace ClankerWorld.Simulation.World;

/// <summary>Pure public files for allegations and civil findings, without physical enforcement.</summary>
public static class TownNonLandRules
{
    public const string CivilStandard = "more_likely_than_not";
    public static TownCaseRevision CurrentRevision(TownNonLandCase item) => item.Revisions[^1];
    public static string RevisionToken(TownNonLandCase item) => DerivedId.Create(item.Id + ":" + CurrentRevision(item).Number.ToString(CultureInfo.InvariantCulture));
    public static string CaseKey(string townId, TownViolationAllegation allegation) =>
        TownHearingProcedure.Digest(new { Town = townId, allegation.IncidentId, allegation.LawId, allegation.LawVersion });
    internal static TownNonLandHearingState Replace(TownNonLandHearingState state, TownNonLandCase item) =>
        state with { Cases = state.Cases.Select(c => c.Id == item.Id ? item : c).ToArray() };
    internal static string NextId(TownNonLandHearingState state, string kind, string? townId = null) =>
        DerivedId.Create("nonviolent-" + kind + ":" + (townId ?? state.Cases[0].TownId) + ":" + checked(state.Sequence + 1).ToString(CultureInfo.InvariantCulture));
    private static TownNonLandCase Exact(TownNonLandHearingState state, string id, int revision) =>
        state.Cases.SingleOrDefault(c => c.Id == id && CurrentRevision(c).Number == revision) ??
            throw new InvalidOperationException("This case notice has changed.");
    public static bool HasNoticeReceipt(TownCaseRevision revision, string actor, long tick, IReadOnlyList<TownCivicReceipt> receipts) =>
        TownHearingProcedure.HasNotice(revision.NoticeId, revision.PublishedTick, actor, tick, receipts);
    public static string ResponseToken(TownCaseResponse response) => TownHearingProcedure.Digest(response);
    public static bool ReadCurrent(TownNonLandCase item, string actor, long tick) => item.Reads.Any(r =>
        r.AgentId == actor && r.Revision == CurrentRevision(item).Number && r.ReadTick <= tick &&
        item.Evidence.All(e => e.SubmittedTick <= r.ReadTick && r.EvidenceIds.Contains(e.Id, StringComparer.Ordinal)) &&
        item.Responses.Where(response => response.Revision == r.Revision).All(response => response.Tick <= r.ReadTick && r.ResponseIds.Contains(ResponseToken(response))) &&
        item.Findings.All(finding => finding.Tick <= r.ReadTick && r.FindingIds.Contains(finding.Id)));
    public static bool JudgeConflict(string actor, string? household, IReadOnlyList<TownCaseParty> parties,
        IReadOnlySet<string>? directStakes = null) => TownHearingProcedure.Conflicted(actor, household,
            parties.SelectMany(p => new[] { p.SubjectId, p.RespondingAdultId ?? p.SubjectId }), parties.Select(p => p.HouseholdId), directStakes);
    public static bool CanCloseResponses(TownNonLandCase item, IReadOnlyList<TownCaseParty> parties, long tick) =>
        TownHearingProcedure.ResponsesClosed(CurrentRevision(item).DeadlineTick, tick,
            parties.Select(p => (p.Id, p.RespondingAdultId)), item.Responses.Where(r => r.Revision == CurrentRevision(item).Number)
                .Select(r => (r.PartyId, r.AgentId, r.Tick)));

    public static TownNonLandHearingState File(TownNonLandHearingState state, string townId, TownViolationAllegation allegation,
        TownViolationFiling filing, IReadOnlyList<TownCaseParty> parties, long tick, int day, string noticeId)
    {
        if (!ValidAllegation(allegation) || !IdentityRules.Reference(townId, int.MaxValue, requireTrimmed: true) || filing is null ||
            !IdentityRules.Reference(filing.AgentId, int.MaxValue, requireTrimmed: true) || filing.Kind is not ("witness" or "affected" or "town") ||
            !BoundedText.IsValid(filing.Statement, 1024, requireTrimmed: true) || filing.Tick != tick || allegation.ConductTick > tick ||
            filing.EvidenceIds is null || !filing.EvidenceIds.All(id => IdentityRules.Reference(id, requireTrimmed: true)) ||
            filing.Kind == "town" && !IdentityRules.Reference(filing.AuthorityId, int.MaxValue, requireTrimmed: true) || !ValidParties(parties) ||
            !parties.Any(p => p.SubjectId == allegation.SubjectId && p.Role == "subject") || day <= 0 || !IdentityRules.Reference(noticeId, int.MaxValue, requireTrimmed: true))
            throw new InvalidOperationException("A report needs specific conduct, its applicable law, known sources and affected people.");
        if (FilingRefusal(state, townId, allegation) is { } refusal) throw new InvalidOperationException(refusal);
        var key = CaseKey(townId, allegation);
        if (state.Cases.SingleOrDefault(c => c.Key == key) is { } old)
        {
            return Replace(state, old with { Filings = old.Filings.Contains(filing) ? old.Filings : old.Filings.Append(filing).ToArray() });
        }
        var revision = new TownCaseRevision(1, parties.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray(), noticeId, tick, checked(tick + day));
        var item = new TownNonLandCase(NextId(state, "case", townId), key, townId, allegation, tick, "pending", [revision], [filing],
            [], [], [], [], null, [], [], null, [], [], TownHearingProcedure.Ordered(parties.Select(p => p.SubjectId).Append(filing.AgentId)));
        return state with { Sequence = checked(state.Sequence + 1), Cases = state.Cases.Append(item).ToArray() };
    }

    public static string? FilingRefusal(TownNonLandHearingState state, string townId, TownViolationAllegation allegation)
    {
        if (state.Cases.SingleOrDefault(item => item.Key == CaseKey(townId, allegation)) is not { } old) return null;
        if (old.Allegation.SubjectId != allegation.SubjectId || old.Allegation.ConductTick != allegation.ConductTick ||
            old.Allegation.Position != allegation.Position || old.Allegation.ConductKind != allegation.ConductKind)
            return "The same incident cannot silently identify different conduct.";
        return old.Status != "pending" ? "A settled act requires grounded reopening, not another report." : null;
    }

    internal static bool ValidAllegation(TownViolationAllegation? a) => a is not null && IdentityRules.Reference(a.IncidentId, int.MaxValue, requireTrimmed: true) &&
IdentityRules.Reference(a.SubjectId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(a.ConductKind, int.MaxValue, requireTrimmed: true) && a.ConductTick >= 0 &&
IdentityRules.Reference(a.LawId, int.MaxValue, requireTrimmed: true) && a.LawVersion > 0 && BoundedText.IsValid(a.Statement, 1024, requireTrimmed: true) &&
        a.SourceEvidenceIds is not null && a.SourceEvidenceIds.All(id => IdentityRules.Reference(id, requireTrimmed: true));
    internal static bool ValidParties(IReadOnlyList<TownCaseParty>? parties) => parties is { Count: > 0 } &&
        parties.All(p => p is not null && IdentityRules.Reference(p.Id, int.MaxValue, requireTrimmed: true) && p.Role is "subject" or "affected" or "town" &&
IdentityRules.Reference(p.SubjectId, int.MaxValue, requireTrimmed: true) && (p.RespondingAdultId is null || IdentityRules.Reference(p.RespondingAdultId, int.MaxValue, requireTrimmed: true)) &&
            (p.CareRelationshipId is null ? p.CareRevision is null : IdentityRules.Reference(p.CareRelationshipId, int.MaxValue, requireTrimmed: true) && p.CareRevision > 0) &&
            (p.HouseholdId is null || IdentityRules.Reference(p.HouseholdId, int.MaxValue, requireTrimmed: true))) &&
        parties.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() == parties.Count;
    public static bool RequiresNewNotice(TownCaseRevision old, IReadOnlyList<TownCaseParty> parties) =>
        parties.Any(p => !old.Parties.Contains(p));
    public static TownNonLandHearingState ReviseParties(TownNonLandHearingState state, string caseId,
        IReadOnlyList<TownCaseParty> parties, long tick, int day, string noticeId)
    {
        var item = state.Cases.Single(c => c.Id == caseId && c.Status == "pending");
        if (!ValidParties(parties) || day <= 0 || !IdentityRules.Reference(noticeId, int.MaxValue, requireTrimmed: true)) throw new InvalidOperationException("A notice needs current legitimate parties.");
        var old = CurrentRevision(item);
        return !RequiresNewNotice(old, parties) ? state : Replace(state, item with
        {
            Revisions = item.Revisions.Append(
            new TownCaseRevision(old.Number + 1, parties.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray(), noticeId, tick, checked(tick + day))).ToArray()
        });
    }
    public static TownNonLandHearingState Inspect(TownNonLandHearingState state, string caseId, int revision, string actor, long tick)
    {
        var item = Exact(state, caseId, revision);
        if (!IdentityRules.Reference(actor, int.MaxValue, requireTrimmed: true) || tick < CurrentRevision(item).PublishedTick) throw new InvalidOperationException("Read the published case first.");
        return Replace(state, item with
        {
            Reads = item.Reads.Append(new TownCaseRead(revision, actor, tick,
            TownHearingProcedure.Ordered(item.Evidence.Select(e => e.Id)), TownHearingProcedure.Ordered(item.ReopenRequests.Select(r => r.Id)))
            {
                ResponseIds = TownHearingProcedure.Ordered(item.Responses.Where(r => r.Revision == revision).Select(ResponseToken)),
                FindingIds = TownHearingProcedure.Ordered(item.Findings.Select(f => f.Id))
            }).ToArray()
        });
    }
    public static TownNonLandHearingState RelayRead(TownNonLandHearingState state, string caseId, int revision, string source, string recipient, long tick, TownCaseRead? selectedRead = null)
    {
        var item = Exact(state, caseId, revision);
        var reads = item.Reads.Where(r => r.Revision == revision && r.ReadTick <= tick && r.AgentId == source).ToArray();
        if (source == recipient || reads.Length == 0) throw new InvalidOperationException("Only an informed person can relay this case.");
        if (selectedRead is not null)
        {
            if (RelayReadRefusal(item, source, selectedRead, tick) is { } refusal) throw new InvalidOperationException(refusal);
            reads = [selectedRead];
        }
        reads = reads.Concat(item.Reads.Where(r => r.Revision == revision && r.ReadTick <= tick && r.AgentId == recipient)).ToArray();
        return Replace(state, item with
        {
            Reads = item.Reads.Append(new TownCaseRead(revision, recipient, tick,
            TownHearingProcedure.Ordered(reads.SelectMany(r => r.EvidenceIds)), TownHearingProcedure.Ordered(reads.SelectMany(r => r.ReopenRequestIds)), source)
            {
                ResponseIds = TownHearingProcedure.Ordered(reads.SelectMany(r => r.ResponseIds)),
                FindingIds = TownHearingProcedure.Ordered(reads.SelectMany(r => r.FindingIds))
            }).ToArray()
        });
    }
    public static string? RelayReadRefusal(TownNonLandCase item, string source, TownCaseRead selectedRead, long tick)
    {
        var reads = item.Reads.Where(read => read.AgentId == source && read.Revision == CurrentRevision(item).Number && read.ReadTick <= tick).ToArray();
        return selectedRead.AgentId != source || selectedRead.Revision != CurrentRevision(item).Number || reads.Length == 0 ||
            selectedRead.EvidenceIds.Except(reads.SelectMany(read => read.EvidenceIds), StringComparer.Ordinal).Any() ||
            selectedRead.ReopenRequestIds.Except(reads.SelectMany(read => read.ReopenRequestIds), StringComparer.Ordinal).Any() ||
            selectedRead.ResponseIds.Except(reads.SelectMany(read => read.ResponseIds), StringComparer.Ordinal).Any() ||
            selectedRead.FindingIds.Except(reads.SelectMany(read => read.FindingIds), StringComparer.Ordinal).Any()
                ? "The selected material was not read by this source." : null;
    }

    public static TownNonLandHearingState Respond(TownNonLandHearingState state, string caseId, int revision, string actor,
        string representedActor, string partyId, string kind, string text, long tick, IReadOnlyList<TownCivicReceipt> receipts,
        IReadOnlyList<TownCaseParty> currentParties)
    {
        var item = Exact(state, caseId, revision);
        var party = currentParties.SingleOrDefault(p => p.Id == partyId && p.SubjectId == representedActor && p.RespondingAdultId == actor);
        if (item.Status != "pending" || party is null || !CurrentRevision(item).Parties.Contains(party) || kind is not ("answer" or "waive") ||
            !BoundedText.IsValid(text, 1024, requireTrimmed: true) || !HasNoticeReceipt(CurrentRevision(item), actor, tick, receipts))
            throw new InvalidOperationException("An answer requires the current person's own informed response or legitimate caregiver support.");
        var response = new TownCaseResponse(revision, partyId, actor, representedActor, kind, text, tick);
        return item.Responses.Contains(response) ? state : Replace(state, item with { Responses = item.Responses.Append(response).ToArray() });
    }
    public static TownNonLandHearingState AddEvidence(TownNonLandHearingState state, string caseId, int revision,
        TownCaseEvidence evidence, IReadOnlyList<TownCivicReceipt> receipts)
    {
        var item = Exact(state, caseId, revision);
        if (!ValidEvidence(evidence) || evidence.Revision != revision || item.Evidence.Any(e => e.Id == evidence.Id) ||
            !HasNoticeReceipt(CurrentRevision(item), evidence.SubmittedByAgentId, evidence.SubmittedTick, receipts))
            throw new InvalidOperationException("Evidence requires a sourced, informed submission to this case.");
        return Replace(state, item with { Evidence = item.Evidence.Append(evidence).ToArray() });
    }
    internal static bool ValidEvidence(TownCaseEvidence? e) => e is not null && IdentityRules.Reference(e.Id, int.MaxValue, requireTrimmed: true) && e.Revision > 0 &&
BoundedText.IsValid(e.Text, 1024, requireTrimmed: true) && IdentityRules.Reference(e.SourceAgentId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(e.SubmittedByAgentId, int.MaxValue, requireTrimmed: true) &&
        e.ObservedTick >= 0 && e.SubmittedTick >= e.ObservedTick &&
        (e.Kind == "allegation" && e.Acquisition is "statement" or "relay" &&
            (e.SourceRecordId is null && e.SourceVersion is null || IdentityRules.Reference(e.SourceRecordId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(e.SourceVersion, int.MaxValue, requireTrimmed: true)) ||
         e.Kind == "observation" && e.Acquisition == "firsthand" && e.SourceAgentId == e.SubmittedByAgentId &&
            (e.SourceRecordId is null && e.SourceVersion is null || IdentityRules.Reference(e.SourceRecordId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(e.SourceVersion, int.MaxValue, requireTrimmed: true)) ||
         e.Kind == "record" && e.Acquisition == "record_inspection" && e.SourceAgentId == e.SubmittedByAgentId &&
IdentityRules.Reference(e.SourceRecordId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(e.SourceVersion, int.MaxValue, requireTrimmed: true));

    internal static bool SupportsConduct(TownNonLandCase item, TownCaseEvidence evidence) =>
        evidence.Kind is "observation" or "record" && evidence.SourceRecordId == item.Allegation.IncidentId &&
IdentityRules.Reference(evidence.SourceVersion, int.MaxValue, requireTrimmed: true);

    public static TownNonLandHearingState AssignJudge(TownNonLandHearingState state, string caseId, TownCaseJudge judge)
    {
        if (judge.Kind is not ("non_land_mayor" or "case_elected") || !IdentityRules.Reference(judge.AuthorityId, int.MaxValue, requireTrimmed: true) || !IdentityRules.Reference(judge.AgentId, int.MaxValue, requireTrimmed: true))
            throw new InvalidOperationException("A formal case requires its explicit non-land authority.");
        var item = TownCaseJudgeRules.CancelContest(state.Cases.Single(c => c.Id == caseId), judge.AssignedTick, "authorized_judge_available");
        return Replace(state, item with { Judge = judge });
    }
    public static TownNonLandHearingState InvalidateJudge(TownNonLandHearingState state, string caseId, long tick, string reason)
    {
        var item = state.Cases.Single(c => c.Id == caseId);
        return item.Judge is null ? state : Replace(state, item with
        {
            Judge = null,
            JudgeHistory = item.JudgeHistory.Append(new(item.Judge, tick, reason)).ToArray()
        });
    }
    public static TownNonLandHearingState Rule(TownNonLandHearingState state, string caseId, int revision, TownCaseJudge judge,
        long tick, string result, string consequence, IReadOnlyList<string> evidenceIds, string reasons, string uncertainty,
        IReadOnlyList<TownCaseParty> currentParties, bool validAuthority, bool crediblePriorNotice = false)
    {
        var item = Exact(state, caseId, revision);
        if (item.Status != "pending" || !validAuthority || item.Judge != judge ||
            JudgeConflict(judge.AgentId, null, currentParties, item.DirectStakeIds.ToHashSet(StringComparer.Ordinal)) ||
            RequiresNewNotice(CurrentRevision(item), currentParties) || !CanCloseResponses(item, currentParties, tick) ||
            !ReadCurrent(item, judge.AgentId, tick) || !BoundedText.IsValid(reasons, 1024, requireTrimmed: true) || !BoundedText.IsValid(uncertainty, 1024, requireTrimmed: true) ||
            result is not ("supported" or "unsupported") || consequence is not ("none" or "explanation" or "warning" or "censure") ||
            consequence == "censure" && !crediblePriorNotice ||
            result == "unsupported" && consequence != "none" || evidenceIds.Any(id => !item.Evidence.Any(e => e.Id == id)) ||
            result == "supported" && (currentParties.Any(p => p.RespondingAdultId is null) || !evidenceIds.Any(id =>
                item.Evidence.Any(e => e.Id == id && SupportsConduct(item, e)))))
            throw new InvalidOperationException("A finding needs current independent authority, a fair hearing and more than unsupported allegations.");
        var finding = new TownViolationFinding(NextId(state, "finding"), revision, judge, tick, result, CivilStandard,
            TownHearingProcedure.Ordered(evidenceIds), reasons, uncertainty, consequence, currentParties.ToArray());
        item = TownCaseJudgeRules.CancelContest(item, tick, "case_closed");
        return Replace(state with { Sequence = checked(state.Sequence + 1) }, item with
        {
            Status = "settled",
            SettledTick = tick,
            Findings = item.Findings.Append(finding).ToArray(),
            Judge = null,
            JudgeHistory = item.JudgeHistory.Append(new(judge, tick, "case_closed")).ToArray()
        });
    }

    public static TownNonLandHearingState RequestReopen(TownNonLandHearingState state, string caseId, string actor,
        string kind, IReadOnlyList<string> evidenceIds, string reasons, long tick)
    {
        var item = state.Cases.Single(c => c.Id == caseId && c.Status == "settled");
        if (kind is not ("material_evidence" or "procedural_error") || !BoundedText.IsValid(reasons, 1024, requireTrimmed: true) || evidenceIds.Count == 0 ||
            evidenceIds.Any(id => !item.Evidence.Any(e => e.Id == id))) throw new InvalidOperationException("Reopening needs identified evidence or a demonstrated procedural error.");
        var request = new TownCaseReopenRequest(NextId(state, "reopen"), actor, tick, kind, TownHearingProcedure.Ordered(evidenceIds), reasons);
        return Replace(state with { Sequence = checked(state.Sequence + 1) }, item with { ReopenRequests = item.ReopenRequests.Append(request).ToArray() });
    }
    public static bool MaterialNewEvidence(TownNonLandCase item, TownCaseReopenRequest request)
    {
        var finding = item.Findings.LastOrDefault(r => r.Tick <= request.Tick);
        if (finding is null) return false;
        var known = item.Evidence.Where(e => e.SubmittedTick <= finding.Tick).ToArray();
        return request.EvidenceIds.Any(id => item.Evidence.Any(e => e.Id == id && e.Kind is "record" or "observation" &&
            e.SubmittedTick > finding.Tick && e.SubmittedTick <= request.Tick &&
            !known.Any(old => old.Kind == e.Kind && TownHearingProcedure.FactKey(old.Text) == TownHearingProcedure.FactKey(e.Text)) &&
            !item.Findings.Any(f => f.Id == e.SourceRecordId) &&
            !item.ReopenRequests.Any(r => r.Id == e.SourceRecordId) &&
            !known.Any(old => e.SourceRecordId is not null && old.SourceRecordId == e.SourceRecordId && old.SourceVersion == e.SourceVersion &&
                (e.Kind == "record" || old.SourceAgentId == e.SourceAgentId))));
    }
    public static bool DemonstratedProceduralError(TownNonLandCase item, TownCaseReopenRequest request) =>
        item.Findings.Where(f => f.Tick <= request.Tick).Any(f =>
            !TownHearingProcedure.ResponsesClosed(item.Revisions.Single(r => r.Number == f.Revision).DeadlineTick, f.Tick,
                f.Parties.Select(p => (p.Id, p.RespondingAdultId)), item.Responses.Where(r => r.Revision == f.Revision).Select(r => (r.PartyId, r.AgentId, r.Tick))) ||
            JudgeConflict(f.Judge.AgentId, null, f.Parties) || !item.Reads.Any(r => r.AgentId == f.Judge.AgentId &&
                r.Revision == f.Revision && r.ReadTick <= f.Tick && f.EvidenceIds.All(r.EvidenceIds.Contains)));
    public static TownNonLandHearingState Reopen(TownNonLandHearingState state, string caseId, string requestId, TownCaseJudge judge,
        bool groundsEstablished, string assessment, IReadOnlyList<TownCaseParty> currentParties,
        long tick, int day, string noticeId, bool validAuthority)
    {
        var item = state.Cases.Single(c => c.Id == caseId && c.Status == "settled");
        var request = item.ReopenRequests.Single(r => r.Id == requestId && r.Status == "pending");
        if (!validAuthority || item.Judge != judge || !BoundedText.IsValid(assessment, 1024, requireTrimmed: true) ||
            !ValidParties(currentParties) || day <= 0 || !IdentityRules.Reference(noticeId, int.MaxValue, requireTrimmed: true) || tick < request.Tick ||
            !item.Reads.Any(r => r.AgentId == judge.AgentId && r.Revision == CurrentRevision(item).Number &&
                r.ReadTick >= request.Tick && r.ReadTick <= tick && r.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal)) ||
            groundsEstablished && !(request.Kind == "material_evidence" ? MaterialNewEvidence(item, request) : DemonstratedProceduralError(item, request)))
            throw new InvalidOperationException("An informed independent judge must establish the reopening grounds.");
        request = request with { Status = groundsEstablished ? "accepted" : "rejected", AssessedBy = judge, AssessedTick = tick, Assessment = assessment };
        item = item with { ReopenRequests = item.ReopenRequests.Select(r => r.Id == requestId ? request : r).ToArray() };
        if (groundsEstablished)
            item = item with
            {
                Status = "pending",
                SettledTick = null,
                Revisions = item.Revisions.Append(new TownCaseRevision(
                CurrentRevision(item).Number + 1, currentParties.ToArray(), noticeId, tick, checked(tick + day))).ToArray()
            };
        else if (!item.ReopenRequests.Any(r => r.Status == "pending"))
            item = item with { Judge = null, JudgeHistory = item.JudgeHistory.Append(new(judge, tick, "reopening_rejected")).ToArray() };
        return Replace(state, item);
    }
}
