using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
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
