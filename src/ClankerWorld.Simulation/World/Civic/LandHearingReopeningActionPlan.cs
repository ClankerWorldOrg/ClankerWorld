namespace ClankerWorld.Simulation.World;

public sealed record LandHearingReopeningChoice(string TownId, string RevisionToken, bool Assessment, string GroundsOrRequestToken)
{
    public string Encode() => $"civic|{TownId}|{(Assessment ? "hearing_assess_reopen" : "hearing_reopen")}|{RevisionToken}|{GroundsOrRequestToken}";
    public static LandHearingReopeningChoice? Decode(string value) =>
        value.Split('|') is ["civic", var town, var verb, var revision, var terms] && IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(revision, DerivedId.MaximumLength) && IdentityRules.Reference(terms, DerivedId.MaximumLength) &&
        verb is "hearing_reopen" or "hearing_assess_reopen" ? new(town, revision, verb == "hearing_assess_reopen", terms) : null;
}

public sealed record LandHearingReopeningActionPlan(string ActorId, LandHearingReopeningChoice Choice, string CaseId,
    TownLandCaseRevision Revision, TownLandRuling Ruling, TownLandReopenRequest? Request, bool Accept,
    TownLandCaseJudge? Judge, TownPropertySnapshot? PropertySnapshot, IReadOnlyList<string>? EvidenceIds = null,
    string? Reasons = null, bool Bound = false, bool InvalidTerms = false) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => Choice.Assessment ? "assess that selected reopening request" : "request reopening of that selected ruling";
}
