using ClankerWorld.Simulation.Cognition;
using System.Globalization;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static ActorCapabilityPolicy NonLandAdjudicationCapabilities(ITownWorld world, NonLandAdjudicationActionPlan plan) =>
        new(plan.Choice.Verb == "law_case_reopen" ? ActionLifeStages.NonInfants : ActionLifeStages.Adults, AllowBoat: true,
            AllowUrgentFood: !world.IsChild(plan.Actor), AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static NonLandAdjudicationActionPlan? PlanNonLandAdjudication(ITownWorld world, string actor, NonLandAdjudicationChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => NonLandActionToken(item) == choice.CaseToken) is not { } item) return null;
        var request = choice.Verb == "law_case_assess_reopen" ? item.ReopenRequests.SingleOrDefault(request =>
            DerivedId.Create(request.Id + ":accept", "agent-sha256:") == choice.OutcomeOrRequestToken || DerivedId.Create(request.Id + ":reject", "agent-sha256:") == choice.OutcomeOrRequestToken) : null;
        return new(actor, choice, item, NonLandParties(world, town, item), item.Judge, request,
            request is not null && DerivedId.Create(request.Id + ":accept", "agent-sha256:") == choice.OutcomeOrRequestToken);
    }

    internal static NonLandAdjudicationActionPlan BindNonLandAdjudication(NonLandAdjudicationActionPlan plan, CognitionNonLandChoice? terms)
    {
        if (plan.Bound) return plan;
        return plan with
        {
            Bound = true,
            EvidenceIds = (terms?.EvidenceIds ?? []).Select(reference => plan.Case.Evidence.FirstOrDefault(evidence =>
                evidence.Id == reference || CivicAgentToken(evidence.Id) == reference)?.Id ?? reference).ToArray(),
            Reasons = CognitionDecisionResponse.NormalizeIdentityText(plan.Choice.Verb == "law_case_reopen" ? terms?.Grounds ?? terms?.Statement : terms?.Statement),
            Uncertainty = CognitionDecisionResponse.NormalizeIdentityText(terms?.Uncertainty)
        };
    }

    internal static ActionCheck CheckNonLandAdjudication(ITownWorld world, NonLandAdjudicationActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var request = plan.Choice.Verb == "law_case_reopen";
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandAdjudicationCapabilities(world, plan));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.Case.Id) is not { } item ||
            NonLandActionToken(item) != plan.Choice.CaseToken || TownHearingProcedure.Digest(NonLandRevision(item)) != TownHearingProcedure.Digest(NonLandRevision(plan.Case)) ||
            !NonLandKnows(world, town, item, plan.Actor) || !NonLandReadCurrent(world, item, plan.Actor) ||
            TownHearingProcedure.Digest(NonLandParties(world, town, item)) != TownHearingProcedure.Digest(plan.Parties))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected finding material, noticed parties or informed knowledge has changed");
        string? failure;
        if (request)
        {
            if (!plan.Parties.Any(party => party.SubjectId == plan.Actor || party.RespondingAdultId == plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they are no longer a party to that selected finding");
            failure = TownNonLandRules.RequestReopenRefusal(item, plan.Choice.OutcomeOrRequestToken, plan.EvidenceIds ?? [], plan.Reasons ?? "", plan.Bound);
        }
        else
        {
            if (plan.Judge is not { } judge || judge.AgentId != plan.Actor || item.Judge != judge || !NonLandJudgeValid(world, town, item, judge))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected independent case authority has changed");
            if (plan.Choice.Verb == "law_case_find")
            {
                if (!NonLandFindingChoices.Contains(plan.Choice.OutcomeOrRequestToken, StringComparer.Ordinal))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected civil outcome is unavailable");
                failure = TownNonLandRules.FindingRefusal(item, judge, world.WorldTick,
                    plan.Choice.OutcomeOrRequestToken == "unsupported" ? "unsupported" : "supported",
                    plan.Choice.OutcomeOrRequestToken == "unsupported" ? "none" : plan.Choice.OutcomeOrRequestToken,
                    plan.EvidenceIds ?? [], plan.Reasons ?? "", plan.Uncertainty ?? "", plan.Parties, true,
                    HasPriorLawNotice(town, item.Allegation, item.Allegation.SubjectId), plan.Bound);
            }
            else
            {
                if (plan.Request is not { } original || item.ReopenRequests.SingleOrDefault(request => request.Id == original.Id) is not { Status: "pending" } current ||
                    TownHearingProcedure.Digest(current) != TownHearingProcedure.Digest(original))
                    return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected pending reopening request has changed");
                failure = TownNonLandRules.ReopeningRefusal(item, current, judge, plan.Accept,
                    plan.Bound ? plan.Reasons ?? "" : "Assess the selected grounds.", plan.Parties, world.WorldTick, CivicDay(world), "planned-notice", true);
            }
        }
        return failure is null ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, failure);
    }

    private static void OfferNonLandAdjudication(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownNonLandCase item, string verb, string terms, string text, int priority)
    {
        var choice = new NonLandAdjudicationChoice(town.Id, verb, NonLandActionToken(item), CivicAgentToken(terms));
        if (PlanNonLandAdjudication(world, actor, choice) is { } plan && CheckNonLandAdjudication(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
    internal static NonLandAgreementActionPlan? PlanNonLandAgreement(ITownWorld world, string actor, NonLandAgreementChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town) return null;
        var agreement = choice.Verb == "remedy_renegotiate" ? town.NonLandHearings.Agreements.SingleOrDefault(item => item.Id == choice.SubjectToken && item.TermsHash == choice.TermsToken) : null;
        var offer = agreement is not null ? town.NonLandHearings.Offers.SingleOrDefault(item => item.Id == agreement.OfferId) :
            choice.Verb == "remedy_offer" ? null : town.NonLandHearings.Offers.SingleOrDefault(item => NonLandOfferToken(item) == choice.SubjectToken);
        var item = choice.Verb == "remedy_offer" ? town.NonLandHearings.Cases.SingleOrDefault(item => NonLandActionToken(item) == choice.SubjectToken) :
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == offer?.CaseId);
        var finding = item?.Findings.SingleOrDefault(finding => finding.Id == (offer?.FindingId ?? choice.TermsToken));
        return item is null || finding is null ? null : new(actor, choice, item, finding, offer, agreement,
            choice.Verb == "remedy_read" ? CivicBoard(world, town) : null,
            choice.Verb is "remedy_offer" or "remedy_counter" or "remedy_renegotiate" ? CaptureNonLandAgreementReferences(world) : new([], [], []),
            town.NonLandHearings.Sequence);
    }

    internal static NonLandAgreementActionPlan BindNonLandAgreement(NonLandAgreementActionPlan plan, CognitionNonLandChoice? payload, int day)
    {
        if (plan.Bound) return plan;
        var terms = BindNonLandAgreementTerms(plan.Case.Id, plan.Sequence, plan.References, payload);
        var reason = CognitionDecisionResponse.NormalizeIdentityText(payload?.Statement);
        return plan with
        {
            Bound = true,
            Terms = terms,
            Reason = reason,
            CompletionTicks = payload?.CompletionTicks ?? checked(3L * day),
            InvalidTerms = payload?.Statement is not null && reason is null
        };
    }

    internal static TownRemedyTerm[]? BindNonLandAgreementTerms(string caseId, long sequence, NonLandAgreementReferences references, CognitionNonLandChoice? payload)
    {
        string Resolve(string reference, IReadOnlyList<string> values) => values.FirstOrDefault(id => id == reference || CivicAgentToken(id) == reference) ?? reference;
        return payload?.Terms?.Select((term, index) => new TownRemedyTerm(TownRecordIds.RemedyTerm(caseId, sequence, index),
            term.Kind, Resolve(term.ContributorId, references.People), term.BeneficiaryId is { } beneficiary ? Resolve(beneficiary, references.Beneficiaries) : null,
            term.ItemKind, term.Quantity, term.TargetId is { } target ? Resolve(target, references.Physical) : null)).ToArray();
    }

    internal ActionCheck CheckNonLandAgreement(ITownWorld world, NonLandAgreementActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.Case.Id) is not { } item)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected voluntary case is unavailable");
        var response = plan.Choice.Verb is "remedy_read" or "remedy_accept" or "remedy_decline" or "remedy_counter";
        TownRemedyOffer? currentOffer = null;
        if (response)
        {
            if (plan.Offer is not { } original || town.NonLandHearings.Offers.SingleOrDefault(offer => offer.Id == original.Id) is not { Status: "pending" } activeOffer ||
                TownHearingProcedure.Digest(activeOffer with { Responses = original.Responses }) != TownHearingProcedure.Digest(original) ||
                NonLandOfferToken(activeOffer) != plan.Choice.SubjectToken || world.WorldTick >= activeOffer.ResponseDeadlineTick || !activeOffer.Terms.Any(term => term.ContributorId == plan.Actor))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected offer, contribution or response window has changed");
            currentOffer = activeOffer;
            if (plan.Choice.Verb == "remedy_read")
                return CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town) && !CivicHistory(town).Known(plan.Actor).Contains(activeOffer.NoticeId)
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot read that selected offer at its notice place now");
            var feasible = plan.Choice.Verb != "remedy_accept" || NonLandRemedyCapacityFeasible(world, activeOffer.Terms) &&
                NonLandRemedyDurationFeasible(world, town, activeOffer.Terms.Where(term => term.ContributorId == plan.Actor).ToArray(), activeOffer.CompletionTicks);
            if (TownRemedyRules.ResponseRefusal(activeOffer, plan.Actor, plan.Choice.Verb == "remedy_accept" ? "accept" : plan.Choice.Verb == "remedy_decline" ? "decline" : "counter",
                plan.Bound ? plan.Reason : null, world.WorldTick, CivicDay(world), town.Council.Knowledge, feasible) is { } refusal)
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
            if (plan.Choice.Verb != "remedy_counter") return plan.InvalidTerms ?
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the personal response text is unavailable") : new();
        }
        if (NonLandActionToken(item) != NonLandActionToken(plan.Case) || !NonLandReadCurrent(world, item, plan.Actor) ||
            item.Findings.SingleOrDefault(finding => finding.Id == plan.Finding.Id) is not { } finding ||
            TownHearingProcedure.Digest(finding) != TownHearingProcedure.Digest(plan.Finding))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected finding material or informed case read has changed");
        if (plan.Choice.Verb == "remedy_offer" && !(NonLandParties(world, town, item).Any(party => party.SubjectId == plan.Actor || party.RespondingAdultId == plan.Actor) ||
            finding.Judge.AgentId == plan.Actor && NonLandJudgeValid(world, town, item, finding.Judge)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer have standing for that selected voluntary offer");
        if (plan.Choice.Verb == "remedy_renegotiate" && (plan.Agreement is not { } previous ||
            town.NonLandHearings.Agreements.SingleOrDefault(agreement => agreement.Id == previous.Id) is not { Status: not "completed" } current ||
            TownRemedyRules.IsSuperseded(town.NonLandHearings, current.Id) ||
            TownHearingProcedure.Digest(current with { Status = previous.Status }) != TownHearingProcedure.Digest(previous) || !current.Terms.Any(term => term.ContributorId == plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected unfinished personal agreement has changed");
        if (!plan.Bound) return item.Status == "settled" && finding.Result == "supported" &&
            !town.NonLandHearings.Offers.Any(offer => offer.CaseId == item.Id && offer.Status == "pending" && offer.Id != currentOffer?.Id)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected finding cannot receive new voluntary terms now");
        if (plan.InvalidTerms || plan.Reason is null || plan.Terms is not { Count: > 0 } selected || !TownRemedyRules.ValidTerms(selected))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "a voluntary offer needs clear reasons and finite named contributions");
        var terms = PublishedNonLandAgreementTerms(plan, town.NonLandHearings.Sequence);
        if (NonLandTermsRefusal(world, town, plan.Actor, terms, plan.CompletionTicks, plan.Agreement?.Id) is { } termsRefusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, termsRefusal);
        var state = currentOffer is null ? town.NonLandHearings : town.NonLandHearings with
        { Offers = town.NonLandHearings.Offers.Select(offer => offer.Id == currentOffer.Id ? offer with { Status = "countered" } : offer).ToArray() };
        return TownRemedyRules.OfferRefusal(state, item.Id, finding.Id, plan.Actor, terms, plan.CompletionTicks, plan.Reason,
            world.WorldTick, CivicDay(world), "planned-notice", true, currentOffer?.Id, plan.Agreement?.Id) is { } offerRefusal
                ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, offerRefusal) : new();
    }

    private void OfferNonLandAgreement(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        string verb, string subject, string terms, string text, int priority)
    {
        var id = CivicAction(world, town.Id, verb, subject, terms);
        if (NonLandAgreementChoice.Decode(id) is { } choice && PlanNonLandAgreement(world, actor, choice) is { } plan && CheckNonLandAgreement(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
    internal static ActorCapabilityPolicy NonLandEvidenceCapabilities(ITownWorld world, NonLandEvidenceActionPlan plan) =>
        new(ActionLifeStages.NonInfants, AllowBoat: true, AllowUrgentFood: !world.IsChild(plan.Actor), AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal const string NonLandWaiverStatement = "I choose not to respond. This admits nothing.";

    internal static NonLandEvidenceActionPlan? PlanNonLandEvidence(ITownWorld world, string actor, NonLandEvidenceChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => NonLandActionToken(item) == choice.CaseToken) is not { } item) return null;
        var revision = NonLandRevision(item);
        var party = choice.Verb is "law_case_answer" or "law_case_waive" ?
            NonLandParties(world, town, item).SingleOrDefault(party => CivicAgentToken(party.Id) == choice.PartyOrRecipientToken) : null;
        var reads = choice.Verb == "law_case_relay" ? item.Reads.Where(read => read.AgentId == actor &&
            read.Revision == revision.Number && read.ReadTick <= world.WorldTick).ToArray() : [];
        TownCaseEvidence? lawEvidence = null;
        if (choice.Verb == "law_case_inspect" && town.Government?.Laws.SingleOrDefault(law => law.Id == item.Allegation.LawId) is { } law &&
            law.Versions.SingleOrDefault(version => version.Version == item.Allegation.LawVersion) is { } version)
            lawEvidence = new(TownRecordIds.LawEvidence(law.Id, version.Version), revision.Number, "record", "record_inspection", actor,
                LandHearingLawToken(law, version), TownLandHearingRules.LawVersion(version), item.Allegation.ConductTick, actor, world.WorldTick,
                BoundedText.Excerpt("Applicable law at the alleged act: " + TownLawRules.Text(version.Subject, version.Rule), 256));
        return new(actor, choice, item.Id, revision, choice.Verb == "law_case_inspect" ? CivicBoard(world, town) : null,
            choice.Verb is "law_case_inspect" or "law_case_evidence" ? NonLandKnownEvidence(world, town, item, actor) : [], lawEvidence, party,
            choice.Verb == "law_case_relay" ? ResolveCivicAgentToken(world, choice.PartyOrRecipientToken) : null,
            reads.Length == 0 ? null : new(revision.Number, actor, world.WorldTick,
                TownHearingProcedure.Ordered(reads.SelectMany(read => read.EvidenceIds)), TownHearingProcedure.Ordered(reads.SelectMany(read => read.ReopenRequestIds)))
            {
                ResponseIds = TownHearingProcedure.Ordered(reads.SelectMany(read => read.ResponseIds)),
                FindingIds = TownHearingProcedure.Ordered(reads.SelectMany(read => read.FindingIds))
            });
    }

    internal static NonLandEvidenceActionPlan BindNonLandEvidence(NonLandEvidenceActionPlan plan, CognitionNonLandChoice? terms) =>
        plan.Bound || plan.Choice.Verb is not ("law_case_statement" or "law_case_advise" or "law_case_answer") ? plan :
            plan with { Bound = true, Statement = CognitionDecisionResponse.NormalizeIdentityText(terms?.Statement) };

    internal static ActionCheck CheckNonLandEvidence(ITownWorld world, NonLandEvidenceActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandEvidenceCapabilities(world, plan));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            NonLandActionToken(item) != plan.Choice.CaseToken || TownHearingProcedure.Digest(NonLandRevision(item)) != TownHearingProcedure.Digest(plan.Revision))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case material or noticed response parties have changed");
        if (plan.Choice.Verb == "law_case_inspect")
        {
            if (CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town) || !NonLandMayInspect(world, town, item, plan.Actor) ||
                NonLandReadCurrent(world, item, plan.Actor) && !item.ReopenRequests.Any(request => !item.Reads.Any(read =>
                    read.AgentId == plan.Actor && read.ReopenRequestIds.Contains(request.Id, StringComparer.Ordinal))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they cannot inspect that selected public file now");
        }
        else if (!NonLandKnows(world, town, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they have not received that selected case notice");
        if (plan.Choice.Verb is "law_case_statement" or "law_case_evidence" && !NonLandMayInspect(world, town, item, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they no longer have standing to submit to that case");
        if (plan.Choice.Verb == "law_case_advise" && (!LandHearingAdult(world, plan.Actor) ||
            !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) || town.Government?.Arrangement.NonLand == TownArrangementRules.Mayor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council mediation authority has changed");
        if (plan.Choice.Verb is "law_case_inspect" or "law_case_evidence")
        {
            var available = NonLandKnownEvidence(world, town, item, plan.Actor);
            if (plan.Evidence.Any(original => !available.Any(current => TownHearingProcedure.Digest(current with { SubmittedTick = original.SubmittedTick }) ==
                TownHearingProcedure.Digest(original))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "a selected lawful source is no longer available");
            if (plan.Choice.Verb == "law_case_evidence" && !plan.Evidence.Any(evidence => !item.Evidence.Any(old =>
                old.SourceRecordId == evidence.SourceRecordId && old.SourceVersion == evidence.SourceVersion && old.SubmittedByAgentId == plan.Actor)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "those selected sources have already been submitted");
            if (plan.Choice.Verb == "law_case_inspect" && (plan.LawEvidence is null || !town.Government!.Laws.Any(law => law.Id == item.Allegation.LawId &&
                law.Versions.Any(version => version.Version == item.Allegation.LawVersion && TownLandHearingRules.LawVersion(version) == plan.LawEvidence.SourceVersion))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected historical law record is unavailable");
        }
        if (plan.Choice.Verb is "law_case_answer" or "law_case_waive" && (item.Status != "pending" || plan.Party is not { } party ||
            party.RespondingAdultId != plan.Actor || !NonLandParties(world, town, item).Contains(party) || !plan.Revision.Parties.Contains(party) ||
            item.Responses.Any(response => response.Revision == plan.Revision.Number && response.PartyId == party.Id)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected person's response or legitimate caregiver support has changed");
        if (plan.Choice.Verb == "law_case_relay" && (plan.SourceRead is null || TownNonLandRules.RelayReadRefusal(item, plan.Actor, plan.SourceRead, world.WorldTick) is not null || !NonLandReadCurrent(world, item, plan.Actor) || plan.Recipient is not { } recipient ||
            recipient == plan.Actor || !world.Agents.TryGetValue(recipient, out var target) ||
            !world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, target.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) ||
            !NonLandMayInspect(world, town, item, recipient) || NonLandReadCurrent(world, item, recipient)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected recipient or informed source is unavailable nearby");
        return plan.Bound && plan.Statement is null ? ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that submission needs a clear personal statement") : new();
    }

    private static void OfferNonLandEvidence(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownNonLandCase item, string verb, string target, string text, int priority)
    {
        var choice = new NonLandEvidenceChoice(town.Id, verb, NonLandActionToken(item), CivicAgentToken(target));
        if (PlanNonLandEvidence(world, actor, choice) is { } plan && CheckNonLandEvidence(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
    internal static NonLandFilingActionPlan? PlanNonLandFiling(ITownWorld world, string actor, NonLandFilingChoice choice,
        (TownViolationAllegation Allegation, TownCaseEvidence Evidence)? selectedReport = null)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            CivicBoard(world, town) is not { } board) return null;
        var report = selectedReport ?? NonLandKnownReports(world, town, actor).FirstOrDefault(report => NonLandReportToken(report.Allegation) == choice.ReportToken);
        if (report.Allegation is null || town.NonLandHearings.ConductRecords.SingleOrDefault(record => record.Id == report.Allegation.IncidentId) is not { } conduct ||
            town.NonLandHearings.Acquisitions.SingleOrDefault(acquisition => acquisition.Id == report.Evidence.Id && acquisition.AgentId == actor) is not { } acquired) return null;
        var office = choice.Verb == "law_case_file_town" ? town.Government?.Offices.SingleOrDefault(office =>
            office.Mandates == "ordinary" && office.HolderId == actor && office.TermEndTick > world.WorldTick) : null;
        var filing = new TownViolationFiling(actor, NonLandFilingKind(choice), world.WorldTick, report.Allegation.Statement,
            report.Allegation.SourceEvidenceIds, office?.ElectionId);
        return new(actor, choice, board, report.Allegation, report.Evidence, conduct, acquired, office, town.Council.Revision,
            TownNonLandPartyRules.CurrentParties(town, report.Allegation, [filing], world.Society, world.WorldTick));
    }

    internal static string NonLandFilingKind(NonLandFilingChoice choice) => choice.Verb switch
    {
        "law_case_file_affected" => "affected",
        "law_case_file_town" => "town",
        _ => "witness"
    };

    internal static NonLandFilingActionPlan BindNonLandFiling(NonLandFilingActionPlan plan, CognitionNonLandChoice? terms) =>
        plan.Bound ? plan : plan with { Bound = true, Statement = CognitionDecisionResponse.NormalizeIdentityText(terms?.Statement) };

    internal static ActionCheck CheckNonLandFiling(ITownWorld world, NonLandFilingActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected report notice place is unavailable");
        var acquisition = town.NonLandHearings.Acquisitions.SingleOrDefault(item => item.Id == plan.Acquisition.Id && item.AgentId == plan.Actor);
        var conduct = town.NonLandHearings.ConductRecords.SingleOrDefault(item => item.Id == plan.Conduct.Id);
        if (acquisition is null || conduct is null || TownHearingProcedure.Digest(acquisition) != TownHearingProcedure.Digest(plan.Acquisition) ||
            TownHearingProcedure.Digest(conduct) != TownHearingProcedure.Digest(plan.Conduct) || NonLandReportToken(plan.Allegation) != plan.Choice.ReportToken ||
            !conduct.Laws.Any(law => law.LawId == plan.Allegation.LawId && law.Version == plan.Allegation.LawVersion &&
                (KnowsConductLaw(town, plan.Actor, law) || acquisition.Kind == "record_inspection" && town.NonLandHearings.Cases.Any(file =>
                    file.Id == acquisition.PublicCaseId && file.Allegation.LawId == law.LawId && file.Allegation.LawVersion == law.Version))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected known act, source or historical law is unavailable");
        var existing = town.NonLandHearings.Cases.SingleOrDefault(item => item.Key == TownNonLandRules.CaseKey(town.Id, plan.Allegation));
        if (existing is not null && (existing.Status != "pending" || existing.Filings.Any(filing => filing.AgentId == plan.Actor &&
                (filing.Kind == "affected" || plan.Actor == existing.Allegation.SubjectId)) || plan.Choice.Verb == "law_case_file" &&
                existing.Filings.Any(filing => filing.AgentId == plan.Actor)) ||
            plan.Choice.Verb == "law_case_file_affected" && plan.Actor == plan.Allegation.SubjectId)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that exact report has already been filed or needs grounded reopening");
        if (plan.Choice.Verb == "law_case_file_town" && (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            town.Government?.Arrangement.Ordinary != TownArrangementRules.Mayor || plan.Office is null ||
            !town.Government.Offices.Contains(plan.Office) || plan.Office.TermEndTick <= world.WorldTick))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected ordinary Town office can no longer file that act");
        if (plan.Choice.Verb == "law_case_propose_town" && (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            town.Government?.Arrangement.Ordinary is not (TownArrangementRules.Council or TownArrangementRules.ElectedCouncil or TownArrangementRules.AllAdultCouncil) ||
            !town.Council.Members.Contains(plan.Actor, StringComparer.Ordinal) || town.Council.Revision != plan.CouncilRevision))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Council authorization has changed");
        var filing = new TownViolationFiling(plan.Actor, NonLandFilingKind(plan.Choice), world.WorldTick, plan.Statement ?? plan.Allegation.Statement,
            plan.Allegation.SourceEvidenceIds, plan.Office?.ElectionId);
        if (TownHearingProcedure.Digest(TownNonLandPartyRules.CurrentParties(town, plan.Allegation, [filing], world.Society, world.WorldTick)) !=
            TownHearingProcedure.Digest(plan.Parties))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected subject or legitimate response support has changed");
        if (!plan.Bound) return new();
        if (plan.Statement is null || !TownNonLandGovernmentFilingRules.IsValid(new(plan.Allegation with { Statement = plan.Statement }, plan.Statement), world.WorldTick))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected report needs a clear personal statement");
        if (plan.Choice.Verb == "law_case_propose_town" && TownCouncilRules.ProposalRetryRefusal(town.Council,
            TownNonLandGovernmentFilingRules.RequestKey(new(plan.Allegation, plan.Statement)),
            "council:" + plan.CouncilRevision.ToString(CultureInfo.InvariantCulture), world.WorldTick, CivicDay(world)) is { } retry)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, retry);
        if (TownNonLandRules.FilingRefusal(town.NonLandHearings, town.Id, plan.Allegation) is { } refusal)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, refusal);
        return new();
    }

    private static void OfferNonLandFiling(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        (TownViolationAllegation Allegation, TownCaseEvidence Evidence) report, string verb, string text, int priority)
    {
        var choice = new NonLandFilingChoice(town.Id, verb, NonLandReportToken(report.Allegation));
        if (PlanNonLandFiling(world, actor, choice, report) is { } plan && CheckNonLandFiling(world, plan).Allowed)
            candidates.Add(new(choice.Encode(), text, priority) { ActionPlan = plan });
    }
    private static string NonLandJudgeActionToken(TownNonLandCase item, NonLandJudgeKind kind) =>
        kind == NonLandJudgeKind.Vote && item.Contest is { } contest
            ? NonLandActionToken(item) + "#" + TownCaseJudgeRules.RoundToken(contest) : NonLandActionToken(item);

    internal static NonLandJudgeActionPlan? PlanNonLandJudge(ITownWorld world, string actor, NonLandJudgeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => NonLandJudgeActionToken(item, choice.Kind) == choice.RevisionOrRoundToken) is not { } item) return null;
        var contest = item.Contest;
        return new(actor, choice, item.Id, NonLandRevision(item),
            item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == actor && consent.WithdrawnTick is null), item.Judge,
            contest is null ? null : contest with
            {
                Voters = contest.Voters.ToArray(),
                Candidates = contest.Candidates.ToArray(),
                Ballots = contest.Ballots.ToArray(),
                TiedCandidates = contest.TiedCandidates.ToArray(),
                Rounds = contest.Rounds.ToArray()
            }, choice.Kind == NonLandJudgeKind.Vote ? ResolveCivicAgentToken(world, choice.CandidateToken) : null);
    }

    internal static ActionCheck CheckNonLandJudge(ITownWorld world, NonLandJudgeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            NonLandJudgeActionToken(item, plan.Choice.Kind) != plan.Choice.RevisionOrRoundToken ||
            TownHearingProcedure.Digest(NonLandRevision(item)) != TownHearingProcedure.Digest(plan.Revision) ||
            !TownNonLandRules.HasNoticeReceipt(NonLandRevision(item), plan.Actor, world.WorldTick, town.Council.Knowledge))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case, noticed revision or knowledge has changed");
        if (plan.Choice.Kind == NonLandJudgeKind.Vote)
        {
            if (plan.Contest is not { } original || item.Contest is not { Stage: "voting", RoundDeadlineTick: { } deadline } current || deadline <= world.WorldTick ||
                TownHearingProcedure.Digest(current with
                { Voters = original.Voters, Candidates = original.Candidates, Ballots = original.Ballots, TiedCandidates = original.TiedCandidates }) != TownHearingProcedure.Digest(original) ||
                !original.Voters.Contains(plan.Actor, StringComparer.Ordinal) || !current.Voters.Contains(plan.Actor, StringComparer.Ordinal) ||
                plan.Candidate is not { } candidate || CivicAgentToken(candidate) != plan.Choice.CandidateToken ||
                !original.Candidates.Contains(candidate, StringComparer.Ordinal) || !current.Candidates.Contains(candidate, StringComparer.Ordinal) ||
                !CivicHistory(town).Knows(plan.Actor, TownCaseJudgeRules.RoundToken(current)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case ballot round or candidate is no longer eligible");
            return new();
        }
        if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            !(item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case no longer needs their personal mandate response");
        var consent = item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == plan.Actor && consent.WithdrawnTick is null);
        return plan.Choice.Kind switch
        {
            NonLandJudgeKind.Register when plan.Consent is null && consent is null && TownCaseJudgeRules.Eligible(item, plan.Actor,
                TownAdults(world, town), LandHearingHouseholds(world), NonLandParties(world, town, item)) => new(),
            NonLandJudgeKind.Withdraw when plan.Consent is not null && consent == plan.Consent => new(),
            NonLandJudgeKind.Resign when plan.Judge is { Kind: "case_elected" } judge && judge.AgentId == plan.Actor && item.Judge == judge => new(),
            _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected consent or assigned case mandate has changed")
        };
    }

    private static void OfferNonLandJudge(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownNonLandCase item, NonLandJudgeKind kind, string target, string text, int priority)
    {
        var id = CivicAction(world, town.Id, NonLandJudgeChoice.Verb(kind), NonLandJudgeActionToken(item, kind), target);
        if (NonLandJudgeChoice.Decode(id) is { } choice && PlanNonLandJudge(world, actor, choice) is { } plan && CheckNonLandJudge(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
    internal static ActorCapabilityPolicy NonLandWorkCapabilities =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static ActorCapabilityPolicy NonLandReportRelayCapabilities =>
        new(ActionLifeStages.NonInfants, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: true, AllowActiveOrder: true);
    internal static NonLandWorkActionPlan? PlanNonLandWork(ITownWorld world, string actor, TownRuntimeState town,
        TownRestorativeAgreement agreement, TownRemedyTerm term)
    {
        var remaining = NonLandRemaining(town.NonLandHearings, agreement, term);
        if (remaining <= 0 || !NonLandWorkAllowed(world, actor) || !NonLandRemedyFeasible(world, town, term with { Quantity = remaining })) return null;
        var choice = new NonLandWorkChoice(town.Id, agreement.Id, term.Id);
        if (term.Kind == "return_goods")
            return NonLandReturnLot(world, term with { Quantity = remaining }) is { } lot ?
                new(actor, choice, agreement, term, remaining, NonLandWorkPhase.ReturnGoods, lot.Id, lot.ItemKind, remaining, null,
                    world.Agents[term.BeneficiaryId!].Position, [], null, null) : null;
        if (term.Kind == "public_service_goods")
        {
            var warehouse = world.WarehouseForResident(actor);
            if (warehouse is null || world.PersonalWarehouseSurplus(actor, term.ItemKind) is not { } surplus) return null;
            var travel = world.Agents[actor].Position != warehouse.Position;
            var quantity = Math.Min(remaining, Math.Min(WorldRuntime.CivicSharedRules.WarehouseLoadQuantity,
                Math.Min(world.DestinationRoom(warehouse.InstanceId), surplus.Quantity)));
            return new(actor, choice, agreement, term, remaining, travel ? NonLandWorkPhase.ServiceTravel : NonLandWorkPhase.ServiceDelivery,
                surplus.Lot.Id, surplus.Lot.ItemKind, quantity, warehouse.InstanceId, warehouse.Position,
                world.ReachNonLandService(actor, warehouse.Position), null, null);
        }
        var person = world.Agents[actor];
        if (person.Equipment?.Repair is { } progress && progress.LotId == term.TargetId)
            return new(actor, choice, agreement, term, remaining, NonLandWorkPhase.ContinueRepair, term.TargetId, term.ItemKind, 1,
                null, person.Position, [], null, progress);
        return world.PlanNonLandRepair(actor, term.TargetId!) is { } repair ?
            new(actor, choice, agreement, term, remaining, NonLandWorkPhase.StartRepair, term.TargetId, term.ItemKind, 1, null,
                null, [], repair, null) : null;
    }

    internal static ActionCheck CheckNonLandWork(ITownWorld world, NonLandWorkActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandWorkCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { } town ||
            town.NonLandHearings.Agreements.SingleOrDefault(item => item.Id == plan.Agreement.Id) is not { Status: "pending" or "overdue" } agreement ||
            TownRemedyRules.IsSuperseded(town.NonLandHearings, agreement.Id) ||
            TownHearingProcedure.Digest(agreement with { Status = plan.Agreement.Status }) != TownHearingProcedure.Digest(plan.Agreement) ||
            !agreement.Terms.Contains(plan.Term) || plan.Term.ContributorId != plan.Actor ||
            NonLandRemaining(town.NonLandHearings, agreement, plan.Term) != plan.Remaining || !NonLandWorkAllowed(world, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected agreement, remaining contribution or personal work ability has changed");
        if (plan.Phase == NonLandWorkPhase.ReturnGoods)
            return NonLandReturnIsCurrent(world, plan.Term with { Quantity = plan.Quantity }, plan.StockId)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected return goods or recipient are unavailable");
        if (plan.Phase is NonLandWorkPhase.ServiceTravel or NonLandWorkPhase.ServiceDelivery)
        {
            if (world.WarehouseForResident(plan.Actor) is not { } warehouse || warehouse.InstanceId != plan.WarehouseId || warehouse.Position != plan.Destination ||
                warehouse.TownId != town.Id || plan.Term.BeneficiaryId != town.Id || plan.Term.TargetId is { } target && target != warehouse.InstanceId ||
                world.PersonalWarehouseSurplus(plan.Actor, plan.ItemKind, plan.StockId) is not { } selected || selected.Lot.Id != plan.StockId || selected.Quantity < plan.Quantity)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected service load or resident Warehouse is unavailable");
            if (plan.Phase == NonLandWorkPhase.ServiceDelivery)
                return world.Agents[plan.Actor].Position == plan.Destination && plan.Quantity > 0 && world.DestinationRoom(warehouse.InstanceId) >= plan.Quantity
                    ? new() : ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected service load cannot be delivered there now");
            return world.Agents[plan.Actor].Position != plan.Destination && world.ReachNonLandService(plan.Actor, warehouse.Position).Count > 0 &&
                world.DestinationRoom(warehouse.InstanceId) >= plan.Remaining &&
                world.PersonalWarehouseAvailableQuantity(plan.Actor, plan.ItemKind!) - WorldRuntime.CivicSharedRules.WarehouseLoadQuantity >= plan.Remaining ? new() :
                ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected service trip cannot reach its destination now");
        }
        if (plan.Phase == NonLandWorkPhase.StartRepair)
            return plan.Repair is { } repair ? world.CheckNativeActionPlan(repair) :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected native repair preparation is unavailable");
        var current = world.Agents[plan.Actor].Equipment?.Repair;
        return current is not null && plan.RepairProgress is { } original &&
            TownHearingProcedure.Digest(current with { WorkDone = original.WorkDone }) == TownHearingProcedure.Digest(original) &&
            world.NonLandRepairProgressIsCurrent(plan.Actor, original) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected repair or its reserved materials are unavailable");
    }

    internal static NonLandReportRelayActionPlan? PlanNonLandReportRelay(ITownWorld world, string actor, TownRuntimeState town,
        TownConductRecord record, string recipient)
    {
        var source = town.NonLandHearings.Acquisitions.FirstOrDefault(item => item.ConductId == record.Id && item.AgentId == actor);
        var parent = town.NonLandHearings.Acquisitions.Where(item => item.ConductId == record.Id && item.AgentId == actor)
            .OrderBy(item => item.Tick).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        return source is null || parent is null ? null : new(actor, new(town.Id, record.Id, recipient), record, parent, source.Text);
    }

    internal static ActionCheck CheckNonLandReportRelay(ITownWorld world, NonLandReportRelayActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandReportRelayCapabilities);
        if (!capability.Allowed) return capability;
        return world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is { } town &&
            town.NonLandHearings.ConductRecords.Any(record => TownHearingProcedure.Digest(record) == TownHearingProcedure.Digest(plan.Conduct)) &&
            town.NonLandHearings.Acquisitions.Contains(plan.Source) && town.NonLandHearings.Acquisitions.Any(item => item.ConductId == plan.Conduct.Id &&
                item.AgentId == plan.Actor && item.Text == plan.Statement) && !town.NonLandHearings.Acquisitions.Any(item => item.ConductId == plan.Conduct.Id && item.AgentId == plan.Choice.Recipient) &&
            world.Agents.TryGetValue(plan.Choice.Recipient, out var recipient) && recipient.AgentId != plan.Actor &&
            world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, recipient.Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected known source or nearby listener has changed");
    }
}
