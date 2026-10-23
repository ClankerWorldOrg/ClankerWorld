
namespace ClankerWorld.Simulation.World;

public sealed record ExplorationChoice(bool Return, ExplorationGoal? Goal = null)
{
    public const string GoalPrefix = "explore_for:";
    public string Encode() => Return ? "explore_return" : Goal is null ? "explore" : IdentityRules.PrefixedReference(GoalPrefix, $"{Goal.Kind}:{Goal.Target}");
    public static ExplorationChoice? Decode(string candidate)
    {
        if (candidate == "explore") return new(false);
        if (candidate == "explore_return") return new(true);
        if (!candidate.StartsWith(GoalPrefix, StringComparison.Ordinal)) return null;
        var parts = candidate[GoalPrefix.Length..].Split(':');
        return parts.Length == 2 ? new(false, new(parts[0], parts[1])) : null;
    }
}

public enum ExplorationStep { Outward, Return, WaitReturn, CompleteReturn, CompleteGoal }
public sealed record ExplorationActionPlan(string ActorId, ExplorationChoice Choice, GridPoint Origin,
    ExplorationState? Original, ExplorationState Prepared, ExplorationStep Step, GridPoint? Target,
    bool Native, bool ReplaceGoal, bool Interrupted, bool Starting, IReadOnlyList<GridPoint> Route) : ActionPlan(ActorId, Choice.Encode())
{
    public ExplorationGoal? Goal => Prepared.Goal;
    public override string Activity => Choice.Return ? "return along their scouting trail" : "take the selected scouting step";
}
