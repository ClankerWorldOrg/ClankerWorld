using System.Text.Json;
using System.Threading.Channels;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class NonLandJudgeHeldBallotTests
{
    private const string Voter = NonLandRuntimeFixture.Subject;
    private const string Selected = NonLandRuntimeFixture.Judge;
    private const string Withdrawing = NonLandRuntimeFixture.Stranger;

    [Fact]
    public async Task AnotherCandidatesWithdrawalKeepsAnAlreadyHeldNonLandBallotAvailable()
    {
        var state = await NonLandRuntimeFixture.FiledAsync();
        var town = state.Towns![0];
        var tick = state.Society.Society.WorldTick;
        // Use the existing native vacancy election; no case mandate or ballot is inserted.
        var (council, government) = TownGovernmentRules.RegisterMayor(town.Council!, town.Government!,
            Voter, "land+non_land", town.ResidentIds, tick);
        (council, government) = TownGovernmentRules.Advance(council, government, town.Id, town.Name,
            state.WorldSeed, town.ResidentIds, tick, NonLandRuntimeFixture.Day);
        var mayoral = Assert.IsType<TownMayoralContest>(government.Contest);
        foreach (var voter in town.ResidentIds)
            government = TownGovernmentRules.VoteMayor(government, TownGovernmentRules.RoundToken(mayoral), voter, Voter, tick);
        state = NonLandRuntimeFixture.Strict(state with
        {
            Towns = [town with { Council = council, Government = government }],
            Agents = state.Agents.Select(person => person with { Position = town.OriginSite!.Value, LastDecisionContext = null }).ToArray()
        });
        var provider = new HeldNonLandBallotProvider();
        using var world = NonLandRuntimeFixture.Create(state, provider);
        try
        {
            await NonLandRuntimeFixture.UntilAsync(world, () => world.Towns[0].NonLandHearings.Cases[0].Contest is { Stage: "voting" } contest &&
                contest.Candidates.Contains(Selected) && contest.Candidates.Contains(Withdrawing), NonLandRuntimeFixture.Day * 3);
            Assert.NotEmpty(world.Towns[0].Government!.Offices);
            Assert.All(world.Towns[0].Government!.Offices, office => Assert.Equal(Voter, office.HolderId));
            Assert.Equal(Voter, TownGovernmentRules.CurrentNonLandAuthority(world.Towns[0].Government!, world.WorldTick)!.HolderId);
            Assert.Null(world.Towns[0].NonLandHearings.Cases[0].Judge);
            Assert.Contains(world.Towns[0].NonLandHearings.Cases[0].JudgeConsents, consent => consent.AgentId == Selected && consent.WithdrawnTick is null);
            Assert.Contains(world.Towns[0].NonLandHearings.Cases[0].JudgeConsents, consent => consent.AgentId == Withdrawing && consent.WithdrawnTick is null);
            provider.Armed = true;
            NonLandRuntimeFixture.Wake(world, Voter, "hold-personal-case-ballot");
            for (var step = 0; step < 20 && !provider.Started.Task.IsCompleted; step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(CaseBallotDeadline);
                Assert.True(result.Advanced);
                if (result.Events.Any(entry => entry.Kind == "hosted_decision_started" && entry.Detail == Withdrawing))
                    await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Withdrawing);
                if (result.Events.Any(entry => entry.Kind == "hosted_decision_started" && entry.Detail == Voter))
                {
                    var call = await provider.Entered.Reader.ReadAsync().AsTask().WaitAsync(CaseBallotDeadline);
                    call.Proceed.TrySetResult(true);
                    if (!await call.Held.Task.WaitAsync(CaseBallotDeadline)) await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Voter);
                }
            }
            Assert.True(provider.Started.Task.IsCompleted, "The actual case ballot was not held within twenty hosted ticks.");
            Assert.False(world.ModelDecisionsFinished(Voter));
            var originalCase = Assert.Single(world.Towns[0].NonLandHearings.Cases);
            Assert.Equal("pending", originalCase.Status);
            var original = Assert.IsType<TownCaseJudgeContest>(originalCase.Contest);
            var token = TownCaseJudgeRules.RoundToken(original);
            Assert.Contains(Voter, original.Voters);
            Assert.Contains(Selected, original.Candidates);
            Assert.Contains(Withdrawing, original.Candidates);
            Assert.Contains("|law_case_judge_vote|", provider.HeldId!, StringComparison.Ordinal);
            Assert.EndsWith("|" + Selected, provider.HeldId!, StringComparison.Ordinal);

            provider.WithdrawOther = true;
            NonLandRuntimeFixture.Wake(world, Withdrawing, "withdraw-other-case-candidate");
            NonLandJudgeActionPlan? withdrawal = null;
            for (var step = 0; step < 10 && world.Towns[0].NonLandHearings.Cases[0].Contest!.Candidates.Contains(Withdrawing); step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(CaseBallotDeadline);
                Assert.True(result.Advanced);
                if (result.Events.Any(entry => entry.Kind == "hosted_decision_started" && entry.Detail == Withdrawing))
                    await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Withdrawing);
                foreach (var decision in result.Decisions.Where(decision => decision.AgentId == Withdrawing &&
                    decision.ActionPlan is NonLandJudgeActionPlan { Choice.Kind: NonLandJudgeKind.Withdraw }))
                {
                    Assert.True(decision.Admission.Accepted);
                    Assert.False(decision.Admission.FellBack);
                    Assert.Null(decision.PlanRefusal);
                    withdrawal = Assert.IsType<NonLandJudgeActionPlan>(decision.ActionPlan);
                }
            }
            Assert.NotNull(withdrawal);
            var currentCase = Assert.Single(world.Towns[0].NonLandHearings.Cases);
            var current = Assert.IsType<TownCaseJudgeContest>(currentCase.Contest);
            Assert.Equal(token, TownCaseJudgeRules.RoundToken(current));
            Assert.Equal(original.RoundDeadlineTick, current.RoundDeadlineTick);
            Assert.Equal(originalCase.Revisions, currentCase.Revisions);
            Assert.Contains(currentCase.JudgeConsents, consent => consent.AgentId == Withdrawing && consent.WithdrawnTick is not null);
            Assert.DoesNotContain(Withdrawing, current.Candidates);
            Assert.Contains(Voter, current.Voters);
            Assert.Contains(Selected, current.Candidates);
            Assert.True(world.WorldTick < current.RoundDeadlineTick);
            Assert.False(world.ModelDecisionsFinished(Voter));
            var rights = JsonSerializer.Serialize(world.HouseholdLandUseRights);
            var buildings = JsonSerializer.Serialize(world.WorldSimulation.Buildings);

            provider.Release();
            await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Voter);
            NonLandJudgeActionPlan? admitted = null;
            for (var step = 0; step < 10 && admitted is null; step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(CaseBallotDeadline);
                Assert.True(result.Advanced);
                foreach (var decision in result.Decisions.Where(decision => decision.AgentId == Voter &&
                    decision.ActionPlan is NonLandJudgeActionPlan { Choice.Kind: NonLandJudgeKind.Vote }))
                {
                    Assert.True(decision.Admission.Accepted);
                    Assert.False(decision.Admission.FellBack);
                    admitted = Assert.IsType<NonLandJudgeActionPlan>(decision.ActionPlan);
                    Assert.Equal(provider.HeldId, admitted.CandidateId);
                    Assert.Contains(Withdrawing, admitted.Contest!.Candidates);
                    Assert.Equal(Selected, admitted.Candidate);
                    Assert.Null(decision.PlanRefusal);
                }
            }
            Assert.NotNull(admitted);
            var voted = Assert.IsType<TownCaseJudgeContest>(world.Towns[0].NonLandHearings.Cases[0].Contest);
            Assert.Equal(token, TownCaseJudgeRules.RoundToken(voted));
            Assert.Equal(Selected, Assert.Single(voted.Ballots, ballot => ballot.AgentId == Voter).CandidateId);
            Assert.DoesNotContain(Withdrawing, voted.Candidates);
            Assert.Equal(rights, JsonSerializer.Serialize(world.HouseholdLandUseRights));
            Assert.Equal(buildings, JsonSerializer.Serialize(world.WorldSimulation.Buildings));
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.True(world.CheckActionPlan(admitted).Allowed);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
            world.Validate();
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved), _ => new ActionCoverageRecorder(chooseIdle: true));
            restored.Validate();
            Assert.Equal(saved, WorldRuntimeCodec.Encode(restored.ExportState()));
        }
        finally { provider.Release(); }
    }

    private static readonly TimeSpan CaseBallotDeadline = TimeSpan.FromSeconds(30);

    private sealed class CaseBallotCall
    {
        public TaskCompletionSource<bool> Proceed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class HeldNonLandBallotProvider : IDecisionProvider
    {
        private readonly TaskCompletionSource<CognitionDecisionResponse> held = new();
        private CognitionDecisionResponse? response;
        private bool released;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public bool Armed { get; set; }
        public bool WithdrawOther { get; set; }
        public Channel<CaseBallotCall> Entered { get; } = Channel.CreateUnbounded<CaseBallotCall>();
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? HeldId { get; private set; }

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            CaseBallotCall? call = null;
            var actor = request.Observation.AgentId;
            if (Armed && !released && actor == Voter)
            {
                call = new();
                await Entered.Writer.WriteAsync(call, cancellationToken);
                await call.Proceed.Task.WaitAsync(cancellationToken);
            }
            var candidates = request.Observation.Candidates;
            var selected = candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal));
            if (actor == Withdrawing)
            {
                var verb = WithdrawOther ? "|law_case_judge_withdraw|" : "|law_case_judge_register|";
                selected ??= candidates.FirstOrDefault(candidate => candidate.Id.Contains(verb, StringComparison.Ordinal));
            }
            if (actor == Selected)
                selected ??= candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_judge_register|", StringComparison.Ordinal));
            if (Armed && !released && actor == Voter)
                selected ??= candidates.FirstOrDefault(candidate => candidate.Id.Contains("|law_case_judge_vote|", StringComparison.Ordinal) &&
                    candidate.Id.EndsWith("|" + Selected, StringComparison.Ordinal));
            selected ??= candidates.FirstOrDefault(candidate => candidate.Id.Contains("|visit|", StringComparison.Ordinal)) ??
                candidates.Single(candidate => candidate.Id == "safe_idle");
            var value = new CognitionDecisionResponse(request.RequestId, actor, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest, selected.Id, 1,
                candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal));
            if (call is not null && selected.Id.Contains("|law_case_judge_vote|", StringComparison.Ordinal))
            {
                response = value;
                HeldId = selected.Id;
                Started.TrySetResult(true);
                call.Held.TrySetResult(true);
                return await held.Task.WaitAsync(cancellationToken);
            }
            call?.Held.TrySetResult(false);
            return value;
        }

        public void Release()
        {
            released = true;
            if (response is { } value) held.TrySetResult(value);
        }
    }
}
