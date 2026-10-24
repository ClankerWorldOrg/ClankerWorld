using ClankerWorld.Simulation.Cognition;
using System.Globalization;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private static void OfferCouncilBallot(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, string? candidate, string text, int priority)
    {
        if (town.Council?.Election is not { } election) return;
        var id = CivicAction(world, town.Id, candidate is null ? "ballot" : "single", CivicRoundToken(election), candidate ?? "");
        if (CouncilBallotChoice.Decode(id) is { } choice && PlanCouncilBallot(world, actor, choice) is { } plan &&
            CheckCouncilBallot(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }

    internal static CouncilBallotActionPlan? PlanCouncilBallot(ITownWorld world, string actor, CouncilBallotChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Council?.Election is not { } election) return null;
        return new(actor, choice, election.Id, election.Stage, election.OpenedTick, election.DeadlineTick, election.Seats,
            election.Candidates.ToArray(), election.Voters.ToArray(),
            choice.CandidateToken is { } candidate ? [ResolveCivicAgentToken(world, candidate)] : null);
    }

    internal static CouncilBallotActionPlan BindCouncilBallot(ITownWorld world, CouncilBallotActionPlan plan, IReadOnlyList<string>? choices) =>
        plan.Choice.CandidateToken is not null || plan.Selected is not null ? plan : plan with
        { Selected = choices?.Select(token => ResolveCivicAgentToken(world, token)).ToArray() };

    internal static ActionCheck CheckCouncilBallot(ITownWorld world, CouncilBallotActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: { Election: { } election } } town ||
            election.Id != plan.ElectionId || election.Stage != plan.Stage || election.Stage is not ("main" or "runoff") ||
            election.OpenedTick != plan.OpenedTick || election.DeadlineTick != plan.Deadline || world.WorldTick >= plan.Deadline ||
            CivicRoundToken(election) != plan.Choice.RoundToken || election.Seats != plan.Seats ||
            !plan.Voters.Contains(plan.Actor, StringComparer.Ordinal) || !election.Voters.Contains(plan.Actor, StringComparer.Ordinal) ||
            !CivicHistory(town).Knows(plan.Actor, election.Stage == "main" ? "election" : "runoff", election.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected council election round is no longer open to them");
        return plan.Selected is not { } selected || selected.Count <= plan.Seats &&
            selected.Distinct(StringComparer.Ordinal).Count() == selected.Count &&
            selected.All(id => plan.Candidates.Contains(id, StringComparer.Ordinal) && election.Candidates.Contains(id, StringComparer.Ordinal))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected ballot contains candidates no longer eligible in that round");
    }
    private static void OfferCouncilConsent(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, CouncilConsentKind kind, string subject, string text, int priority)
    {
        var id = CivicAction(world, town.Id, CouncilConsentChoice.Verb(kind), subject);
        if (CouncilConsentChoice.Decode(id) is { } choice && PlanCouncilConsent(world, actor, choice) is { } plan &&
            CheckCouncilConsent(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }

    internal static CouncilConsentActionPlan? PlanCouncilConsent(ITownWorld world, string actor, CouncilConsentChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Council is not { } council) return null;
        var nominee = choice.Kind == CouncilConsentKind.Nominate ? ResolveCivicAgentToken(world, choice.SubjectToken) : null;
        return new(actor, choice, council.Candidates.SingleOrDefault(candidate => candidate.AgentId == actor),
            council.TermEndTick, nominee);
    }

    internal static ActionCheck CheckCouncilConsent(ITownWorld world, CouncilConsentActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId) is not { Council: { } council } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town council is no longer available to them");
        var agreement = council.Candidates.SingleOrDefault(candidate => candidate.AgentId == plan.Actor);
        var allowed = plan.Choice.Kind switch
        {
            CouncilConsentKind.Register => agreement?.FullTerm != true,
            CouncilConsentKind.Remainder => council.Form == "representative" && council.TermEndTick == plan.RemainderDeadline &&
                plan.RemainderDeadline is { } deadline && plan.Choice.SubjectToken == deadline.ToString(CultureInfo.InvariantCulture) &&
                agreement?.RemainderTermEndTick != deadline,
            CouncilConsentKind.WithdrawCandidate => agreement is not null && agreement == plan.Agreement,
            _ => plan.Nominee is { } nominee && nominee != plan.Actor && world.Agents.TryGetValue(nominee, out var person) &&
                TownAdults(world, town).Contains(nominee, StringComparer.Ordinal) &&
                world.IsWithinInteractionRange(person.Position, world.Agents[plan.Actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange) &&
                !council.Candidates.Any(candidate => candidate.AgentId == nominee && candidate.FullTerm) &&
                !council.Notices.Any(notice => notice.Kind == "nomination" && notice.SubjectId == TownRecordIds.Nomination(plan.Actor, nominee)),
        };
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected candidacy, term or nominee has changed");
    }
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
    internal static CivicProposalVoteActionPlan? PlanCivicProposalVote(ITownWorld world, string actor, CivicProposalVoteChoice choice) =>
        world.Towns.SingleOrDefault(town => town.Id == choice.TownId)?.Council?.Proposals.SingleOrDefault(proposal => proposal.Id == choice.ProposalId) is { } proposal
            ? new(actor, choice, proposal with { Voters = proposal.Voters.ToArray(), Votes = proposal.Votes.ToArray() }) : null;

    internal static ActionCheck CheckCivicProposalVote(ITownWorld world, CivicProposalVoteActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: { } council } town ||
            council.Proposals.SingleOrDefault(proposal => proposal.Id == plan.Proposal.Id) is not { Status: "pending" } current ||
            current.OpenedTick != plan.Proposal.OpenedTick || current.DeadlineTick != plan.Proposal.DeadlineTick ||
            current.CouncilRevision != plan.Proposal.CouncilRevision || current.Kind != plan.Proposal.Kind ||
            current.RequiredYes != plan.Proposal.RequiredYes || current.RequestKey != plan.Proposal.RequestKey ||
            current.Circumstances != plan.Proposal.Circumstances || current.Project != plan.Proposal.Project ||
            current.LandHearingRequest != plan.Proposal.LandHearingRequest || current.NonLandRequest != plan.Proposal.NonLandRequest ||
            !(current.LandClaimTiles ?? []).SequenceEqual(plan.Proposal.LandClaimTiles ?? []) ||
            current.SubjectId != plan.Proposal.SubjectId || current.AuthorId != plan.Proposal.AuthorId || current.Text != plan.Proposal.Text ||
            !current.Voters.SequenceEqual(plan.Proposal.Voters) || !CivicHistory(town).Knows(plan.Actor, current.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Town proposal or voting window has changed");
        var allowed = plan.Choice.Kind == CivicProposalVoteKind.Withdraw ? current.AuthorId == plan.Actor :
            world.WorldTick < current.DeadlineTick && current.Voters.Contains(plan.Actor, StringComparer.Ordinal) &&
            !current.Votes.Any(vote => vote.AgentId == plan.Actor) && !(current.Kind == "admission" && current.SubjectId == plan.Actor);
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that proposal response is final or no longer available");
    }

    private static void OfferCivicProposalVote(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownProposal proposal, CivicProposalVoteKind kind, string text, int priority)
    {
        var plan = new CivicProposalVoteActionPlan(actor, new(town.Id, kind, proposal.Id),
            proposal with { Voters = proposal.Voters.ToArray(), Votes = proposal.Votes.ToArray() });
        if (CheckCivicProposalVote(world, plan).Allowed) candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }
}
