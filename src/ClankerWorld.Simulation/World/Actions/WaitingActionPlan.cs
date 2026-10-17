namespace ClankerWorld.Simulation.World;

public sealed record WaitingChoice
{
    public const string Candidate = "safe_idle";
    public static string Encode() => Candidate;
}

public sealed record WaitingActionPlan(string ActorId, bool LegacyAgePermitted)
    : ActionPlan(ActorId, WaitingChoice.Candidate)
{
    public override string Activity => "continue safely";
}
