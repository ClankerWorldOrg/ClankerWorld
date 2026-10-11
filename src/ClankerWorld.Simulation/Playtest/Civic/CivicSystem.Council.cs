using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class CivicSystem
{
    internal static string[] CouncilMembers(ICivicWorld world) => world.Society.Inhabitants.Where(person =>
            person.Status == SocietyInhabitantStatus.Active && person.HouseholdId is not null &&
            person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
        .Select(person => person.Id).Order(StringComparer.Ordinal).ToArray();

    internal static int ContributionScore(ICivicWorld world, string actor) => world.Society.Memories.Count(memory => memory.SubjectId == actor &&
            (memory.Id.StartsWith("project-gratitude:", StringComparison.Ordinal) || memory.Id.StartsWith("settlement-trust:", StringComparison.Ordinal) ||
                memory.Id.StartsWith("lesson-gratitude:", StringComparison.Ordinal))) +
        (world.Inhabitants.GetValueOrDefault(actor)?.Project?.Stage == "completed" ? 1 : 0);

    internal static int SharedFoodQuantity(ICivicWorld world) => world.Society.Inventory.Lots.Where(lot =>
            world.Society.Households.Any(household => household.Id == lot.OwnerId) && world.IsEdibleFood(lot.ItemKind))
        .Sum(world.AvailableLotQuantity);

    internal string? ProposedFoodPolicy(ICivicWorld world, string actor)
    {
        if (council is null || council.StewardId != actor || !CouncilMembers(world).Contains(actor, StringComparer.Ordinal) ||
            council.Ballot is not null || world.WorldTick - council.LastResolutionTick < 300)
        {
            return null;
        }
        var quantity = SharedFoodQuantity(world);
        return council.FoodPolicy == "open" && quantity < world.Inhabitants.Count * 2 ? "essential_first"
            : council.FoodPolicy == "essential_first" && quantity >= world.Inhabitants.Count * 4 ? "open" : null;
    }

    internal bool HasCouncilDecision(ICivicWorld world, string actor) => !world.NeedsUrgentWarmth(world.Inhabitants[actor]) && (
        council?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
        !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal) ||
        ProposedFoodPolicy(world, actor) is not null);

    internal int SharedFoodCollectionAllowance(ICivicWorld world, string actor) => council?.FoodPolicy != "essential_first" ||
        world.Inhabitants[actor].HungerBasisPoints < 4_500
            ? int.MaxValue
            : Math.Max(0, SharedFoodQuantity(world) - world.Inhabitants.Count);

    internal bool MayCollectSharedFood(ICivicWorld world, string actor) => SharedFoodCollectionAllowance(world, actor) > 0;

    internal void AdvanceSettlementCouncil(ICivicWorld world)
    {
        if (!world.HasSurvivalState && council is null)
        {
            return;
        }
        if (council is null)
        {
            council = new(null, "open", world.WorldTick);
        }
        var members = CouncilMembers(world);
        if (council.StewardId is null || !members.Contains(council.StewardId, StringComparer.Ordinal))
        {
            var successor = members.Where(id => council.StewardId is not null || ContributionScore(world, id) > 0)
                .OrderByDescending(id => ContributionScore(world, id)).ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault();
            if (successor != council.StewardId)
            {
                council = council with { StewardId = successor };
                world.AppendEvent("council_steward_changed", successor ?? "none");
            }
        }
        ResolveCouncilBallot(world);
    }

    internal void ResolveCouncilBallot(ICivicWorld world)
    {
        if (council?.Ballot is not { } ballot)
        {
            return;
        }
        var living = CouncilMembers(world).Intersect(ballot.Electorate, StringComparer.Ordinal).ToArray();
        var required = living.Length / 2 + 1;
        var approvals = ballot.Approvals.Count(id => living.Contains(id, StringComparer.Ordinal));
        var rejections = ballot.Rejections.Count(id => living.Contains(id, StringComparer.Ordinal));
        if (world.WorldTick <= ballot.ExpiryTick && living.Length > 0 && approvals >= required)
        {
            council = council with { FoodPolicy = ballot.Policy, LastResolutionTick = world.WorldTick, Ballot = null };
            world.AppendEvent("council_policy_adopted", ballot.Policy);
        }
        else if (living.Length == 0 || rejections > living.Length - required || world.WorldTick > ballot.ExpiryTick)
        {
            council = council with { LastResolutionTick = world.WorldTick, Ballot = null };
            world.AppendEvent("council_policy_rejected", ballot.Policy);
        }
    }

    internal void AddCouncilCandidates(ICivicWorld world, List<CognitionCandidate> candidates, string actor)
    {
        if (world.NeedsUrgentWarmth(world.Inhabitants[actor]))
        {
            return;
        }
        if (ProposedFoodPolicy(world, actor) is { } policy)
        {
            candidates.Add(new("council_propose:" + policy, policy == "essential_first"
                ? "Ask the household to reserve scarce shared food for hungry members; a majority must agree."
                : "Ask the household to reopen plentiful shared food to everyone.", 14));
        }
        if (council?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
            !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal))
        {
            var helps = ballot.Policy == "essential_first" ? SharedFoodQuantity(world) < world.Inhabitants.Count * 2 : SharedFoodQuantity(world) >= world.Inhabitants.Count * 4;
            var description = ballot.Policy == "essential_first" ? "reserve the last shared servings for hungry members" : "restore open shared-food access";
            candidates.Add(new("council_vote_yes", "Vote to " + description + ".", helps ? 13 : 60));
            candidates.Add(new("council_vote_no", "Reject the proposed food policy; retain the existing access rule.", helps ? 60 : 13));
        }
    }

    internal void ApplyCouncilCandidate(ICivicWorld world, string actor, string candidate)
    {
        if (candidate.StartsWith("council_propose:", StringComparison.Ordinal))
        {
            var policy = candidate[16..];
            if (policy != ProposedFoodPolicy(world, actor) || council is null)
            {
                return;
            }
            council = council with { Ballot = new(policy, world.WorldTick, world.WorldTick + 120, CouncilMembers(world), [actor], []) };
            world.AppendEvent("council_policy_proposed", policy);
        }
        else if (council?.Ballot is { } ballot && ballot.Electorate.Contains(actor, StringComparer.Ordinal) &&
                 !ballot.Approvals.Contains(actor, StringComparer.Ordinal) && !ballot.Rejections.Contains(actor, StringComparer.Ordinal))
        {
            council = council with
            {
                Ballot = candidate == "council_vote_yes"
                    ? ballot with { Approvals = ballot.Approvals.Append(actor).Order(StringComparer.Ordinal).ToArray() }
                    : ballot with { Rejections = ballot.Rejections.Append(actor).Order(StringComparer.Ordinal).ToArray() },
            };
            world.AppendEvent("council_vote_recorded", actor);
        }
        ResolveCouncilBallot(world);
    }

    internal static void ValidateCouncil(PrivateWorldRuntimeState state)
    {
        if (state.Council is not { } saved)
        {
            return;
        }
        var tick = state.Society.Society.WorldTick;
        var known = state.Society.Society.Inhabitants.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
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
