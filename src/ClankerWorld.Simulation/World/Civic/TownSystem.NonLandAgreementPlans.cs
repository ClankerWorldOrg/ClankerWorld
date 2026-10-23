using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
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
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
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
}
