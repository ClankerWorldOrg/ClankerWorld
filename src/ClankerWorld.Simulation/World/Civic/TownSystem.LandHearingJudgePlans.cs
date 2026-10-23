using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private static string LandHearingJudgeActionToken(TownLandCase item, LandHearingJudgeKind kind) =>
        kind == LandHearingJudgeKind.Vote && item.Contest is { } contest
            ? DerivedId.Create(TownLandHearingRules.RevisionToken(item) + "#" + TownLandCaseJudgeRules.RoundToken(contest)) : TownLandHearingRules.RevisionToken(item);

    internal static LandHearingJudgeActionPlan? PlanLandHearingJudge(ITownWorld world, string actor, LandHearingJudgeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => LandHearingJudgeActionToken(item, choice.Kind) == choice.RevisionOrRoundToken) is not { } item) return null;
        var contest = item.Contest;
        return new(actor, choice, item.Id, TownLandHearingRules.CurrentRevision(item),
            item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == actor && consent.WithdrawnTick is null), item.Judge,
            contest is null ? null : contest with
            {
                Voters = contest.Voters.ToArray(),
                Candidates = contest.Candidates.ToArray(),
                Ballots = contest.Ballots.ToArray(),
                TiedCandidates = contest.TiedCandidates.ToArray(),
                Rounds = contest.Rounds.ToArray()
            }, choice.Kind == LandHearingJudgeKind.Vote ? ResolveCivicAgentToken(world, choice.CandidateToken) : null);
    }

    internal ActionCheck CheckLandHearingJudge(ITownWorld world, LandHearingJudgeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Cases.SingleOrDefault(item => item.Id == plan.CaseId) is not { } item ||
            LandHearingJudgeActionToken(item, plan.Choice.Kind) != plan.Choice.RevisionOrRoundToken ||
            TownLandHearingRules.RecordVersion(TownLandHearingRules.CurrentRevision(item)) != TownLandHearingRules.RecordVersion(plan.Revision) ||
            !TownLandHearingRules.HasNoticeReceipt(TownLandHearingRules.CurrentRevision(item), plan.Actor, world.WorldTick, town.Council.Knowledge))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case, noticed revision or knowledge has changed");
        if (plan.Choice.Kind == LandHearingJudgeKind.Vote)
        {
            if (plan.Contest is not { } original || item.Contest is not { Stage: "voting", RoundDeadlineTick: { } deadline } current || deadline <= world.WorldTick ||
                TownLandHearingRules.RecordVersion(current with
                { Voters = original.Voters, Candidates = original.Candidates, Ballots = original.Ballots, TiedCandidates = original.TiedCandidates }) != TownLandHearingRules.RecordVersion(original) ||
                !original.Voters.Contains(plan.Actor, StringComparer.Ordinal) || !current.Voters.Contains(plan.Actor, StringComparer.Ordinal) ||
                plan.Candidate is not { } candidate || CivicAgentToken(candidate) != plan.Choice.CandidateToken ||
                !original.Candidates.Contains(candidate, StringComparer.Ordinal) || !current.Candidates.Contains(candidate, StringComparer.Ordinal) ||
                !town.Council.Knowledge.Any(receipt => receipt.AgentId == plan.Actor && town.Council.Notices.Any(notice => notice.Id == receipt.NoticeId &&
                    notice.Kind == "land_hearing" && notice.SubjectId == TownLandCaseJudgeRules.RoundToken(current))))
                return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case ballot round or candidate is no longer eligible");
            return new();
        }
        if (!TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            !(item.Status == "pending" || item.ReopenRequests.Any(request => request.Status == "pending")))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected case no longer needs their personal mandate response");
        var consent = item.JudgeConsents.SingleOrDefault(consent => consent.AgentId == plan.Actor && consent.WithdrawnTick is null);
        return plan.Choice.Kind switch
        {
            LandHearingJudgeKind.Register when plan.Consent is null && consent is null && TownLandCaseJudgeRules.Eligible(item, plan.Actor,
                TownAdults(world, town), LandHearingHouseholds(world), LandHearingParties(world, town, plan.Revision.Tiles, item), LandHearingDirectStakes(world, item)) => new(),
            LandHearingJudgeKind.Withdraw when plan.Consent is not null && consent == plan.Consent => new(),
            LandHearingJudgeKind.Resign when plan.Judge is { Kind: "case_elected" } judge && judge.AgentId == plan.Actor && item.Judge == judge => new(),
            _ => ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected consent or assigned case mandate has changed")
        };
    }

    private void OfferLandHearingJudge(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandCase item, LandHearingJudgeKind kind, string target, string text, int priority)
    {
        var id = CivicAction(world, town.Id, LandHearingJudgeChoice.Verb(kind), LandHearingJudgeActionToken(item, kind), target);
        if (LandHearingJudgeChoice.Decode(id) is { } choice && PlanLandHearingJudge(world, actor, choice) is { } plan && CheckLandHearingJudge(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
