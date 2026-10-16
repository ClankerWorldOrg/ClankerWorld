namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal FoodPolicyActionPlan? PlanFoodPolicy(string actor, string candidate)
    {
        if (foodStewardship is not { } state) return null;
        var propose = candidate.StartsWith("food_policy_propose:", StringComparison.Ordinal);
        if (!propose && candidate is not ("food_policy_vote_yes" or "food_policy_vote_no")) return null;
        var policy = propose ? candidate["food_policy_propose:".Length..] : state.Ballot?.Policy;
        return policy is null ? null : new(actor, new(policy, propose ? null : candidate == "food_policy_vote_yes"),
            state.StewardId, state.FoodPolicy, state.LastResolutionTick, state.Ballot);
    }

    internal ActionCheck CheckFoodPolicy(ICivicWorld world, FoodPolicyActionPlan plan)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if (foodStewardship is not { } state || state.StewardId != plan.StewardId || state.FoodPolicy != plan.CurrentPolicy ||
            state.LastResolutionTick != plan.LastResolutionTick || !FoodPolicyVoters(world).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shared-food policy or voting authority has changed");
        if (plan.Choice.Approve is null)
            return state.Ballot is null && ProposedFoodPolicy(world, plan.Actor) == plan.Choice.Policy ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected policy proposal is no longer available");
        return state.Ballot is { } ballot && plan.Ballot is { } original && ballot.Policy == original.Policy &&
            ballot.ProposedTick == original.ProposedTick && ballot.ExpiryTick == original.ExpiryTick &&
            ballot.Electorate.SequenceEqual(original.Electorate) && ballot.Electorate.Contains(plan.Actor, StringComparer.Ordinal) &&
            !ballot.Approvals.Contains(plan.Actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(plan.Actor, StringComparer.Ordinal)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the original ballot or their personal response has changed");
    }
}
