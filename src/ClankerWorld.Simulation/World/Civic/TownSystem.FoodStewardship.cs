using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static string[] FoodPolicyVoters(ICivicWorld world) => world.Society.Agents.Where(person =>
            person.Status == SocietyAgentStatus.Active && person.HouseholdId is not null &&
            LifeStageRules.IsAdult(person.AgeBand))
        .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();

    internal static int ContributionScore(ICivicWorld world, string actor) => world.Society.Memories.Count(memory => memory.SubjectId == actor &&
            (memory.Id.Value.StartsWith("project-gratitude:", StringComparison.Ordinal) || memory.Id.Value.StartsWith("trust-memory:", StringComparison.Ordinal) ||
                memory.Id.Value.StartsWith("lesson-gratitude:", StringComparison.Ordinal))) +
        (world.Agents.GetValueOrDefault(actor)?.Project?.Stage == "completed" ? 1 : 0);

    internal static int SharedFoodQuantity(ICivicWorld world) => world.Society.Inventory.Lots.Where(lot =>
            world.Society.Households.Any(household => household.Id == lot.OwnerId) && world.IsEdibleFood(lot.ItemKind))
        .Sum(world.AvailableLotQuantity);

    internal string? ProposedFoodPolicy(ICivicWorld world, string actor)
    {
        if (State.FoodStewardship is null || State.FoodStewardship.StewardId != actor || !FoodPolicyVoters(world).Contains(actor, StringComparer.Ordinal) ||
            State.FoodStewardship.Ballot is not null || world.WorldTick - State.FoodStewardship.LastResolutionTick < 300)
        {
            return null;
        }
        var quantity = SharedFoodQuantity(world);
        return State.FoodStewardship.FoodPolicy == "open" && quantity < world.Agents.Count * 2 ? "essential_first"
            : State.FoodStewardship.FoodPolicy == "essential_first" && quantity >= world.Agents.Count * 4 ? "open" : null;
    }

    internal bool HasFoodPolicyDecision(ICivicWorld world, string actor) => !world.NeedsUrgentWarmth(world.Agents[actor]) && (
        State.FoodStewardship?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
        !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal) ||
        ProposedFoodPolicy(world, actor) is not null);

    internal int SharedFoodCollectionAllowance(ICivicWorld world, string actor) => State.FoodStewardship?.FoodPolicy != "essential_first" ||
        world.Agents[actor].FullnessBasisPoints < NeedThresholds.RoutineFoodSeekFullness
            ? int.MaxValue
            : Math.Max(0, SharedFoodQuantity(world) - world.Agents.Count);

    internal bool MayCollectSharedFood(ICivicWorld world, string actor) => SharedFoodCollectionAllowance(world, actor) > 0;

    internal void AdvanceFoodStewardship(ICivicWorld world)
    {
        if (!world.HasSurvivalState && State.FoodStewardship is null)
        {
            return;
        }
        if (State.FoodStewardship is not { } stewardship)
        {
            stewardship = new(null, "open", world.WorldTick);
            RestoreFoodStewardship(stewardship);
        }
        var members = FoodPolicyVoters(world);
        if (stewardship.StewardId is null || !members.Contains(stewardship.StewardId, StringComparer.Ordinal))
        {
            var successor = members.Where(id => stewardship.StewardId is not null || ContributionScore(world, id) > 0)
                .OrderByDescending(id => ContributionScore(world, id)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (successor != stewardship.StewardId)
            {
                RestoreFoodStewardship(stewardship with { StewardId = successor });
                world.AppendEvent(new FoodStewardChangedEventPayload(successor, successor ?? "none"));
            }
        }
        ResolveFoodPolicyBallot(world);
    }

    internal void ResolveFoodPolicyBallot(ICivicWorld world)
    {
        if (State.FoodStewardship?.Ballot is not { } ballot)
        {
            return;
        }
        var living = FoodPolicyVoters(world).Intersect(ballot.Electorate, StringComparer.Ordinal).ToArray();
        var required = living.Length / 2 + 1;
        var approvals = ballot.Approvals.Count(id => living.Contains(id, StringComparer.Ordinal));
        var rejections = ballot.Rejections.Count(id => living.Contains(id, StringComparer.Ordinal));
        if (world.WorldTick <= ballot.ExpiryTick && living.Length > 0 && approvals >= required)
        {
            RestoreFoodStewardship(State.FoodStewardship with { FoodPolicy = ballot.Policy, LastResolutionTick = world.WorldTick, Ballot = null });
            world.AppendEvent(new FoodPolicyAdoptedEventPayload(null, ballot.Policy));
        }
        else if (living.Length == 0 || rejections > living.Length - required || world.WorldTick > ballot.ExpiryTick)
        {
            RestoreFoodStewardship(State.FoodStewardship with { LastResolutionTick = world.WorldTick, Ballot = null });
            world.AppendEvent(new FoodPolicyRejectedEventPayload(null, ballot.Policy));
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
            var plan = PlanFoodPolicy(actor, new FoodPolicyChoice(policy, null).Encode())!;
            if (CheckFoodPolicy(world, plan).Allowed) candidates.Add(new(plan.CandidateId, policy == "essential_first"
                ? "Ask the household to reserve scarce shared food for hungry members; a majority must agree."
                : "Ask the household to reopen plentiful shared food to everyone.", 14)
            { ActionPlan = plan });
        }
        if (State.FoodStewardship?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
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
        if (RefuseIfNeeded(world, plan, CheckFoodPolicy(world, plan).Refusal)) return;
        ApplyCheckedFoodPolicy(world, plan);
    }

    internal void ApplyCheckedFoodPolicy(ICivicWorld world, FoodPolicyActionPlan plan)
    {
        var actor = plan.Actor;
        var candidate = plan.CandidateId;
        if (candidate.StartsWith("food_policy_propose:", StringComparison.Ordinal))
        {
            var policy = plan.Choice.Policy;
            if (policy != ProposedFoodPolicy(world, actor) || State.FoodStewardship is null)
            {
                return;
            }
            RestoreFoodStewardship(State.FoodStewardship with { Ballot = new(policy, world.WorldTick, world.WorldTick + 120, FoodPolicyVoters(world), [actor], []) });
            world.AppendEvent(new FoodPolicyProposedEventPayload(null, policy));
        }
        else if (State.FoodStewardship?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
                 !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal))
        {
            RestoreFoodStewardship(State.FoodStewardship with
            {
                Ballot = plan.Choice.Approve == true
                    ? ballot with { Approvals = ballot.Approvals.Append(actor).Order(StringComparer.Ordinal).ToArray() }
                    : ballot with { Rejections = ballot.Rejections.Append(actor).Order(StringComparer.Ordinal).ToArray() },
            });
            world.AppendEvent(new FoodPolicyVoteRecordedEventPayload(actor));
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
        ValidateLocalFoodStewardship(saved);
        if (saved.LastResolutionTick > tick || saved.StewardId is not null && !known.Contains(saved.StewardId))
            throw new InvalidDataException("The saved household council is invalid.");
        if (saved.Ballot is { } ballot && (ballot.ProposedTick > tick || ballot.Electorate.Any(id => !known.Contains(id))))
            throw new InvalidDataException("The saved household ballot is invalid.");
    }
}
