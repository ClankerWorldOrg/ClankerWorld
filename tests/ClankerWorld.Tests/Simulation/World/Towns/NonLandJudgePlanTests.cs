using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class NonLandJudgePlanTests
{
    [Fact]
    public async Task AConflictedMayorRequiresPersonalCaseConsentAndTheSameResidentBallotAcrossReplay()
    {
        var state = await NonLandRuntimeFixture.FiledAsync();
        var town = state.Towns![0];
        var tick = state.Society.Society.WorldTick;
        var (council, government) = TownGovernmentRules.RegisterMayor(town.Council!, town.Government!,
            NonLandRuntimeFixture.Subject, "land+non_land", town.ResidentIds, tick);
        (council, government) = TownGovernmentRules.Advance(council, government, town.Id, town.Name,
            state.WorldSeed, town.ResidentIds, tick, NonLandRuntimeFixture.Day);
        var contest = Assert.IsType<TownMayoralContest>(government.Contest);
        foreach (var voter in town.ResidentIds)
            government = TownGovernmentRules.VoteMayor(government, TownGovernmentRules.RoundToken(contest), voter,
                NonLandRuntimeFixture.Subject, tick);
        state = NonLandRuntimeFixture.Strict(state with { Towns = [town with { Council = council, Government = government }] });
        var plans = new List<NonLandJudgeActionPlan>();
        using var world = NonLandRuntimeFixture.Create(state, Provider());
        await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].Contest is { Stage: "voting" },
            NonLandRuntimeFixture.Day * 3, Observe);
        var pending = world.Towns[0].NonLandHearings.Cases[0];
        Assert.Null(pending.Judge);
        Assert.All(world.Towns[0].Government!.Offices, office => Assert.Equal(NonLandRuntimeFixture.Subject, office.HolderId));
        Assert.Contains(pending.JudgeConsents, consent => consent.AgentId == NonLandRuntimeFixture.Judge && consent.WithdrawnTick is null);
        Assert.DoesNotContain(NonLandRuntimeFixture.Subject, pending.Contest!.Candidates);
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = NonLandRuntimeFixture.Create(WorldRuntimeCodec.Decode(saved), Provider());
        for (var step = 0; step < NonLandRuntimeFixture.Day * 2 && world.Towns[0].NonLandHearings.Cases[0].Judge is null; step++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            Observe(result);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        }
        var actual = world.Towns[0].NonLandHearings.Cases[0];
        var judge = Assert.IsType<TownCaseJudge>(actual.Judge);
        Assert.Equal((NonLandRuntimeFixture.Judge, "case_elected"), (judge.AgentId, judge.Kind));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Contains(plans, plan => plan.Choice.Kind == NonLandJudgeKind.Register && plan.Actor == judge.AgentId);
        var ballots = plans.Where(plan => plan.Choice.Kind == NonLandJudgeKind.Vote).ToArray();
        Assert.NotEmpty(ballots);
        var election = actual.ContestHistory.Single(item => item.Id == judge.AuthorityId);
        Assert.All(ballots, plan =>
        {
            Assert.Equal(plan.Choice, NonLandJudgeChoice.Decode(plan.CandidateId));
            Assert.Equal(judge.AgentId, plan.Candidate);
            Assert.Equal(election.Id, plan.Contest!.Id);
            Assert.Contains(plan.Actor, plan.Contest.Voters);
            Assert.Contains(election.Rounds.SelectMany(round => round.Ballots), ballot => ballot.AgentId == plan.Actor && ballot.CandidateId == plan.Candidate);
            Assert.Equal(ActionRefusalReason.TargetUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        });
        Assert.All(plans.Where(plan => plan.Choice.Kind == NonLandJudgeKind.Register), plan =>
            Assert.Contains(actual.JudgeConsents, consent => consent.AgentId == plan.Actor));
        var assignedState = NonLandRuntimeFixture.Strict(world.ExportState());
        foreach (var verb in new[] { "law_case_judge_withdraw", "law_case_judge_resign" })
        {
            var leaving = new NonLandTestProvider
            {
                Choose = observation => observation.AgentId == judge.AgentId
                    ? observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|" + verb + "|", StringComparison.Ordinal)) : null
            };
            using var departed = NonLandRuntimeFixture.Create(assignedState, leaving);
            NonLandRuntimeFixture.Wake(departed, judge.AgentId, "personal-case-mandate-" + verb);
            NonLandJudgeActionPlan? departedPlan = null;
            await NonLandRuntimeFixture.UntilAsync(departed, () => departed.Towns[0].NonLandHearings.Cases[0].Judge is null &&
                departed.Towns[0].NonLandHearings.Cases[0].JudgeConsents.Any(consent => consent.AgentId == judge.AgentId && consent.WithdrawnTick is not null), 8, result =>
            {
                departedPlan = result.Decisions.Where(decision => decision.ActionPlan is NonLandJudgeActionPlan && decision.Admission.Accepted && decision.PlanRefusal is null)
                    .Select(decision => Assert.IsType<NonLandJudgeActionPlan>(decision.ActionPlan)).LastOrDefault() ?? departedPlan;
            });
            Assert.NotNull(departedPlan);
            Assert.Equal(verb, NonLandJudgeChoice.Verb(departedPlan.Choice.Kind));
            Assert.Equal(judge.AgentId, departedPlan.Actor);
            Assert.Equal(ActionRefusalReason.TargetUnavailable, departed.CheckActionPlan(departedPlan).Refusal!.Reason);
            NonLandRuntimeFixture.Strict(departed.ExportState());
        }

        void Observe(WorldStepResult result)
        {
            plans.AddRange(result.Decisions.Where(decision => decision.ActionPlan is NonLandJudgeActionPlan plan && decision.Admission.Accepted && decision.PlanRefusal is null &&
                    result.Events.Any(item => item.Detail == town.Id + "|" + plan.CaseId + "|" + plan.Actor &&
                        item.Kind == (plan.Choice.Kind == NonLandJudgeKind.Vote ? "law_case_evidence" : "law_case_judge_consent")))
                .Select(decision => Assert.IsType<NonLandJudgeActionPlan>(decision.ActionPlan)));
        }
    }

    private static NonLandTestProvider Provider() => new()
    {
        Choose = observation => observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal)) ??
            observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_judge_register|", StringComparison.Ordinal) &&
                observation.AgentId is NonLandRuntimeFixture.Judge or NonLandRuntimeFixture.Stranger) ??
            observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_judge_vote|", StringComparison.Ordinal) &&
                candidate.Id.EndsWith("|" + NonLandRuntimeFixture.Judge, StringComparison.Ordinal))
    };
}
