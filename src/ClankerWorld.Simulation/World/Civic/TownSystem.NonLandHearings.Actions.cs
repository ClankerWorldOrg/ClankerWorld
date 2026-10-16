using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static (TownCouncilState Council, TownNonLandHearingState NonLandHearings) OpenNonLandCase(ITownWorld world, TownRuntimeState town,
        TownCouncilState council, TownNonLandHearingState state, TownViolationAllegation allegation, TownViolationFiling filing,
        TownCaseEvidence? evidence, IReadOnlyList<TownCaseParty>? selectedParties = null)
    {
        var count = state.Cases.Count;
        state = TownNonLandRules.File(state, town.Id, allegation, filing, selectedParties ?? TownNonLandPartyRules.CurrentParties(town, allegation, [filing], world.Society, world.WorldTick), world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council));
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
        TownCouncilState council, TownGovernmentState government, NonLandFilingActionPlan? filingPlan = null, NonLandEvidenceActionPlan? evidencePlan = null, NonLandJudgeActionPlan? judgePlan = null, NonLandAdjudicationActionPlan? adjudicationPlan = null, NonLandAgreementActionPlan? agreementPlan = null)
    {
        (council, var state) = AdvanceTownNonLand(world, town, council, government);
        town = town with { Council = council, Government = government, NonLandHearings = state };
        if (filingPlan is not null)
        {
            if (CheckNonLandFiling(world, filingPlan, town).Refusal is { } refusal)
            {
                world.RefuseChoice(filingPlan, refusal);
                return (council, state);
            }
        }
        else if (evidencePlan is not null)
        {
            if (CheckNonLandEvidence(world, evidencePlan, town).Refusal is { } refusal)
            {
                world.RefuseChoice(evidencePlan, refusal);
                return (council, state);
            }
        }
        else if (judgePlan is not null)
        {
            if (CheckNonLandJudge(world, judgePlan, town).Refusal is { } refusal)
            {
                world.RefuseChoice(judgePlan, refusal);
                return (council, state);
            }
        }
        else if (adjudicationPlan is not null)
        {
            if (CheckNonLandAdjudication(world, adjudicationPlan, town).Refusal is { } refusal)
            {
                world.RefuseChoice(adjudicationPlan, refusal);
                return (council, state);
            }
        }
        else if (agreementPlan is not null)
        {
            if (CheckNonLandAgreement(world, agreementPlan, town).Refusal is { } refusal)
            {
                world.RefuseChoice(agreementPlan, refusal);
                return (council, state);
            }
        }
        else
        {
            var selectedId = new NonLandEvidenceChoice(town.Id, action, subject, choice).Encode();
            if (NonLandEvidenceChoice.Decode(selectedId) is { } selectedEvidence && PlanNonLandEvidence(world, actor, selectedEvidence) is { } plannedEvidence)
            {
                evidencePlan = BindNonLandEvidence(plannedEvidence, payload);
                if (CheckNonLandEvidence(world, evidencePlan, town).Refusal is { } refusal)
                {
                    world.RefuseChoice(evidencePlan, refusal);
                    return (council, state);
                }
            }
            if (NonLandJudgeChoice.Decode(selectedId) is { } selectedJudge && PlanNonLandJudge(world, actor, selectedJudge) is { } plannedJudge)
            {
                judgePlan = plannedJudge;
                if (CheckNonLandJudge(world, judgePlan, town).Refusal is { } refusal)
                {
                    world.RefuseChoice(judgePlan, refusal);
                    return (council, state);
                }
            }
            if (NonLandAdjudicationChoice.Decode(selectedId) is { } selectedAdjudication && PlanNonLandAdjudication(world, actor, selectedAdjudication) is { } plannedAdjudication)
            {
                adjudicationPlan = BindNonLandAdjudication(plannedAdjudication, payload);
                if (CheckNonLandAdjudication(world, adjudicationPlan, town).Refusal is { } refusal)
                {
                    world.RefuseChoice(adjudicationPlan, refusal);
                    return (council, state);
                }
            }
            if (NonLandAgreementChoice.Decode(selectedId) is { } selectedAgreement && PlanNonLandAgreement(world, actor, selectedAgreement) is { } plannedAgreement)
            {
                agreementPlan = BindNonLandAgreement(plannedAgreement, payload, CivicDay(world));
                if (CheckNonLandAgreement(world, agreementPlan, town).Refusal is { } refusal)
                {
                    world.RefuseChoice(agreementPlan, refusal);
                    return (council, state);
                }
            }
            if (NonLandFilingChoice.Decode(selectedId) is { } selectedFiling && PlanNonLandFiling(world, actor, selectedFiling) is { } plannedFiling)
            {
                filingPlan = BindNonLandFiling(plannedFiling, payload);
                if (CheckNonLandFiling(world, filingPlan, town).Refusal is { } refusal)
                {
                    world.RefuseChoice(filingPlan, refusal);
                    return (council, state);
                }
            }
            if (filingPlan is null && evidencePlan is null && judgePlan is null && adjudicationPlan is null && agreementPlan is null)
                throw new InvalidOperationException("A non-land action has no registered plan.");
        }
        if (action is "law_case_file" or "law_case_file_affected" or "law_case_file_town" or "law_case_propose_town")
        {
            var selected = filingPlan ?? throw new InvalidOperationException("A planned non-land filing is required.");
            var text = selected.Statement!;
            var allegation = selected.Allegation with { Statement = text };
            if (action == "law_case_propose_town")
            {
                var request = new TownViolationFilingRequest(allegation, text);
                council = TownCouncilRules.SubmitProposal(council, town.Id, actor, "law_case", null,
                    "Let the Town file this report for a non-land hearing.", "council:" + selected.CouncilRevision, TownAdults(world, town), world.WorldTick, CivicDay(world),
                    noticeText: TownNonLandGovernmentFilingRules.Describe(request), nonLandRequest: request);
                return (council, state);
            }
            var office = selected.Office;
            var filing = new TownViolationFiling(actor, NonLandFilingKind(selected.Choice), world.WorldTick, text, allegation.SourceEvidenceIds, office?.ElectionId);
            var opened = OpenNonLandCase(world, town, council, state, allegation, filing, selected.Evidence with { SubmittedTick = world.WorldTick }, selected.Parties);
            NonLandEvent(world, "opened", town, opened.NonLandHearings.Cases.Single(item => item.Key == TownNonLandRules.CaseKey(town.Id, allegation)).Id, actor);
            return opened;
        }
        if (action == "remedy_renegotiate")
        {
            var agreement = agreementPlan!.Agreement!;
            var oldOffer = agreementPlan.Offer!;
            var terms = PublishedNonLandAgreementTerms(agreementPlan, state.Sequence);
            state = TownRemedyRules.Offer(state, oldOffer.CaseId, oldOffer.FindingId, actor, terms,
                agreementPlan.CompletionTicks, agreementPlan.Reason!, world.WorldTick, CivicDay(world),
                TownCouncilRules.NextNoticeId(council), true, replacesAgreementId: agreement.Id);
            council = PostNonLandOffer(world, council, state.Offers[^1]);
            NonLandEvent(world, "offer", town, state.Offers[^1].Id, actor);
            return (council, state);
        }
        if (action is "remedy_read" or "remedy_accept" or "remedy_decline" or "remedy_counter")
        {
            var offer = agreementPlan!.Offer!;
            if (action == "remedy_read") council = TownCouncilRules.LearnNotice(council, actor, offer.NoticeId, world.WorldTick);
            else if (action == "remedy_counter")
            {
                var terms = PublishedNonLandAgreementTerms(agreementPlan, state.Sequence);
                var text = agreementPlan.Reason!;
                state = TownRemedyRules.Respond(state, offer.Id, offer.Revision, actor, "counter", text, world.WorldTick, CivicDay(world), council.Knowledge,
                    NonLandRemedyDurationFeasible(world, town, offer.Terms.Where(term => term.ContributorId == actor).ToArray(), offer.CompletionTicks));
                state = TownRemedyRules.Offer(state, offer.CaseId, offer.FindingId, actor, terms, agreementPlan.CompletionTicks,
                    text, world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council), true, replacesOfferId: offer.Id);
                council = PostNonLandOffer(world, council, state.Offers[^1]);
            }
            else state = TownRemedyRules.Respond(state, offer.Id, offer.Revision, actor, action == "remedy_accept" ? "accept" : "decline",
                agreementPlan.Reason, world.WorldTick, CivicDay(world), council.Knowledge,
                NonLandRemedyCapacityFeasible(world, offer.Terms) &&
                NonLandRemedyDurationFeasible(world, town, offer.Terms.Where(term => term.ContributorId == actor).ToArray(), offer.CompletionTicks));
            NonLandEvent(world, "offer_response", town, offer.Id, actor);
            return (council, state);
        }
        var item = (evidencePlan?.CaseId ?? judgePlan?.CaseId ?? adjudicationPlan?.Case.Id ?? agreementPlan?.Case.Id) is { } selectedCase ? state.Cases.Single(item => item.Id == selectedCase) : state.Cases.Single(item => NonLandActionToken(item) == subject || item.Contest is { } contest &&
            NonLandActionToken(item) + "#" + TownCaseJudgeRules.RoundToken(contest) == subject);
        var revision = NonLandRevision(item);
        var parties = NonLandParties(world, town, item);
        switch (action)
        {
            case "law_case_inspect":
                council = TownCouncilRules.LearnNotice(council, actor, revision.NoticeId, world.WorldTick);
                foreach (var evidence in evidencePlan!.Evidence)
                {
                    state = AddNonLandEvidence(world, state, state.Cases.Single(current => current.Id == item.Id), evidence, actor, council);
                }
                state = AddNonLandEvidence(world, state, state.Cases.Single(current => current.Id == item.Id), evidencePlan.LawEvidence!, actor, council);
                state = TownNonLandRules.Inspect(state, item.Id, revision.Number, actor, world.WorldTick);
                state = AcquireNonLandInspectedSources(world, town with { NonLandHearings = state, Council = council },
                    state.Cases.Single(current => current.Id == item.Id), actor);
                break;
            case "law_case_evidence":
                foreach (var evidence in evidencePlan!.Evidence)
                    state = AddNonLandEvidence(world, state, state.Cases.Single(current => current.Id == item.Id), evidence, actor, council);
                break;
            case "law_case_advise":
            case "law_case_statement":
                var text = evidencePlan!.Statement!;
                state = AddNonLandEvidence(world, state, item,
                    new("statement:" + NonLandToken(actor + "|" + text), revision.Number, "allegation", "statement", actor,
                        null, null, world.WorldTick, actor, world.WorldTick, text), actor, council);
                break;
            case "law_case_answer":
            case "law_case_waive":
                var party = evidencePlan!.Party!;
                state = TownNonLandRules.Respond(state, item.Id, revision.Number, actor, party.SubjectId, party.Id,
                    action == "law_case_answer" ? "answer" : "waive", action == "law_case_answer" ? evidencePlan.Statement! : NonLandWaiverStatement, world.WorldTick, council.Knowledge, parties);
                break;
            case "law_case_judge_register": state = TownCaseJudgeRules.Register(state, item.Id, actor, TownAdults(world, town), LandHearingHouseholds(world), world.WorldTick, parties); break;
            case "law_case_judge_withdraw": state = TownCaseJudgeRules.Withdraw(state, item.Id, actor, world.WorldTick); break;
            case "law_case_judge_resign": state = TownCaseJudgeRules.Resign(state, item.Id, actor, world.WorldTick); break;
            case "law_case_judge_vote": state = TownCaseJudgeRules.Vote(state, item.Id, TownCaseJudgeRules.RoundToken(judgePlan!.Contest!), actor, judgePlan.Candidate!, world.WorldTick); break;
            case "law_case_find":
                state = TownNonLandRules.Rule(state, adjudicationPlan!.Case.Id, revision.Number, adjudicationPlan.Judge!, world.WorldTick,
                    choice == "unsupported" ? "unsupported" : "supported", choice == "unsupported" ? "none" : choice,
                    adjudicationPlan.EvidenceIds ?? [], adjudicationPlan.Reasons!, adjudicationPlan.Uncertainty!, adjudicationPlan.Parties,
                    item.Judge is { } judge && NonLandJudgeValid(world, town, item, judge),
                    crediblePriorNotice: HasPriorLawNotice(town, item.Allegation, item.Allegation.SubjectId));
                council = TownCouncilRules.PostNotice(council, "law_case", state.Cases.Single(current => current.Id == item.Id).Findings[^1].Id,
                    "A reasoned civil finding was recorded: " + choice + ". This finding changes no physical facts, stock, membership or office.", world.WorldTick);
                break;
            case "law_case_reopen":
                state = TownNonLandRules.RequestReopen(state, adjudicationPlan!.Case.Id, actor, choice, adjudicationPlan.EvidenceIds ?? [], adjudicationPlan.Reasons!, world.WorldTick);
                break;
            case "law_case_assess_reopen":
                var request = adjudicationPlan!.Request!;
                var accept = adjudicationPlan.Accept;
                state = TownNonLandRules.Reopen(state, item.Id, request.Id, adjudicationPlan.Judge!, accept, adjudicationPlan.Reasons!, adjudicationPlan.Parties, world.WorldTick, CivicDay(world), TownCouncilRules.NextNoticeId(council), item.Judge is { } reopeningJudge && NonLandJudgeValid(world, town, item, reopeningJudge));
                if (accept) council = PostNonLandNotice(council, state.Cases.Single(current => current.Id == item.Id));
                break;
            case "law_case_relay":
                var recipient = evidencePlan!.Recipient!;
                council = TownCouncilRules.LearnNotice(council, recipient, revision.NoticeId, world.WorldTick, actor);
                state = TownNonLandRules.RelayRead(state, item.Id, revision.Number, actor, recipient, world.WorldTick, evidencePlan.SourceRead);
                break;
            case "remedy_offer":
                var proposedTerms = PublishedNonLandAgreementTerms(agreementPlan!, state.Sequence);
                state = TownRemedyRules.Offer(state, item.Id, choice, actor, proposedTerms,
                    agreementPlan!.CompletionTicks, agreementPlan.Reason!, world.WorldTick, CivicDay(world),
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

    private static TownRemedyTerm[] PublishedNonLandAgreementTerms(NonLandAgreementActionPlan plan, long sequence) =>
        plan.Terms!.Select((term, index) => term with { Id = TownRecordIds.RemedyTerm(plan.Case.Id, sequence, index) }).ToArray();

    internal static NonLandAgreementReferences CaptureNonLandAgreementReferences(ITownWorld world) => new(
        world.Society.Agents.Select(person => person.Id).ToArray(),
        world.Society.Agents.Select(person => person.Id).Concat(world.Society.Households.Select(household => household.Id)).Concat(world.Towns.Select(town => town.Id)).ToArray(),
        world.Society.Inventory.Lots.Select(lot => lot.Id).Concat(world.WorldSimulation.Buildings.Select(building => building.InstanceId)).ToArray());

    internal TownRemedyTerm[] NonLandTerms(ITownWorld world, TownRuntimeState town, string actor, string caseId, CognitionNonLandChoice? payload, string? replacingAgreementId = null)
    {
        var references = CaptureNonLandAgreementReferences(world);
        if (BindNonLandAgreementTerms(caseId, town.NonLandHearings.Sequence, references, payload) is not { Length: > 0 } result)
            throw new InvalidOperationException("A restorative offer needs finite named goods or work.");
        var duration = payload?.CompletionTicks ?? checked(3L * CivicDay(world));
        if (NonLandTermsRefusal(world, town, actor, result, duration, replacingAgreementId) is { } refusal) throw new InvalidOperationException(refusal);
        return result;
    }

    internal string? NonLandTermsRefusal(ITownWorld world, TownRuntimeState town, string actor, TownRemedyTerm[] result,
        long duration, string? replacingAgreementId = null)
    {
        if (replacingAgreementId is not null)
            town = town with
            {
                NonLandHearings = town.NonLandHearings with
                { Agreements = town.NonLandHearings.Agreements.Where(agreement => agreement.Id != replacingAgreementId).ToArray() }
            };
        if (result.Any(term => !LandHearingAdult(world, term.ContributorId) || !NonLandRemedyFeasible(world, town, term) || !NonLandRemedyTermKnownTo(world, town, term, actor)))
            return "A proposed contribution must be feasible, lawful and known without searching private property.";
        if (duration > long.MaxValue - world.WorldTick - CivicDay(world) || !NonLandRemedyDurationFeasible(world, town, result, duration))
            return "The proposed deadline leaves insufficient time for the named physical work and travel.";
        return null;
    }

    internal static TownCouncilState PostNonLandOffer(ITownWorld world, TownCouncilState council, TownRemedyOffer offer) =>
        TownCouncilRules.PostNotice(council, "remedy", offer.Id, "Voluntary restorative offer: " + NonLandTermsText(offer.Terms) +
            ". " + NonLandOfferTiming(world, offer) + " Each contributing adult may accept, decline or counter; silence is not consent and declining is no offense.", offer.PublishedTick);
}
