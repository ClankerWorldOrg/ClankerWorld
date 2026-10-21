namespace ClankerWorld.Simulation.World;

public static partial class TownNonLandValidation
{
    private static void ValidateLocalRemedies(TownNonLandHearingState state)
    {
        foreach (var offer in state.Offers) ValidateLocalOffer(state, offer);
        foreach (var agreement in state.Agreements) ValidateLocalAgreement(state, agreement);
        Unique(state.Effects.Select(e => e.NativeReceiptId.Value));
        foreach (var effect in state.Effects)
        {
            var agreement = state.Agreements.SingleOrDefault(a => a.Id == effect.AgreementId);
            var term = agreement?.Terms.SingleOrDefault(t => t.Id == effect.TermId);
            Check(agreement is not null && term is not null && effect.Tick >= agreement.AcceptedTick &&
                effect.Quantity > 0 && effect.ActorId == term.ContributorId && effect.Kind == term.Kind &&
                effect.BeneficiaryId == term.BeneficiaryId && effect.ItemKind == term.ItemKind && effect.TargetId == term.TargetId &&
                !state.Agreements.Any(a => a.ReplacesAgreementId == agreement.Id && effect.Tick > a.AcceptedTick),
                "A performance receipt must be the agreed contributor's actual later effect, with exact goods or work.");
        }
    }

    private static void ValidateLocalOffer(TownNonLandHearingState state, TownRemedyOffer offer)
    {
        var item = state.Cases.SingleOrDefault(c => c.Id == offer.CaseId);
        var finding = item?.Findings.SingleOrDefault(f => f.Id == offer.FindingId);
        Check(finding is { Result: "supported" } && offer.PublishedTick >= finding.Tick &&
            offer.Revision > 0 && offer.ResponseDeadlineTick >= offer.PublishedTick && offer.CompletionTicks > 0 &&
            offer.TermsHash == TownRemedyRules.TermsHash(offer.Terms, offer.CompletionTicks) && BoundedText.IsValid(offer.Reason, 1024, requireTrimmed: true) &&
            offer.Status is "pending" or "accepted" or "declined" or "unanswered" or "countered" or "superseded",
            "A voluntary offer needs its actual finding, publication, exact terms and response window.");
        Unique(offer.Responses.Select(r => r.AgentId));
        foreach (var response in offer.Responses)
            Check(offer.Terms.Any(t => t.ContributorId == response.AgentId) && response.Revision == offer.Revision && response.TermsHash == offer.TermsHash &&
                response.Kind is "accept" or "decline" or "counter" && response.Tick >= offer.PublishedTick && response.Tick < offer.ResponseDeadlineTick &&
                response.NoticeId == offer.NoticeId,
                "Each remedy response must be that contributor's informed answer to the exact offer.");
        Check(offer.Status switch
        {
            "pending" => offer.AgreementId is null && offer.Responses.All(r => r.Kind == "accept") && MissingConsent(offer),
            "accepted" or "superseded" => offer.AgreementId is not null && state.Agreements.Any(a => a.Id == offer.AgreementId && a.OfferId == offer.Id),
            "declined" => offer.AgreementId is null && offer.Responses.Any(r => r.Kind == "decline"),
            "countered" => offer.AgreementId is null && offer.Responses.Any(r => r.Kind == "counter"),
            "unanswered" => offer.AgreementId is null && offer.Responses.All(r => r.Kind == "accept") && MissingConsent(offer),
            _ => false,
        }, "Offer status cannot invent consent or conceal decline, silence or a counteroffer.");
        if (offer.ReplacesOfferId is { } replaced)
            Check(state.Offers.TakeWhile(o => o.Id != offer.Id).Any(o => o.Id == replaced && o.CaseId == offer.CaseId && o.PublishedTick <= offer.PublishedTick &&
                o.Revision + 1 == offer.Revision && o.Status is "countered" or "accepted" or "superseded"), "A counteroffer must retain the prior exact offer.");
    }
    private static void ValidateLocalAgreement(TownNonLandHearingState state, TownRestorativeAgreement agreement)
    {
        var offer = state.Offers.SingleOrDefault(o => o.Id == agreement.OfferId);
        Check(offer is not null && offer.AgreementId == agreement.Id && agreement.OfferRevision == offer.Revision && agreement.TermsHash == offer.TermsHash &&
            agreement.Terms.SequenceEqual(offer.Terms) && agreement.Consents.SequenceEqual(offer.Responses) && agreement.Consents.Count > 0 &&
            agreement.Consents.All(c => c.Kind == "accept") && agreement.Terms.All(t => agreement.Consents.Any(c => c.AgentId == t.ContributorId)) &&
            agreement.AcceptedTick == agreement.Consents.Max(c => c.Tick) &&
            agreement.DeadlineTick >= agreement.AcceptedTick && agreement.DeadlineTick - agreement.AcceptedTick == offer.CompletionTicks, "An agreement needs every contributor's own exact consent and recorded deadline.");
        var complete = true;
        foreach (var term in agreement.Terms)
        {
            var quantity = state.Effects.Where(e => e.AgreementId == agreement.Id && e.TermId == term.Id).Sum(e => (long)e.Quantity);
            Check(quantity >= 0 && quantity <= term.Quantity, "Performance cannot exceed or invent the agreed quantity.");
            complete &= quantity == term.Quantity;
        }
        Check((complete ? agreement.Status == "completed" : agreement.Status is "pending" or "overdue"), "Compliance must reflect actual physical performance and the agreed deadline.");
        var previousOffer = TownRemedyRules.ReplacedAgreementOffer(state, offer);
        Check(agreement.ReplacesAgreementId == previousOffer?.AgreementId,
            "An agreement must retain its exact predecessor through the counteroffer history.");
        if (agreement.ReplacesAgreementId is { } replaced)
            Check(replaced != agreement.Id && state.Agreements.TakeWhile(a => a.Id != agreement.Id).Any(a => a.Id == replaced && a.AcceptedTick <= agreement.AcceptedTick && previousOffer!.Id == a.OfferId) &&
                state.Agreements.Count(a => a.ReplacesAgreementId == replaced) == 1, "Renegotiation must retain one exact earlier agreement.");
    }
}
