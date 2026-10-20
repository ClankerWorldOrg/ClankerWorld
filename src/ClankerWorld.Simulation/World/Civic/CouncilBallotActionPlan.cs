namespace ClankerWorld.Simulation.World;

public sealed record CouncilBallotChoice(string TownId, string RoundToken, string? CandidateToken)
{
    public string Encode() => $"civic|{TownId}|{(CandidateToken is null ? "ballot" : "single")}|{RoundToken}|{CandidateToken ?? ""}";
    public static CouncilBallotChoice? Decode(string value) => value.Split('|') is ["civic", var town, var verb, var round, var candidate] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(round, DerivedId.MaximumLength) &&
        (verb == "ballot" && candidate is "" || verb == "single" && IdentityRules.Reference(candidate, DerivedId.MaximumLength))
            ? new(town, round, verb == "single" ? candidate : null) : null;
}

public sealed record CouncilBallotActionPlan(string ActorId, CouncilBallotChoice Choice, string ElectionId, string Stage,
    long OpenedTick, long Deadline, int Seats, IReadOnlyList<string> Candidates, IReadOnlyList<string> Voters,
    IReadOnlyList<string>? Selected) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "cast that council election ballot";
}
