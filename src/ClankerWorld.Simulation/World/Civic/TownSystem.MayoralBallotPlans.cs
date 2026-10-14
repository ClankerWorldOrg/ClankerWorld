using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static MayoralBallotActionPlan? PlanMayoralBallot(ITownWorld world, string actor, MayoralBallotChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Government?.Contest is not { } contest) return null;
        return new(actor, choice, contest with
        {
            Voters = contest.Voters.ToArray(),
            Candidates = contest.Candidates.ToArray(),
            Ballots = contest.Ballots.ToArray(),
            TiedCandidates = contest.TiedCandidates.ToArray(),
            Rounds = contest.Rounds.ToArray()
        },
            ResolveCivicAgentToken(world, choice.CandidateToken));
    }

    internal static ActionCheck CheckMayoralBallot(ITownWorld world, MayoralBallotActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not
            { Council: not null, Government.Contest: { Stage: "voting" } current } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            current with
            {
                Voters = plan.Contest.Voters,
                Candidates = plan.Contest.Candidates,
                Ballots = plan.Contest.Ballots,
                TiedCandidates = plan.Contest.TiedCandidates,
                Rounds = plan.Contest.Rounds
            } != plan.Contest ||
            !current.Voters.SequenceEqual(plan.Contest.Voters) || !current.Candidates.SequenceEqual(plan.Contest.Candidates) ||
            !current.TiedCandidates.SequenceEqual(plan.Contest.TiedCandidates) || !current.Rounds.SequenceEqual(plan.Contest.Rounds) ||
            TownGovernmentRules.RoundToken(current) != plan.Choice.RoundToken || world.WorldTick >= current.RoundDeadlineTick ||
            !current.Voters.Contains(plan.Actor, StringComparer.Ordinal) || !current.Candidates.Contains(plan.Candidate, StringComparer.Ordinal) ||
            !CivicHistory(town).Knows(plan.Actor, plan.Choice.RoundToken))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected mayoral round, candidate or voting window has changed");
        return new();
    }

    private static void OfferMayoralBallot(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, TownMayoralContest contest, string candidate, string text, int priority)
    {
        var id = CivicAction(world, town.Id, "mayor_vote", TownGovernmentRules.RoundToken(contest), candidate);
        if (MayoralBallotChoice.Decode(id) is { } choice && PlanMayoralBallot(world, actor, choice) is { } plan && CheckMayoralBallot(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
