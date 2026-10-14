using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static string[] FoodPolicyVoters(ICivicWorld world) => world.Society.Agents.Where(person =>
            person.Status == SocietyAgentStatus.Active && person.HouseholdId is not null &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
        .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();

    internal static int ContributionScore(ICivicWorld world, string actor) => world.Society.Memories.Count(memory => memory.SubjectId == actor &&
            (memory.Id.StartsWith("project-gratitude:", StringComparison.Ordinal) || memory.Id.StartsWith("trust-memory:", StringComparison.Ordinal) ||
                memory.Id.StartsWith("lesson-gratitude:", StringComparison.Ordinal))) +
        (world.Agents.GetValueOrDefault(actor)?.Project?.Stage == "completed" ? 1 : 0);

    internal static int SharedFoodQuantity(ICivicWorld world) => world.Society.Inventory.Lots.Where(lot =>
            world.Society.Households.Any(household => household.Id == lot.OwnerId) && world.IsEdibleFood(lot.ItemKind))
        .Sum(world.AvailableLotQuantity);

    internal string? ProposedFoodPolicy(ICivicWorld world, string actor)
    {
        if (foodStewardship is null || foodStewardship.StewardId != actor || !FoodPolicyVoters(world).Contains(actor, StringComparer.Ordinal) ||
            foodStewardship.Ballot is not null || world.WorldTick - foodStewardship.LastResolutionTick < 300)
        {
            return null;
        }
        var quantity = SharedFoodQuantity(world);
        return foodStewardship.FoodPolicy == "open" && quantity < world.Agents.Count * 2 ? "essential_first"
            : foodStewardship.FoodPolicy == "essential_first" && quantity >= world.Agents.Count * 4 ? "open" : null;
    }

    internal bool HasFoodPolicyDecision(ICivicWorld world, string actor) => !world.NeedsUrgentWarmth(world.Agents[actor]) && (
        foodStewardship?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
        !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal) ||
        ProposedFoodPolicy(world, actor) is not null);

    internal int SharedFoodCollectionAllowance(ICivicWorld world, string actor) => foodStewardship?.FoodPolicy != "essential_first" ||
        world.Agents[actor].FullnessBasisPoints < NeedThresholds.RoutineFoodSeekFullness
            ? int.MaxValue
            : Math.Max(0, SharedFoodQuantity(world) - world.Agents.Count);

    internal bool MayCollectSharedFood(ICivicWorld world, string actor) => SharedFoodCollectionAllowance(world, actor) > 0;

    internal void AdvanceFoodStewardship(ICivicWorld world)
    {
        if (!world.HasSurvivalState && foodStewardship is null)
        {
            return;
        }
        if (foodStewardship is null)
        {
            foodStewardship = new(null, "open", world.WorldTick);
        }
        var members = FoodPolicyVoters(world);
        if (foodStewardship.StewardId is null || !members.Contains(foodStewardship.StewardId, StringComparer.Ordinal))
        {
            var successor = members.Where(id => foodStewardship.StewardId is not null || ContributionScore(world, id) > 0)
                .OrderByDescending(id => ContributionScore(world, id)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (successor != foodStewardship.StewardId)
            {
                foodStewardship = foodStewardship with { StewardId = successor };
                world.AppendEvent("food_steward_changed", successor ?? "none");
            }
        }
        ResolveFoodPolicyBallot(world);
    }

    internal void ResolveFoodPolicyBallot(ICivicWorld world)
    {
        if (foodStewardship?.Ballot is not { } ballot)
        {
            return;
        }
        var living = FoodPolicyVoters(world).Intersect(ballot.Electorate, StringComparer.Ordinal).ToArray();
        var required = living.Length / 2 + 1;
        var approvals = ballot.Approvals.Count(id => living.Contains(id, StringComparer.Ordinal));
        var rejections = ballot.Rejections.Count(id => living.Contains(id, StringComparer.Ordinal));
        if (world.WorldTick <= ballot.ExpiryTick && living.Length > 0 && approvals >= required)
        {
            foodStewardship = foodStewardship with { FoodPolicy = ballot.Policy, LastResolutionTick = world.WorldTick, Ballot = null };
            world.AppendEvent("food_policy_adopted", ballot.Policy);
        }
        else if (living.Length == 0 || rejections > living.Length - required || world.WorldTick > ballot.ExpiryTick)
        {
            foodStewardship = foodStewardship with { LastResolutionTick = world.WorldTick, Ballot = null };
            world.AppendEvent("food_policy_rejected", ballot.Policy);
        }
    }

    internal void AddFoodPolicyCandidates(ICivicWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (world.NeedsUrgentWarmth(world.Agents[actor]))
        {
            return;
        }
        if (ProposedFoodPolicy(world, actor) is { } policy)
        {
            var plan = PlanFoodPolicy(actor, "food_policy_propose:" + policy)!;
            if (CheckFoodPolicy(world, plan).Allowed) candidates.Add(new(plan.CandidateId, policy == "essential_first"
                ? "Ask the household to reserve scarce shared food for hungry members; a majority must agree."
                : "Ask the household to reopen plentiful shared food to everyone.", 14)
            { ActionPlan = plan });
        }
        if (foodStewardship?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
            !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal))
        {
            var helps = ballot.Policy == "essential_first" ? SharedFoodQuantity(world) < world.Agents.Count * 2 : SharedFoodQuantity(world) >= world.Agents.Count * 4;
            var description = ballot.Policy == "essential_first" ? "reserve the last shared servings for hungry members" : "restore open shared-food access";
            var yes = PlanFoodPolicy(actor, "food_policy_vote_yes")!;
            var no = PlanFoodPolicy(actor, "food_policy_vote_no")!;
            if (CheckFoodPolicy(world, yes).Allowed) candidates.Add(new(yes.CandidateId, "Vote to " + description + ".", helps ? 13 : 60) { ActionPlan = yes });
            if (CheckFoodPolicy(world, no).Allowed) candidates.Add(new(no.CandidateId, "Reject the proposed food policy; retain the existing access rule.", helps ? 60 : 13) { ActionPlan = no });
        }
    }

    internal void ApplyFoodPolicyCandidate(ICivicWorld world, string actor, string candidate, ActionPlan? originalPlan = null)
    {
        var plan = originalPlan as FoodPolicyActionPlan ?? PlanFoodPolicy(actor, candidate);
        if (plan is null) return;
        if (CheckFoodPolicy(world, plan).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        if (candidate.StartsWith("food_policy_propose:", StringComparison.Ordinal))
        {
            var policy = plan.Choice.Policy;
            if (policy != ProposedFoodPolicy(world, actor) || foodStewardship is null)
            {
                return;
            }
            foodStewardship = foodStewardship with { Ballot = new(policy, world.WorldTick, world.WorldTick + 120, FoodPolicyVoters(world), [actor], []) };
            world.AppendEvent("food_policy_proposed", policy);
        }
        else if (foodStewardship?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
                 !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal))
        {
            foodStewardship = foodStewardship with
            {
                Ballot = plan.Choice.Approve == true
                    ? ballot with { Approvals = ballot.Approvals.Append(actor).Order(StringComparer.Ordinal).ToArray() }
                    : ballot with { Rejections = ballot.Rejections.Append(actor).Order(StringComparer.Ordinal).ToArray() },
            };
            world.AppendEvent("food_policy_vote_recorded", actor);
        }
        ResolveFoodPolicyBallot(world);
    }

    internal static void ValidateFoodStewardship(WorldRuntimeState state)
    {
        if (state.FoodStewardship is not { } saved)
        {
            return;
        }
        var tick = state.Society.Society.WorldTick;
        var known = state.Society.Society.Agents.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        if (saved.FoodPolicy is not ("open" or "essential_first") ||
            saved.LastResolutionTick < 0 || saved.LastResolutionTick > tick ||
            saved.StewardId is not null && !known.Contains(saved.StewardId))
        {
            throw new InvalidDataException("The saved household council is invalid.");
        }
        if (saved.Ballot is { } ballot && (ballot.Policy is not ("open" or "essential_first") || ballot.Policy == saved.FoodPolicy ||
            ballot.Electorate is null || ballot.Approvals is null || ballot.Rejections is null ||
            ballot.ProposedTick < 0 || ballot.ProposedTick > tick || ballot.ExpiryTick < ballot.ProposedTick || ballot.ExpiryTick - ballot.ProposedTick != 120 ||
            ballot.Electorate.Count == 0 || ballot.Electorate.Distinct(StringComparer.Ordinal).Count() != ballot.Electorate.Count ||
            ballot.Electorate.Any(id => !known.Contains(id)) ||
            ballot.Approvals.Concat(ballot.Rejections).Distinct(StringComparer.Ordinal).Count() != ballot.Approvals.Count + ballot.Rejections.Count ||
            ballot.Approvals.Concat(ballot.Rejections).Any(id => !ballot.Electorate.Contains(id, StringComparer.Ordinal))))
        {
            throw new InvalidDataException("The saved household ballot is invalid.");
        }
    }
}
