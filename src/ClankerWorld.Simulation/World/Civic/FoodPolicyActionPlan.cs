namespace ClankerWorld.Simulation.World;

public sealed record FoodPolicyChoice(string Policy, bool? Approve)
{
    public string Encode() => Approve is null ? IdentityRules.PrefixedReference("food_policy_propose:", Policy) : Approve.Value ? "food_policy_vote_yes" : "food_policy_vote_no";
}

public sealed record FoodPolicyActionPlan(string ActorId, FoodPolicyChoice Choice, string? StewardId,
    string CurrentPolicy, long LastResolutionTick, FoodPolicyBallot? Ballot)
    : ActionPlan(ActorId, Choice.Encode())
{
    public override string Activity => "respond to that original shared-food policy";
}
