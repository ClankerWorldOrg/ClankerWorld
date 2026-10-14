namespace ClankerWorld.Simulation.World;

public sealed record MayoralBallotChoice(string TownId, string RoundToken, string CandidateToken)
{
    public string Encode() => $"civic|{TownId}|mayor_vote|{RoundToken}|{CandidateToken}";
    public static MayoralBallotChoice? Decode(string value) => value.Split('|') is ["civic", var town, "mayor_vote", var round, var candidate] &&
        town.Length > 0 && round.Length > 0 && candidate.Length > 0 ? new(town, round, candidate) : null;
}

public sealed record MayoralBallotActionPlan(string ActorId, MayoralBallotChoice Choice,
    TownMayoralContest Contest, string Candidate) : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "cast that selected mayoral ballot";
}
