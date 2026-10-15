using System.Text.Json;
using System.Threading.Channels;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class TownLandHearingRuntimeTests
{
    [Fact]
    public async Task AnotherCandidatesWithdrawalKeepsAnAlreadyHeldCaseBallotAvailable()
    {
        var choices = new HearingProvider();
        using var elected = NewWorld(choices);
        await UntilAsync(elected, () => elected.Towns[0].Government!.Offices.Any(office => office.HolderId == Judge), Day * 3, choices);
        var household = elected.Society.GetAgent(Judge).HouseholdId!;
        choices.Plot = elected.HouseholdLandUseRights.First(right => right.HouseholdId == household).Tiles.ToArray();
        choices.CaseFiler = Judge;
        choices.ActingJudge = Filer;
        choices.HearingsEnabled = true;
        var provider = new HeldCaseBallotProvider(choices);
        using var world = WorldRuntime.Restore(elected.ExportState(), _ => provider);
        try
        {
            Prompt(world, Judge, "Consider filing the disagreement on your household's exact plot.");
            await UntilAsync(world, () => world.Towns[0].LandRecords.Cases.SingleOrDefault()?.Contest is { Stage: "voting" } contest &&
                contest.Candidates.Contains(Filer) && contest.Candidates.Contains(Waiver), Day * 3, choices);
            provider.Armed = true;
            Prompt(world, Judge, "Read the actual case-election notice and consider your ballot.");
            for (var step = 0; step < 20 && !provider.Started.Task.IsCompleted; step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(CaseBallotDeadline);
                Assert.True(result.Advanced);
                if (result.Events.Any(entry => entry.Kind == "hosted_decision_started" && entry.Detail == Waiver))
                    await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Waiver);
                if (result.Events.Any(entry => entry.Kind == "hosted_decision_started" && entry.Detail == Judge))
                {
                    var call = await provider.Entered.Reader.ReadAsync().AsTask().WaitAsync(CaseBallotDeadline);
                    call.Proceed.TrySetResult(true);
                    if (!await call.Held.Task.WaitAsync(CaseBallotDeadline)) await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Judge);
                }
            }
            Assert.True(provider.Started.Task.IsCompleted, "The actual case ballot was not held within twenty hosted ticks.");
            Assert.False(world.ModelDecisionsFinished(Judge));
            var originalCase = Assert.Single(world.Towns[0].LandRecords.Cases);
            var original = Assert.IsType<TownLandCaseJudgeContest>(originalCase.Contest);
            var token = TownLandCaseJudgeRules.RoundToken(original);
            Assert.Contains(Judge, original.Voters);
            Assert.Contains(Filer, original.Candidates);
            Assert.Contains(Waiver, original.Candidates);
            Assert.Contains("|hearing_judge_vote|", provider.HeldId!, StringComparison.Ordinal);
            Assert.EndsWith("|" + Filer, provider.HeldId!, StringComparison.Ordinal);

            provider.WithdrawOther = true;
            Prompt(world, Waiver, "Consider withdrawing your own willingness for this case mandate.");
            LandHearingJudgeActionPlan? withdrawal = null;
            for (var step = 0; step < 10 && world.Towns[0].LandRecords.Cases[0].Contest!.Candidates.Contains(Waiver); step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(CaseBallotDeadline);
                Assert.True(result.Advanced);
                if (result.Events.Any(entry => entry.Kind == "hosted_decision_started" && entry.Detail == Waiver))
                    await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Waiver);
                foreach (var decision in result.Decisions.Where(decision => decision.AgentId == Waiver &&
                    decision.ActionPlan is LandHearingJudgeActionPlan { Choice.Kind: LandHearingJudgeKind.Withdraw }))
                {
                    Assert.True(decision.Admission.Accepted);
                    Assert.False(decision.Admission.FellBack);
                    Assert.Null(decision.PlanRefusal);
                    withdrawal = Assert.IsType<LandHearingJudgeActionPlan>(decision.ActionPlan);
                }
            }
            Assert.NotNull(withdrawal);
            var currentCase = Assert.Single(world.Towns[0].LandRecords.Cases);
            var current = Assert.IsType<TownLandCaseJudgeContest>(currentCase.Contest);
            Assert.Equal(token, TownLandCaseJudgeRules.RoundToken(current));
            Assert.Equal(original.RoundDeadlineTick, current.RoundDeadlineTick);
            Assert.Equal(originalCase.Revisions, currentCase.Revisions);
            Assert.Contains(currentCase.JudgeConsents, consent => consent.AgentId == Waiver && consent.WithdrawnTick is not null);
            Assert.DoesNotContain(Waiver, current.Candidates);
            Assert.Contains(Judge, current.Voters);
            Assert.Contains(Filer, current.Candidates);
            Assert.True(world.WorldTick < current.RoundDeadlineTick);
            Assert.False(world.ModelDecisionsFinished(Judge));
            var rights = JsonSerializer.Serialize(world.HouseholdLandUseRights);
            var buildings = JsonSerializer.Serialize(world.WorldSimulation.Buildings);

            provider.Release();
            await TownLandHearingHeldReplyTests.WaitForModelCompletionAsync(world, Judge);
            LandHearingJudgeActionPlan? admitted = null;
            for (var step = 0; step < 10 && admitted is null; step++)
            {
                var result = await world.AdvanceOneTickNonBlockingAsync().AsTask().WaitAsync(CaseBallotDeadline);
                Assert.True(result.Advanced);
                foreach (var decision in result.Decisions.Where(decision => decision.AgentId == Judge &&
                    decision.ActionPlan is LandHearingJudgeActionPlan { Choice.Kind: LandHearingJudgeKind.Vote }))
                {
                    Assert.True(decision.Admission.Accepted);
                    Assert.False(decision.Admission.FellBack);
                    admitted = Assert.IsType<LandHearingJudgeActionPlan>(decision.ActionPlan);
                    Assert.Equal(provider.HeldId, admitted.CandidateId);
                    Assert.Contains(Waiver, admitted.Contest!.Candidates);
                    Assert.Equal(Filer, admitted.Candidate);
                    Assert.Null(decision.PlanRefusal);
                }
            }
            Assert.NotNull(admitted);
            var voted = Assert.IsType<TownLandCaseJudgeContest>(world.Towns[0].LandRecords.Cases[0].Contest);
            Assert.Equal(token, TownLandCaseJudgeRules.RoundToken(voted));
            Assert.Equal(Filer, Assert.Single(voted.Ballots, ballot => ballot.AgentId == Judge).CandidateId);
            Assert.DoesNotContain(Waiver, voted.Candidates);
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

    private sealed class HeldCaseBallotProvider(HearingProvider choices) : IDecisionProvider
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
            if (Armed && !released && request.Observation.AgentId == Judge)
            {
                call = new();
                await Entered.Writer.WriteAsync(call, cancellationToken);
                await call.Proceed.Task.WaitAsync(cancellationToken);
            }
            var value = await choices.DecideAsync(request, cancellationToken);
            var candidates = request.Observation.Candidates;
            var selected = candidates.Single(candidate => candidate.Id == value.SelectedCandidateId);
            if (request.Observation.AgentId == Waiver)
            {
                var verb = WithdrawOther ? "|hearing_judge_withdraw|" : "|hearing_judge_register|";
                if (!selected.Id.Contains("|read|", StringComparison.Ordinal) && candidates.FirstOrDefault(candidate => candidate.Id.Contains(verb, StringComparison.Ordinal)) is { } personal)
                    selected = personal;
            }
            if (!Armed && selected.Id.Contains("|hearing_judge_vote|", StringComparison.Ordinal))
                selected = candidates.Single(candidate => candidate.Id == "safe_idle");
            if (selected.Id != value.SelectedCandidateId)
                value = value with
                {
                    SelectedCandidateId = selected.Id,
                    Probabilities = candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
                    CivicLandTiles = null,
                    CivicLandHearing = null
                };
            if (call is not null && selected.Id.Contains("|hearing_judge_vote|", StringComparison.Ordinal))
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
