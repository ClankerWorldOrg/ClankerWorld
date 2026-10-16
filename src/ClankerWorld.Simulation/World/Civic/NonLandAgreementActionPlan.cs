namespace ClankerWorld.Simulation.World;

public sealed record NonLandAgreementChoice(string TownId, string Verb, string SubjectToken, string TermsToken)
{
    public string Encode() => $"civic|{TownId}|{Verb}|{SubjectToken}|{TermsToken}";
    public static NonLandAgreementChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var subject, var terms] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) && IdentityRules.Reference(subject, DerivedId.MaximumLength) &&
        (terms is "" || IdentityRules.Reference(terms, DerivedId.MaximumLength)) && verb is "remedy_offer" or "remedy_renegotiate" or "remedy_read" or "remedy_accept" or "remedy_decline" or "remedy_counter"
            ? new(town, verb, subject, terms) : null;
}

public sealed record NonLandAgreementReferences(IReadOnlyList<string> People, IReadOnlyList<string> Beneficiaries, IReadOnlyList<string> Physical);

public sealed record NonLandAgreementActionPlan(string ActorId, NonLandAgreementChoice Choice, TownNonLandCase Case,
    TownViolationFinding Finding, TownRemedyOffer? Offer, TownRestorativeAgreement? Agreement, GridPoint? NoticePlace,
    NonLandAgreementReferences References, long Sequence, IReadOnlyList<TownRemedyTerm>? Terms = null, long CompletionTicks = 0,
    string? Reason = null, bool Bound = false, bool InvalidTerms = false) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Verb switch
    {
        "remedy_read" => "read that selected voluntary offer",
        "remedy_accept" or "remedy_decline" => "respond to those selected personal contributions",
        "remedy_counter" => "counter that selected voluntary offer",
        "remedy_renegotiate" => "renegotiate that selected unfinished agreement",
        _ => "offer those selected voluntary contributions"
    };
}
