using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static GovernmentProposalActionPlan? PlanGovernmentProposal(ITownWorld world, string actor, GovernmentProposalChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Government: { } government } town) return null;
        if (choice.Kind is GovernmentProposalKind.Propose or GovernmentProposalKind.ReplaceMayor)
            return TownArrangementRules.Parse(choice.Subject) is { } target
                ? new(actor, choice, government.Arrangement, target, CivicBoard(world, town), null) : null;
        var change = government.Changes.SingleOrDefault(change => change.Id == choice.Subject);
        return change is null ? null : new(actor, choice, government.Arrangement, null, null,
            change with { Voters = change.Voters.ToArray(), OpeningVoters = change.OpeningVoters.ToArray(), Votes = change.Votes.ToArray() });
    }

    internal static ActionCheck CheckGovernmentProposal(ITownWorld world, GovernmentProposalActionPlan plan, TownRuntimeState? currentTown = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true));
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not
            { Council: not null, Government: { } government } town || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected resident government proposal is no longer available");
        if (plan.Choice.Kind is GovernmentProposalKind.Propose or GovernmentProposalKind.ReplaceMayor)
            return government.Arrangement == plan.OriginalArrangement && plan.Target is { } target && TownArrangementRules.IsSupported(target) &&
                (plan.Choice.Kind == GovernmentProposalKind.Propose ? target != government.Arrangement :
                    target == government.Arrangement && TownArrangementRules.HasOffice(target)) &&
                CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected government arrangement or notice place has changed");
        var current = government.Changes.SingleOrDefault(change => change.Id == plan.Choice.Subject);
        if (current is null || plan.Change is not { } selected ||
            current with { Votes = selected.Votes, Voters = selected.Voters, OpeningVoters = selected.OpeningVoters } != selected ||
            !current.Voters.SequenceEqual(selected.Voters) || !current.OpeningVoters.SequenceEqual(selected.OpeningVoters) ||
            current.Status is not ("queued" or "voting") || !CivicHistory(town).Knows(plan.Actor, current.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected protected proposal or voting window has changed");
        var allowed = plan.Choice.Kind == GovernmentProposalKind.Withdraw ? current.AuthorId == plan.Actor :
            current.Status == "voting" && world.WorldTick < current.DeadlineTick &&
            CivicHistory(town).Knows(plan.Actor, TownGovernmentRules.VoteNoticeToken(current)) &&
            current.Voters.Contains(plan.Actor, StringComparer.Ordinal) && !current.Votes.Any(vote => vote.AgentId == plan.Actor);
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that protected response is final or no longer eligible");
    }

    private static void OfferGovernmentProposal(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, GovernmentProposalKind kind, string subject, string text, int priority)
    {
        if (PlanGovernmentProposal(world, actor, new(town.Id, kind, subject)) is { } plan && CheckGovernmentProposal(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }
}
