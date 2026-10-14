namespace ClankerWorld.Simulation.World;

public sealed record FoodPolicyChoice(string Policy, bool? Approve)
{
    public string Encode() => Approve is null ? "food_policy_propose:" + Policy : Approve.Value ? "food_policy_vote_yes" : "food_policy_vote_no";
    public static FoodPolicyChoice? Decode(string candidate) => candidate switch
    {
        "food_policy_vote_yes" => new("", true),
        "food_policy_vote_no" => new("", false),
        _ when candidate.StartsWith("food_policy_propose:", StringComparison.Ordinal) => new(candidate["food_policy_propose:".Length..], null),
        _ => null,
    };
}

public sealed record FoodPolicyActionPlan(string ActorId, FoodPolicyChoice Choice, string? StewardId,
    string CurrentPolicy, long LastResolutionTick, FoodPolicyBallot? Ballot)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to that original shared-food policy";
}
