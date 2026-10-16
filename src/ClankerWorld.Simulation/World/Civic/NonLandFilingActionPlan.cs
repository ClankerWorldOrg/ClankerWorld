namespace ClankerWorld.Simulation.World;

public sealed record NonLandFilingChoice(string TownId, string Verb, string ReportToken)
{
    public string Encode() => $"civic|{TownId}|{Verb}|{ReportToken}|";
    public static NonLandFilingChoice? Decode(string value) =>
        value.Split('|') is ["civic", var town, var verb, var report, ""] && IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(report, DerivedId.MaximumLength) &&
        verb is "law_case_file" or "law_case_file_affected" or "law_case_file_town" or "law_case_propose_town"
            ? new(town, verb, report) : null;
}

public sealed record NonLandFilingActionPlan(string ActorId, NonLandFilingChoice Choice, GridPoint NoticePlace,
    TownViolationAllegation Allegation, TownCaseEvidence Evidence, TownConductRecord Conduct, TownConductAcquisition Acquisition,
    TownOffice? Office, long CouncilRevision, IReadOnlyList<TownCaseParty> Parties, string? Statement = null, bool Bound = false)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "file that selected known act under its historical law";
}
