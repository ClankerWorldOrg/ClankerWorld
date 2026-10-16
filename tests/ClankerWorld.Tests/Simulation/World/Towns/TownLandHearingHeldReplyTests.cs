using System.Text.Json;
using System.Threading.Channels;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownLandHearingHeldReplyTests
{
    private const string Actor = NormalPathWorld.Founder3;
    private const string Newcomer = "agent:00000000000000000000000000000093";
    private const int Day = 40;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("hearing_answer")]
    [InlineData("hearing_waive")]
    public async Task AReplyHeldByThePlayableHostCannotAnswerAnOldWindowAfterAnAdultJoins(string action)
    {
        var provider = new HeldReplyProvider(action);
        using var world = NewWorld(provider);
        try
        {
            var initial = world.ExportState();
            var household = world.Society.GetAgent(Actor).HouseholdId!;
            var originalRights = JsonSerializer.Serialize(initial.HouseholdLandUseRights);
            var originalBuildings = JsonSerializer.Serialize(initial.WorldSimulation!.Buildings);
            await HoldPersonalReplyAsync(world, provider);
            Assert.True(provider.Started.Task.IsCompleted, "The actual posted notice and response were not offered within twenty hosted ticks.");
            await ModelWorkHarness.WaitForSignalAsync(provider.Started.Task);
            Assert.NotNull(provider.ReadCandidateId);
            Assert.Contains("|read|", provider.ReadCandidateId, StringComparison.Ordinal);
            var item = Assert.Single(world.Towns[0].LandRecords.Cases);
            var oldRevision = Assert.Single(item.Revisions);
            Assert.Equal("expiry", item.Kind);
            Assert.Equal(1, oldRevision.PublishedTick);
            Assert.Equal(41, oldRevision.DeadlineTick);
            Assert.Contains("|" + action + "|" + item.Id + ":1|", provider.HeldCandidateId!, StringComparison.Ordinal);
            Assert.Contains(world.Towns[0].Council!.Knowledge,
                receipt => receipt.AgentId == Actor && receipt.NoticeId == oldRevision.NoticeId);
            Assert.Empty(item.Responses);
            var oldParty = Assert.Single(oldRevision.Parties, party => party.HouseholdId == household);
            Assert.DoesNotContain(Newcomer, oldParty.AdultIds);

            // Actual House placement changes the affected adult roster while the exact old reply remains outstanding.
            var house = world.WorldSimulation.Buildings.First(building => building.HouseholdId == household &&
                building.InstanceId.Contains("house-", StringComparison.Ordinal));
            Assert.Equal(household, world.AddAgent(Newcomer, house.Position));
            Assert.Equal(household, world.Society.GetAgent(Newcomer).HouseholdId);
            await ModelWorkHarness.WaitForSignalAsync(Task.Run(provider.Release));
            await WaitForModelCompletionAsync(world);

            WorldStepResult? completed = null;
            for (var attempt = 0; attempt < 10 && completed is null; attempt++)
            {
                var step = await world.TickModelWorkAsync().AsTask().WaitAsync(Deadline);
                Assert.True(step.Advanced);
                if (step.Decisions.Any(decision => decision.AgentId == Actor) ||
                    step.Events.Any(item => item.Kind == "hosted_decision_discarded" && item.Detail == Actor)) completed = step;
            }
            Assert.NotNull(completed);
            Assert.True(completed.Decisions.Any(decision => decision.AgentId == Actor &&
                (decision.Admission.FellBack || decision.PlanRefusal is { Reason: ActionRefusalReason.TargetUnavailable })) ||
                completed.Events.Any(item => item.Kind == "hosted_decision_discarded" && item.Detail == Actor ||
                    item.Kind == "town_civic_action_rejected" && item.Detail == world.Towns[0].Id + "|" + Actor + "|" + action),
                "The completed old response must be refused by admission or by the hearing's current-action preflight.");

            item = Assert.Single(world.Towns[0].LandRecords.Cases);
            Assert.Equal("pending", item.Status);
            Assert.Equal(2, item.Revisions.Count);
            Assert.Equal(oldRevision, item.Revisions[0]);
            var current = item.Revisions[^1];
            Assert.Equal(2, current.Number);
            Assert.Equal(current.PublishedTick + Day, current.DeadlineTick);
            Assert.True(current.DeadlineTick > oldRevision.DeadlineTick);
            Assert.True(world.WorldTick < current.DeadlineTick);
            Assert.All(oldParty.AdultIds, adult => Assert.Contains(adult, Assert.Single(current.Parties, party => party.HouseholdId == household).AdultIds));
            Assert.Contains(Newcomer, Assert.Single(current.Parties, party => party.HouseholdId == household).AdultIds);
            Assert.NotEqual(oldRevision.NoticeId, current.NoticeId);
            Assert.Contains(world.Towns[0].Council!.Notices,
                notice => notice.Id == current.NoticeId && notice.SubjectId == item.Id + ":2" && notice.PostedTick == current.PublishedTick);
            Assert.DoesNotContain(world.Towns[0].Council!.Knowledge,
                receipt => receipt.AgentId == Actor && receipt.NoticeId == current.NoticeId);
            Assert.Empty(item.Responses);
            Assert.Empty(item.Rulings);
            Assert.Empty(world.Towns[0].LandRecords.Adjustments);
            Assert.Equal(originalRights, JsonSerializer.Serialize(world.HouseholdLandUseRights));
            Assert.Equal(originalBuildings, JsonSerializer.Serialize(world.WorldSimulation.Buildings));
            Assert.Equal(initial.TownLandTitles, world.TownLandTitles);

            world.Pause();
            world.Validate();
            var encoded = WorldRuntimeCodec.Encode(world.ExportState());
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded), _ => new ActionCoverageRecorder(chooseIdle: true));
            restored.Validate();
            Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
            var savedCase = Assert.Single(restored.Towns[0].LandRecords.Cases);
            Assert.Equal(current.DeadlineTick, savedCase.Revisions[^1].DeadlineTick);
            Assert.Empty(savedCase.Responses);
            Assert.Equal(originalRights, JsonSerializer.Serialize(restored.HouseholdLandUseRights));
        }
        finally { provider.Release(); }
    }

    [Theory]
    [InlineData("hearing_answer")]
    [InlineData("hearing_waive")]
    public async Task ARemainingAdultsHeldResponseSurvivesAnotherHouseholdAdultsDeparture(string action)
    {
        var provider = new HeldReplyProvider(action);
        using var world = NewWorld(provider);
        try
        {
            await HoldPersonalReplyAsync(world, provider);
            Assert.True(provider.Started.Task.IsCompleted);
            var original = Assert.Single(world.Towns[0].LandRecords.Cases);
            var revision = Assert.Single(original.Revisions);
            var household = world.Society.GetAgent(Actor).HouseholdId!;
            var party = Assert.Single(revision.Parties, item => item.HouseholdId == household);
            var departing = party.AdultIds.First(id => id != Actor);
            Assert.Contains(world.Towns[0].Council!.Knowledge,
                receipt => receipt.AgentId == Actor && receipt.NoticeId == revision.NoticeId);

            // Keep the real host request outstanding; another adult's native exit does not
            // replace this notice or invalidate the remaining adult's personal response.
            Assert.True(world.DisplaceAdult(departing));
            Assert.Null(world.Society.GetAgent(departing).HouseholdId);
            Assert.Equal(household, world.Society.GetAgent(Actor).HouseholdId);
            await ModelWorkHarness.WaitForSignalAsync(Task.Run(provider.Release));
            await WaitForModelCompletionAsync(world);
            WorldStepResult? completed = null;
            for (var attempt = 0; attempt < 10 && completed is null; attempt++)
            {
                var step = await ModelWorkHarness.For(world).WaitAsync(world.TickModelWorkAsync().AsTask());
                Assert.True(step.Advanced);
                if (step.Decisions.Any(decision => decision.AgentId == Actor)) completed = step;
            }
            Assert.NotNull(completed);
            var decision = Assert.Single(completed.Decisions, decision => decision.AgentId == Actor);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Null(decision.PlanRefusal);
            var plan = Assert.IsType<LandHearingEvidenceActionPlan>(decision.ActionPlan);
            Assert.Equal(provider.HeldCandidateId, plan.CandidateId);
            Assert.Contains(departing, plan.Party!.AdultIds);
            var current = Assert.Single(world.Towns[0].LandRecords.Cases);
            Assert.Equal(revision, Assert.Single(current.Revisions));
            var response = Assert.Single(current.Responses);
            Assert.Equal((Actor, party.Id, revision.Number, action == "hearing_answer" ? "answer" : "waive"),
                (response.AgentId, response.PartyId, response.Revision, response.Kind));
            Assert.Equal(action == "hearing_answer" ? "I personally answer this noticed hearing." : "I choose not to respond. This speaks only for me.", response.Text);
            Assert.DoesNotContain(current.Responses, response => response.AgentId == departing);
            Assert.Empty(current.Rulings);
            Assert.Empty(world.Towns[0].LandRecords.Adjustments);

            world.Pause();
            world.Validate();
            var encoded = WorldRuntimeCodec.Encode(world.ExportState());
            using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded), _ => new ActionCoverageRecorder(chooseIdle: true));
            restored.Validate();
            Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
            Assert.Equal(response, Assert.Single(Assert.Single(restored.Towns[0].LandRecords.Cases).Responses));
        }
        finally { provider.Release(); }
    }

    private static async Task HoldPersonalReplyAsync(WorldRuntime world, HeldReplyProvider provider)
    {
        var refreshed = false;
        for (var attempt = 0; attempt < 20 && !provider.Started.Task.IsCompleted; attempt++)
        {
            var step = await world.TickModelWorkAsync().AsTask().WaitAsync(Deadline);
            Assert.True(step.Advanced);
            if (!refreshed && world.Towns[0].LandRecords.Cases.Count > 0)
            {
                world.SubmitInstruction(new("consider-hearing", "owner:test", Actor, OwnerInstructionKind.Suggestion,
                    "Read the posted land hearing and consider your own response."));
                refreshed = true;
            }
            if (step.Events.Any(item => item.Kind == "hosted_decision_started" && item.Detail == Actor))
            {
                var call = await ModelWorkHarness.For(world).WaitAsync(provider.Entered.Reader.ReadAsync().AsTask());
                // The provider cannot even select a reply until this gate opens. Waiting for
                // real entry keeps background scheduling from consuming the hearing window.
                Assert.False(ModelWorkHarness.For(world).DecisionsFinished(Actor));
                call.Proceed.TrySetResult(true);
                if (!await ModelWorkHarness.For(world).WaitAsync(call.ReplyHeld.Task)) await WaitForModelCompletionAsync(world);
            }
        }
    }

    private static Task WaitForModelCompletionAsync(WorldRuntime world) =>
        ModelWorkHarness.For(world).WaitForDecisionAsync(Actor);

    private static WorldRuntime NewWorld(HeldReplyProvider provider)
    {
        using var generated = NormalPathWorld.CreateGenerated("government-personal-path", _ => new ActionCoverageRecorder(chooseIdle: true), ticksPerDay: Day);
        var state = generated.ExportState();
        var society = state.Society.Society;
        var oldDay = society.Config.TicksPerWorldDay;
        var household = society.GetAgent(Actor).HouseholdId;
        // A household can hold several connected starter allocations; only this real right is near expiry.
        var rights = Assert.IsAssignableFrom<IReadOnlyList<HouseholdLandUseRight>>(state.HouseholdLandUseRights);
        var ending = rights.First(right => right.HouseholdId == household);
        return WorldRuntime.Restore(state with
        {
            HouseholdLandUseRights = rights.Select(right => right.Id == ending.Id ? right with { AgreedEndTick = 1 } : right).ToArray(),
            Agents = state.Agents.Select(person => person with
            { Position = state.Towns![0].OriginSite!.Value, FullnessBasisPoints = 8_000 }).ToArray(),
            WorldSystems = RegionalWeatherRules.Initialize(state.WorldSystems! with
            { Config = state.WorldSystems.Config with { TicksPerDay = Day, CalendarOffsetTicks = 0 }, RegionalWeather = null }, state.Map),
            Society = state.Society with
            {
                Society = society with
                {
                    Config = society.Config with { TicksPerWorldDay = Day },
                    Agents = society.Agents.Select(person => person with
                    { BirthTick = person.BirthTick / oldDay * Day, BirthLifeTick = person.BirthLifeTick is { } birth ? birth / oldDay * Day : null }).ToArray()
                }
            }
        }, id => id == Actor ? provider : new ActionCoverageRecorder(chooseIdle: true));
    }

    private sealed class ProviderCall
    {
        public InlineCompletionSource<bool> Proceed { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> ReplyHeld { get; } = new(TaskCreationOptions.None);
    }

    private sealed class HeldReplyProvider(string action) : IDecisionProvider
    {
        // Inline completion lets Release finish after the host's awaiting task receives the old exact response.
        private readonly InlineCompletionSource<CognitionDecisionResponse> held = new();
        private CognitionDecisionResponse? response;
        private bool released;
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public Channel<ProviderCall> Entered { get; } = Channel.CreateUnbounded<ProviderCall>();
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public string? ReadCandidateId { get; private set; }
        public string? HeldCandidateId { get; private set; }

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var call = new ProviderCall();
            await Entered.Writer.WriteAsync(call, cancellationToken);
            await call.Proceed.Task.WaitAsync(cancellationToken);
            var observation = request.Observation;
            var choice = released ? null : observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|read|", StringComparison.Ordinal)) ??
                observation.Candidates.FirstOrDefault(candidate => candidate.Id.Contains("|" + action + "|", StringComparison.Ordinal));
            choice ??= observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
            var value = new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, choice.Id, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == choice.Id ? 1d : 0d, StringComparer.Ordinal),
                CivicLandHearing: choice.Id.Contains("|hearing_answer|", StringComparison.Ordinal) ? new(Statement: "I personally answer this noticed hearing.") : null);
            if (choice.Id.Contains("|read|", StringComparison.Ordinal)) ReadCandidateId = choice.Id;
            if (!released && choice.Id.Contains("|" + action + "|", StringComparison.Ordinal))
            {
                response = value;
                HeldCandidateId = choice.Id;
                Started.TrySetResult(true);
                call.ReplyHeld.TrySetResult(true);
                return await held.Task.WaitAsync(cancellationToken);
            }
            call.ReplyHeld.TrySetResult(false);
            return value;
        }

        public void Release()
        {
            released = true;
            if (response is { } value) held.TrySetResult(value);
        }
    }
}
