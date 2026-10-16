namespace ClankerWorld.Simulation.World;

public sealed record LandHearingRulingChoice(string TownId, string RevisionToken, string OutcomeKind)
{
    public string Encode() => $"civic|{TownId}|hearing_rule|{RevisionToken}|{OutcomeKind}";
    public static LandHearingRulingChoice? Decode(string value) =>
        value.Split('|') is ["civic", var town, "hearing_rule", var revision, var outcome] && IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(revision, DerivedId.MaximumLength) &&
        outcome is "confirm" or "renew" or "amend" or "end" or "reject" or "reclaim" or "grant" ? new(town, revision, outcome) : null;
}

public sealed record LandHearingRulingActionPlan(string ActorId, LandHearingRulingChoice Choice, string CaseId,
    TownLandCaseRevision Revision, TownLandCaseJudge? Judge, IReadOnlyList<string> LapsedHouseholds,
    IReadOnlyList<HouseholdLandUseRight> AffectedRights, IReadOnlyList<HouseholdLandUseRequest> PendingRequests,
    TownPropertyRequest? PropertyRequest, TownPropertySnapshot? PropertySnapshot,
    TownLandRequestedOutcome? Outcome = null, IReadOnlyList<string>? EvidenceIds = null, IReadOnlyList<string>? LawIds = null,
    string? Reasons = null, bool Bound = false, bool InvalidTerms = false) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "make that selected land case ruling";
}
