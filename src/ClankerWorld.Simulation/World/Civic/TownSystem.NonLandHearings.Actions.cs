using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static (TownCouncilState Council, TownNonLandHearingState NonLandHearings) OpenNonLandCase(ITownWorld world, TownRuntimeState town,
        TownCouncilState council, TownNonLandHearingState state, TownViolationAllegation allegation, TownViolationFiling filing,
        TownCaseEvidence? evidence)
    {
        var count = state.Cases.Count;
        state = TownNonLandRules.File(state, town.Id, allegation, filing, TownNonLandPartyRules.CurrentParties(town, allegation, [filing], world.Society, world.WorldTick), world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council));
        var item = state.Cases.Single(current => current.Key == TownNonLandRules.CaseKey(town.Id, allegation));
        if (state.Cases.Count > count) council = PostNonLandNotice(council, item);
        council = TownCouncilRules.LearnNotice(council, filing.AgentId, NonLandRevision(item).NoticeId, world.WorldTick);
        if (evidence is not null)
            state = AddNonLandEvidence(world, state, item, evidence, filing.AgentId, council);
        return (council, state);
    }

    internal static TownNonLandHearingState AddNonLandEvidence(ITownWorld world, TownNonLandHearingState state, TownNonLandCase item,
        TownCaseEvidence evidence, string actor, TownCouncilState council)
    {
        if (item.Evidence.Any(old => old.Kind == evidence.Kind && old.SourceRecordId == evidence.SourceRecordId &&
                old.SourceVersion == evidence.SourceVersion && old.SourceAgentId == evidence.SourceAgentId && old.Text == evidence.Text)) return state;
        var nativeSource = state.ConductRecords.Any(record => record.Id == evidence.SourceRecordId) &&
            state.Acquisitions.Any(acquisition => acquisition.Id == evidence.Id && acquisition.ConductId == evidence.SourceRecordId && acquisition.AgentId == actor);
        var id = nativeSource ? evidence.Id : "case-evidence:" + NonLandToken(item.Id + "|" + evidence.Id + "|" + actor + "|" + NonLandRevision(item).Number);
        return TownNonLandRules.AddEvidence(state, item.Id, NonLandRevision(item).Number, evidence with
        { Id = id, Revision = NonLandRevision(item).Number, SubmittedByAgentId = actor, SubmittedTick = world.WorldTick }, council.Knowledge);
    }

    internal (TownCouncilState Council, TownNonLandHearingState NonLandHearings) ApplyTownNonLandAction(ITownWorld world, TownRuntimeState town,
        string actor, string action, string subject, string choice, CognitionNonLandChoice? payload,
        TownCouncilState council, TownGovernmentState government)
    {
        (council, var state) = AdvanceTownNonLand(world, town, council, government);
        town = town with { Council = council, Government = government, NonLandHearings = state };
        var candidates = new List<CognitionCandidate>();
        AddTownNonLandCandidates(world, candidates, actor, town);
        if (!candidates.Any(candidate => candidate.Id == $"civic|{town.Id}|{action}|{subject}|{choice}"))
            throw new InvalidOperationException("This legal choice no longer has current authority or informed eligibility.");
        if (action is "law_case_file" or "law_case_file_affected" or "law_case_file_town" or "law_case_propose_town")
        {
            var report = NonLandKnownReports(world, town, actor).Single(report => NonLandReportToken(report.Allegation) == subject);
            var text = NonLandText(payload?.Statement);
            var allegation = report.Allegation with { Statement = text };
            if (action == "law_case_propose_town")
            {
                var request = new TownViolationFilingRequest(allegation, text);
                council = TownCouncilRules.SubmitProposal(council, town.Id, actor, "law_case", null,
                    "Let the Town file this report for a non-land hearing.", "council:" + council.Revision, TownAdults(world, town), world.WorldTick, CivicDay(world),
                    noticeText: TownNonLandGovernmentFilingRules.Describe(request), nonLandRequest: request);
                return (council, state);
            }
            var office = action == "law_case_file_town" ? government.Offices.Single(office => office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
            var filing = new TownViolationFiling(actor, office is not null ? "town" : action == "law_case_file_affected" ? "affected" : "witness", world.WorldTick, text, allegation.SourceEvidenceIds, office?.ElectionId);
            var opened = OpenNonLandCase(world, town, council, state, allegation, filing, report.Evidence);
            NonLandEvent(world, "opened", town, opened.NonLandHearings.Cases.Single(item => item.Key == TownNonLandRules.CaseKey(town.Id, allegation)).Id, actor);
            return opened;
        }
        if (action == "remedy_renegotiate")
        {
            var agreement = state.Agreements.Single(agreement => agreement.Id == subject && agreement.TermsHash == choice);
            var oldOffer = state.Offers.Single(offer => offer.Id == agreement.OfferId);
            var terms = NonLandTerms(world, town, actor, oldOffer.CaseId, payload, agreement.Id);
            state = TownRemedyRules.Offer(state, oldOffer.CaseId, oldOffer.FindingId, actor, terms,
                payload?.CompletionTicks ?? checked(3L * CivicDay(world)), NonLandText(payload?.Statement), world.WorldTick, CivicDay(world),
                TownCouncilRules.NextNoticeId(council), true, replacesAgreementId: agreement.Id);
            council = PostNonLandOffer(world, council, state.Offers[^1]);
            NonLandEvent(world, "offer", town, state.Offers[^1].Id, actor);
            return (council, state);
        }
        if (action is "remedy_read" or "remedy_accept" or "remedy_decline" or "remedy_counter")
        {
            var offer = state.Offers.Single(offer => NonLandOfferToken(offer) == subject);
            if (action == "remedy_read") council = TownCouncilRules.LearnNotice(council, actor, offer.NoticeId, world.WorldTick);
            else if (action == "remedy_counter")
            {
                var terms = NonLandTerms(world, town, actor, offer.CaseId, payload);
                var text = NonLandText(payload?.Statement);
                state = TownRemedyRules.Respond(state, offer.Id, offer.Revision, actor, "counter", text, world.WorldTick, CivicDay(world), council.Knowledge,
                    NonLandRemedyDurationFeasible(world, town, offer.Terms.Where(term => term.ContributorId == actor).ToArray(), offer.CompletionTicks));
                state = TownRemedyRules.Offer(state, offer.CaseId, offer.FindingId, actor, terms, payload?.CompletionTicks ?? checked(3L * CivicDay(world)),
                    text, world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council), true, replacesOfferId: offer.Id);
                council = PostNonLandOffer(world, council, state.Offers[^1]);
            }
            else state = TownRemedyRules.Respond(state, offer.Id, offer.Revision, actor, action == "remedy_accept" ? "accept" : "decline",
                payload?.Statement, world.WorldTick, CivicDay(world), council.Knowledge,
                NonLandRemedyCapacityFeasible(world, offer.Terms) &&
                NonLandRemedyDurationFeasible(world, town, offer.Terms.Where(term => term.ContributorId == actor).ToArray(), offer.CompletionTicks));
            NonLandEvent(world, "offer_response", town, offer.Id, actor);
            return (council, state);
        }
        var item = state.Cases.Single(item => NonLandActionToken(item) == subject || item.Contest is { } contest &&
            NonLandActionToken(item) + "#" + TownCaseJudgeRules.RoundToken(contest) == subject);
        if (payload?.EvidenceIds is { } references)
            payload = payload with
            {
                EvidenceIds = references.Select(reference => item.Evidence.FirstOrDefault(evidence =>
                evidence.Id == reference || CivicAgentToken(evidence.Id) == reference)?.Id ?? reference).ToArray()
            };
        var revision = NonLandRevision(item);
        var parties = NonLandParties(world, town, item);
        switch (action)
        {
            case "law_case_inspect":
                council = TownCouncilRules.LearnNotice(council, actor, revision.NoticeId, world.WorldTick);
                foreach (var evidence in NonLandKnownEvidence(world, town, item, actor))
                {
                    state = AddNonLandEvidence(world, state, state.Cases.Single(current => current.Id == item.Id), evidence, actor, council);
                }
                var law = government.Laws.Single(law => law.Id == item.Allegation.LawId);
                var version = law.Versions.Single(version => version.Version == item.Allegation.LawVersion);
                var lawText = ("Applicable law at the alleged act: " + TownLawRules.Text(version.Subject, version.Rule));
                state = AddNonLandEvidence(world, state, state.Cases.Single(current => current.Id == item.Id),
                    new(TownRecordIds.LawEvidence(law.Id, version.Version), revision.Number, "record", "record_inspection", actor, LandHearingLawToken(law, version), TownLandHearingRules.LawVersion(version), item.Allegation.ConductTick,
                        actor, world.WorldTick, BoundedText.Excerpt(lawText, 256)), actor, council);
                state = TownNonLandRules.Inspect(state, item.Id, revision.Number, actor, world.WorldTick);
                state = AcquireNonLandInspectedSources(world, town with { NonLandHearings = state, Council = council },
                    state.Cases.Single(current => current.Id == item.Id), actor);
                break;
            case "law_case_evidence":
                foreach (var evidence in NonLandKnownEvidence(world, town, item, actor))
                    state = AddNonLandEvidence(world, state, state.Cases.Single(current => current.Id == item.Id), evidence, actor, council);
                break;
            case "law_case_advise":
            case "law_case_statement":
                var text = NonLandText(payload?.Statement);
                state = AddNonLandEvidence(world, state, item,
                    new("statement:" + NonLandToken(actor + "|" + text), revision.Number, "allegation", "statement", actor,
                        null, null, world.WorldTick, actor, world.WorldTick, text), actor, council);
                break;
            case "law_case_answer":
            case "law_case_waive":
                var party = parties.Single(party => CivicAgentToken(party.Id) == choice);
                state = TownNonLandRules.Respond(state, item.Id, revision.Number, actor, party.SubjectId, party.Id,
                    action == "law_case_answer" ? "answer" : "waive", action == "law_case_answer" ? NonLandText(payload?.Statement) : "I choose not to respond. This admits nothing.", world.WorldTick, council.Knowledge, parties);
                break;
            case "law_case_judge_register": state = TownCaseJudgeRules.Register(state, item.Id, actor, TownAdults(world, town), LandHearingHouseholds(world), world.WorldTick, parties); break;
            case "law_case_judge_withdraw": state = TownCaseJudgeRules.Withdraw(state, item.Id, actor, world.WorldTick); break;
            case "law_case_judge_resign": state = TownCaseJudgeRules.Resign(state, item.Id, actor, world.WorldTick); break;
            case "law_case_judge_vote": state = TownCaseJudgeRules.Vote(state, item.Id, TownCaseJudgeRules.RoundToken(item.Contest!), actor, ResolveCivicAgentToken(world, choice), world.WorldTick); break;
            case "law_case_find":
                state = TownNonLandRules.Rule(state, item.Id, revision.Number, item.Judge!, world.WorldTick,
                    choice == "unsupported" ? "unsupported" : "supported", choice == "unsupported" ? "none" : choice,
                    payload?.EvidenceIds ?? [], NonLandText(payload?.Statement), NonLandText(payload?.Uncertainty), parties,
                    item.Judge is { } judge && NonLandJudgeValid(world, town, item, judge),
                    crediblePriorNotice: HasPriorLawNotice(town, item.Allegation, item.Allegation.SubjectId));
                council = TownCouncilRules.PostNotice(council, "law_case", state.Cases.Single(current => current.Id == item.Id).Findings[^1].Id,
                    "A reasoned civil finding was recorded: " + choice + ". This finding changes no physical facts, stock, membership or office.", world.WorldTick);
                break;
            case "law_case_reopen":
                state = TownNonLandRules.RequestReopen(state, item.Id, actor, choice, payload?.EvidenceIds ?? [], NonLandText(payload?.Grounds ?? payload?.Statement), world.WorldTick);
                break;
            case "law_case_assess_reopen":
                var request = item.ReopenRequests.Single(request => request.Status == "pending" &&
                    (CivicAgentToken(request.Id + ":accept") == choice || CivicAgentToken(request.Id + ":reject") == choice));
                var accept = CivicAgentToken(request.Id + ":accept") == choice;
                state = TownNonLandRules.Reopen(state, item.Id, request.Id, item.Judge!, accept, NonLandText(payload?.Statement), parties, world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council), item.Judge is { } reopeningJudge && NonLandJudgeValid(world, town, item, reopeningJudge));
                if (accept) council = PostNonLandNotice(council, state.Cases.Single(current => current.Id == item.Id));
                break;
            case "law_case_relay":
                var recipient = ResolveCivicAgentToken(world, choice);
                council = TownCouncilRules.LearnNotice(council, recipient, revision.NoticeId, world.WorldTick, actor);
                state = TownNonLandRules.RelayRead(state, item.Id, revision.Number, actor, recipient, world.WorldTick);
                break;
            case "remedy_offer":
                var proposedTerms = NonLandTerms(world, town, actor, item.Id, payload);
                state = TownRemedyRules.Offer(state, item.Id, choice, actor, proposedTerms,
                    payload?.CompletionTicks ?? checked(3L * CivicDay(world)), NonLandText(payload?.Statement), world.WorldTick, CivicDay(world),
                    TownCouncilRules.NextNoticeId(council), true);
                council = PostNonLandOffer(world, council, state.Offers[^1]);
                break;
            default: throw new InvalidOperationException("Unsupported non-land case action.");
        }
        NonLandEvent(world, action switch
        {
            "law_case_inspect" => "inspected",
            "law_case_relay" => "relayed",
            "law_case_find" => "finding",
            "law_case_answer" or "law_case_waive" => "response",
            "law_case_reopen" => "reopen_requested",
            "law_case_assess_reopen" => state.Cases.Single(current => current.Id == item.Id).Status == "pending" ? "reopened" : "rejected",
            "law_case_judge_register" or "law_case_judge_withdraw" or "law_case_judge_resign" => "judge_consent",
            "remedy_offer" => "offer",
            _ => "evidence"
        }, town, item.Id, actor);
        return (council, state);
    }

    internal TownRemedyTerm[] NonLandTerms(ITownWorld world, TownRuntimeState town, string actor, string caseId, CognitionNonLandChoice? payload, string? replacingAgreementId = null)
    {
        if (payload?.Terms is not { Count: > 0 } terms) throw new InvalidOperationException("A restorative offer needs finite named goods or work.");
        string? PhysicalReference(string? reference) => reference is null ? null : world.Society.Inventory.Lots.Select(lot => lot.Id).Concat(world.WorldSimulation.Buildings.Select(building => building.InstanceId))
                .FirstOrDefault(id => id == reference || CivicAgentToken(id) == reference) ?? reference;
        string BeneficiaryReference(string reference) => world.Society.Agents.Select(person => person.Id)
            .Concat(world.Society.Households.Select(household => household.Id)).Concat(world.Towns.Select(item => item.Id))
            .FirstOrDefault(id => id == reference || CivicAgentToken(id) == reference) ?? reference;
        var result = terms.Select((term, index) => new TownRemedyTerm("term:" + NonLandToken(caseId + "|" + town.NonLandHearings.Sequence + "|" + index),
            term.Kind, ResolveCivicAgentToken(world, term.ContributorId), term.BeneficiaryId is { } beneficiary ? BeneficiaryReference(beneficiary) : null,
            term.ItemKind, term.Quantity, PhysicalReference(term.TargetId))).ToArray();
        if (replacingAgreementId is not null)
            town = town with
            {
                NonLandHearings = town.NonLandHearings with
                { Agreements = town.NonLandHearings.Agreements.Where(agreement => agreement.Id != replacingAgreementId).ToArray() }
            };
        if (result.Any(term => !LandHearingAdult(world, term.ContributorId) || !NonLandRemedyFeasible(world, town, term) || !NonLandRemedyTermKnownTo(world, town, term, actor)))
            throw new InvalidOperationException("A proposed contribution must be feasible, lawful and known without searching private property.");
        var duration = payload.CompletionTicks ?? checked(3L * CivicDay(world));
        if (duration > long.MaxValue - world.WorldTick - CivicDay(world) || !NonLandRemedyDurationFeasible(world, town, result, duration))
            throw new InvalidOperationException("The proposed deadline leaves insufficient time for the named physical work and travel.");
        return result;
    }

    internal static TownCouncilState PostNonLandOffer(ITownWorld world, TownCouncilState council, TownRemedyOffer offer) =>
        TownCouncilRules.PostNotice(council, "remedy", offer.Id, "Voluntary restorative offer: " + NonLandTermsText(offer.Terms) +
            ". " + NonLandOfferTiming(world, offer) + " Each contributing adult may accept, decline or counter; silence is not consent and declining is no offense.", offer.PublishedTick);
}
