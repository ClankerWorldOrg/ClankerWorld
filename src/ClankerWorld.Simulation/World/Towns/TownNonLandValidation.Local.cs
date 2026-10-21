using System.Diagnostics.CodeAnalysis;

namespace ClankerWorld.Simulation.World;

public static partial class TownNonLandValidation
{
    internal static void ValidateLocal(string townId, [NotNull] TownNonLandHearingState? state)
    {
        Check(state is not null && state.Sequence >= 0 && state.Cases is not null && state.Offers is not null &&
            state.Agreements is not null && state.Effects is not null, "The Town must retain its nonviolent case and remedy records.");
        Check(state.Cases.All(c => c is not null) && state.Offers.All(o => o is not null) &&
            state.Agreements.All(a => a is not null) && state.Effects.All(e => e is not null), "Nonviolent records cannot contain null entries.");
        // Establish the shape of every file before any cross-case reference lookup.
        foreach (var item in state.Cases) Shape(item);
        foreach (var item in state.Cases) ValidateLocalCase(townId, item);
        foreach (var offer in state.Offers)
            Check(IdentityRules.Reference(offer.Id, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(offer.CaseId, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(offer.FindingId, DerivedId.MaximumLength, requireTrimmed: true) && TownRemedyRules.ValidTerms(offer.Terms) &&
                offer.Responses is not null && offer.Responses.All(r => r is not null), "A remedy offer must retain its typed terms and responses.");
        foreach (var agreement in state.Agreements)
            Check(IdentityRules.Reference(agreement.Id, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(agreement.OfferId, DerivedId.MaximumLength, requireTrimmed: true) && TownRemedyRules.ValidTerms(agreement.Terms) &&
                agreement.Consents is not null && agreement.Consents.All(r => r is not null), "An agreement must retain its terms and personal consents.");
        foreach (var effect in state.Effects)
            Check(IdentityRules.Reference(effect.Id, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(effect.AgreementId, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(effect.TermId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(effect.ActorId, int.MaxValue, requireTrimmed: true) &&
                IdentityRules.Reference(effect.NativeReceiptId, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(effect.NativeReceiptVersion, int.MaxValue, requireTrimmed: true), "A remedy effect needs actual named physical provenance.");
        Unique(state.Cases.Select(c => c.Key));
        Unique(state.Cases.Select(c => c.Id).Concat(state.Cases.SelectMany(c => c.Findings.Select(f => f.Id)))
            .Concat(state.Cases.SelectMany(c => c.ReopenRequests.Select(r => r.Id))).Concat(state.Offers.Select(o => o.Id.Value))
            .Concat(state.Agreements.Select(a => a.Id)).Concat(state.Effects.Select(e => e.Id)));
        Check(state.Sequence >= state.Cases.Count + state.Cases.Sum(c => c.Findings.Count + c.ReopenRequests.Count) +
            state.Offers.Count + state.Agreements.Count, "The nonviolent record sequence cannot precede its saved records.");
    }

    private static void Shape(TownNonLandCase item)
    {
        Check(IdentityRules.Reference(item.Id, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(item.Key, int.MaxValue, requireTrimmed: true) && IdentityRules.Reference(item.TownId, int.MaxValue, requireTrimmed: true) && TownNonLandRules.ValidAllegation(item.Allegation) &&
            item.Revisions is { Count: > 0 } && item.Filings is { Count: > 0 } && item.Evidence is not null && item.Reads is not null &&
            item.Responses is not null && item.Findings is not null && item.JudgeHistory is not null && item.JudgeConsents is not null &&
            item.ContestHistory is not null && item.ReopenRequests is not null && Canonical(item.DirectStakeIds), "A case must retain its complete file.");
        Check(item.Revisions.All(r => r is not null && TownNonLandRules.ValidParties(r.Parties)) &&
            item.Filings.All(f => f is not null && f.EvidenceIds is not null) &&
            item.Evidence.All(e => e is not null) && item.Reads.All(r => r is not null && r.EvidenceIds is not null && r.ReopenRequestIds is not null && r.ResponseIds is not null && r.FindingIds is not null) &&
            item.Responses.All(r => r is not null) && item.Findings.All(f => f is not null && IdentityRules.Reference(f.Id, DerivedId.MaximumLength, requireTrimmed: true) && f.Judge is not null &&
                f.EvidenceIds is not null && TownNonLandRules.ValidParties(f.Parties)) &&
            item.JudgeHistory.All(h => h is not null && h.Judge is not null) && item.JudgeConsents.All(c => c is not null) &&
            item.ContestHistory.All(c => c is not null) && item.ReopenRequests.All(r => r is not null && IdentityRules.Reference(r.Id, DerivedId.MaximumLength, requireTrimmed: true) && r.EvidenceIds is not null),
            "A nonviolent case cannot contain incomplete file entries.");
        Check(item.Revisions.Select(r => r.Number).SequenceEqual(Enumerable.Range(1, item.Revisions.Count)) &&
            item.Revisions[0].PublishedTick == item.FiledTick && OrderedTimes(item.Revisions.Select(r => r.PublishedTick)), "Hearing revisions must retain their original and later notice windows.");
    }

    private static void ValidateLocalCase(string townId, TownNonLandCase item)
    {
        Check(item.TownId == townId && item.Key == TownNonLandRules.CaseKey(townId, item.Allegation) &&
            item.Status is "pending" or "settled" && item.FiledTick >= item.Allegation.ConductTick,
            "A saved allegation must identify its actual Town, person, place and time.");
        foreach (var filing in item.Filings)
            Check(IdentityRules.Reference(filing.AgentId, int.MaxValue, requireTrimmed: true) && filing.Kind is "witness" or "affected" or "town" && BoundedText.IsValid(filing.Statement, 1024, requireTrimmed: true) &&
                filing.Tick >= item.FiledTick && Canonical(filing.EvidenceIds), "A filing must retain a specific informed reporter and source.");
        foreach (var evidence in item.Evidence)
            Check(TownNonLandRules.ValidEvidence(evidence), "Evidence must preserve its source and actual informed submission.");
        foreach (var response in item.Responses)
        {
            var revision = item.Revisions.SingleOrDefault(r => r.Number == response.Revision);
            var party = revision?.Parties.SingleOrDefault(p => p.Id == response.PartyId);
            Check(revision is not null && party is not null && response.AgentId == party.RespondingAdultId && response.RepresentedAgentId == party.SubjectId &&
                response.Kind is "answer" or "waive" && BoundedText.IsValid(response.Text, 1024, requireTrimmed: true),
                "A response must belong to the noticed person or their recorded caregiver support.");
        }
        foreach (var consent in item.JudgeConsents)
            Check(IdentityRules.Reference(consent.AgentId, int.MaxValue, requireTrimmed: true) && consent.Tick >= item.FiledTick &&
                (consent.WithdrawnTick is null || consent.WithdrawnTick >= consent.Tick), "A case judge needs their own recorded willingness.");
        foreach (var term in item.JudgeHistory)
            Check(term.EndedTick >= term.Judge.AssignedTick && BoundedText.IsValid(term.Reason, 1024, requireTrimmed: true), "An ended assignment must retain its actual end and reason.");
        for (var index = 0; index < item.Reads.Count; index++)
        {
            var read = item.Reads[index];
            Check(IdentityRules.Reference(read.AgentId, int.MaxValue, requireTrimmed: true) && Canonical(read.EvidenceIds) && Canonical(read.ReopenRequestIds) && Canonical(read.ResponseIds) && Canonical(read.FindingIds) &&
                item.Revisions.Any(r => r.Number == read.Revision && r.PublishedTick <= read.ReadTick) &&
                read.EvidenceIds.All(id => item.Evidence.Any(e => e.Id == id && e.SubmittedTick <= read.ReadTick)) &&
                read.ReopenRequestIds.All(id => item.ReopenRequests.Any(r => r.Id == id && r.Tick <= read.ReadTick)) &&
                read.ResponseIds.All(id => item.Responses.Any(r => TownNonLandRules.ResponseToken(r) == id && r.Tick <= read.ReadTick)) &&
                read.FindingIds.All(id => item.Findings.Any(f => f.Id == id && f.Tick <= read.ReadTick)), "A file read cannot contain unknown or future facts.");
            if (read.SourceAgentId is { } source)
            {
                var prior = item.Reads.Take(index).Where(r => r.Revision == read.Revision && r.ReadTick <= read.ReadTick).ToArray();
                Check(source != read.AgentId && prior.Any(r => r.AgentId == source) &&
                    read.EvidenceIds.All(id => prior.Any(r => (r.AgentId == source || r.AgentId == read.AgentId) && r.EvidenceIds.Contains(id))) &&
                    read.ReopenRequestIds.All(id => prior.Any(r => (r.AgentId == source || r.AgentId == read.AgentId) && r.ReopenRequestIds.Contains(id))) &&
                    read.ResponseIds.All(id => prior.Any(r => (r.AgentId == source || r.AgentId == read.AgentId) && r.ResponseIds.Contains(id))) &&
                    read.FindingIds.All(id => prior.Any(r => (r.AgentId == source || r.AgentId == read.AgentId) && r.FindingIds.Contains(id))),
                    "A relay cannot convey file material neither recipient nor source had read.");
            }
        }
        Check(OrderedTimes(item.Reads.Select(r => r.ReadTick)), "File reads must preserve their chronology.");
        Check(item.Status == "settled" ? item.Findings.Count > 0 && item.SettledTick == item.Findings[^1].Tick : item.SettledTick is null,
            "A settled case must retain its finding; reopening keeps prior findings without a false settlement time.");
        Unique(item.Responses.Select(TownNonLandRules.ResponseToken));
        Unique(item.Evidence.Select(e => e.Id));
        Unique(item.JudgeConsents.Where(c => c.WithdrawnTick is null).Select(c => c.AgentId));
    }
}
