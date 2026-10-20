namespace ClankerWorld.Simulation.World;

public sealed record NonLandAdjudicationChoice(string TownId, string Verb, string CaseToken, string OutcomeOrRequestToken)
{
    public string Encode() => $"civic|{TownId}|{Verb}|{CaseToken}|{OutcomeOrRequestToken}";
    public static NonLandAdjudicationChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var item, var terms] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(item, DerivedId.MaximumLength) && IdentityRules.Reference(terms, DerivedId.MaximumLength) && verb is "law_case_find" or "law_case_reopen" or "law_case_assess_reopen"
            ? new(town, verb, item, terms) : null;
}

public sealed record NonLandAdjudicationActionPlan(string ActorId, NonLandAdjudicationChoice Choice, TownNonLandCase Case,
    IReadOnlyList<TownCaseParty> Parties, TownCaseJudge? Judge, TownCaseReopenRequest? Request, bool Accept,
    IReadOnlyList<string>? EvidenceIds = null, string? Reasons = null, string? Uncertainty = null, bool Bound = false)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Verb switch
    {
        "law_case_find" => "record that selected case finding",
        "law_case_reopen" => "request reopening of that selected finding",
        _ => "assess that selected reopening request"
    };
}
