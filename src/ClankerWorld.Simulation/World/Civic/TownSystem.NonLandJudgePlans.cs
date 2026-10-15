using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private static string NonLandJudgeActionToken(TownNonLandCase item, NonLandJudgeKind kind) =>
        kind == NonLandJudgeKind.Vote && item.Contest is { } contest
            ? NonLandActionToken(item) + "#" + TownCaseJudgeRules.RoundToken(contest) : NonLandActionToken(item);

    internal static NonLandJudgeActionPlan? PlanNonLandJudge(ITownWorld world, string actor, NonLandJudgeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => NonLandJudgeActionToken(item, choice.Kind) == choice.RevisionOrRoundToken) is not { } item) return null;
        var contest = item.Contest;
        return new(actor, choice, item.Id, NonLandRevision(item),
            item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == actor && consent.WithdrawnTick is null), item.Judge,
            contest is null ? null : contest with
            {
                Voters = contest.Voters.ToArray(),
                Candidates = contest.Candidates.ToArray(),
                Ballots = contest.Ballots.ToArray(),
                TiedCandidates = contest.TiedCandidates.ToArray(),
                Rounds = contest.Rounds.ToArray()
            }, choice.Kind == NonLandJudgeKind.Vote ? ResolveCivicAgentToken(world, choice.CandidateToken) : null);
    }

    internal static ActionCheck CheckNonLandJudge(ITownWorld world, NonLandJudgeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? NonLandAdultCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.NonLandHearings.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            NonLandJudgeActionToken(item, plan.Choice.Kind) != plan.Choice.RevisionOrRoundToken ||
            TownHearingProcedure.Digest(NonLandRevision(item)) != TownHearingProcedure.Digest(plan.Revision) ||
            !TownNonLandRules.HasNoticeReceipt(NonLandRevision(item), plan.Actor, world.WorldTick, town.Council.Knowledge))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case, noticed revision or knowledge has changed");
        if (plan.Choice.Kind == NonLandJudgeKind.Vote)
        {
            if (plan.Contest is not { } original || item.Contest is not { Stage: "voting", RoundDeadlineTick: { } deadline } current || deadline <= world.WorldTick ||
                TownHearingProcedure.Digest(current with { Ballots = original.Ballots }) != TownHearingProcedure.Digest(original) ||
                !current.Voters.Contains(plan.Actor, StringComparer.Ordinal) || plan.Candidate is not { } candidate ||
                CivicAgentToken(candidate) != plan.Choice.CandidateToken || !current.Candidates.Contains(candidate, StringComparer.Ordinal) ||
                !CivicHistory(town).Knows(plan.Actor, TownCaseJudgeRules.RoundToken(current)))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case ballot round or candidate is no longer eligible");
            return new();
        }
        if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            !(item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case no longer needs their personal mandate response");
        var consent = item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == plan.Actor && consent.WithdrawnTick is null);
        return plan.Choice.Kind switch
        {
            NonLandJudgeKind.Register when plan.Consent is null && consent is null && TownCaseJudgeRules.Eligible(item, plan.Actor,
                TownAdults(world, town), LandHearingHouseholds(world), NonLandParties(world, town, item)) => new(),
            NonLandJudgeKind.Withdraw when plan.Consent is not null && consent == plan.Consent => new(),
            NonLandJudgeKind.Resign when plan.Judge is { Kind: "case_elected" } judge && judge.AgentId == plan.Actor && item.Judge == judge => new(),
            _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected consent or assigned case mandate has changed")
        };
    }

    private static void OfferNonLandJudge(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownNonLandCase item, NonLandJudgeKind kind, string target, string text, int priority)
    {
        var id = CivicAction(world, town.Id, NonLandJudgeChoice.Verb(kind), NonLandJudgeActionToken(item, kind), target);
        if (NonLandJudgeChoice.Decode(id) is { } choice && PlanNonLandJudge(world, actor, choice) is { } plan && CheckNonLandJudge(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
