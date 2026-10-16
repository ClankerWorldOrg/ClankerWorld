namespace ClankerWorld.Simulation.World;

public sealed record MayoralBallotChoice(string TownId, string RoundToken, string CandidateToken)
{
    public string Encode() => $"civic|{TownId}|mayor_vote|{RoundToken}|{CandidateToken}";
    public static MayoralBallotChoice? Decode(string value) => value.Split('|') is ["civic", var town, "mayor_vote", var round, var candidate] &&
        IdentityRules.Reference(town, DerivedId.MaximumLength, requireTrimmed: true) &&
        IdentityRules.Reference(round, DerivedId.MaximumLength) && IdentityRules.Reference(candidate, DerivedId.MaximumLength)
            ? new(town, round, candidate) : null;
}

public sealed record MayoralBallotActionPlan(string ActorId, MayoralBallotChoice Choice,
    TownMayoralContest Contest, string Candidate) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "cast that selected mayoral ballot";
}
