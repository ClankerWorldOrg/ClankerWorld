using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class LearningTests
{
    [Theory]
    [InlineData(false, "cancelled")]
    [InlineData(true, "declined")]
    public async Task APendingWithdrawalStopsTheSameLessonWithoutRewindingItsProgress(bool teacherStops, string expected)
    {
        var (state, learner, teacher) = await RequestedLessonState();
        using var setup = WorldRuntime.Restore(state, actor => new LessonProvider(actor == teacher ? "lesson_accept:" : "safe_idle"));
        for (var tick = 0; tick < 80 && setup.Agents.Single(person => person.AgentId == learner).Lesson?.Progress is not >= 3; tick++)
            Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        Assert.InRange(setup.Agents.Single(person => person.AgentId == learner).Lesson!.Progress, 3, 10);
        var actor = teacherStops ? teacher : learner;
        var provider = new HeldLessonWithdrawalProvider(teacherStops ? LearningChoiceKind.Decline : LearningChoiceKind.Cancel);
        using var world = WorldRuntime.Restore(setup.ExportState(), id => id == actor ? provider : new LessonProvider("safe_idle"));
        world.SubmitInstruction(new("lesson-withdrawal-guidance", "owner:test", actor,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var frame = WorldRuntimeCodec.Encode(world.ExportState());
            var plan = await ModelWorkHarness.WaitForSignalAsync(provider.Started.Task);
            Assert.Equal(frame, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal((learner, teacher, SkillKind.Building), (plan.StudentId, plan.TeacherId, plan.Skill));
            for (var tick = 0; tick < 2; tick++)
                Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var current = world.Agents.Single(person => person.AgentId == learner).Lesson!;
            Assert.InRange(current.Progress, plan.Original!.Progress + 2, 19);
            Assert.True(world.CheckActionPlan(plan).Allowed);

            // A different request and a terminal agreement remain stale even
            // though ordinary progress within this request is allowed.
            foreach (var stale in new[]
            {
                current with { RequestedTick = current.RequestedTick + 1 },
                current with { Stage = "cancelled", LastTransitionTick = world.WorldTick },
            })
            {
                using var changed = WorldRuntime.Restore(world.ExportState() with
                {
                    Agents = world.Agents.Select(person => person.AgentId == learner ? person with { Lesson = stale } : person).ToArray(),
                });
                var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
                Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
                Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
            }

            provider.Release.TrySetResult();
            frame = WorldRuntimeCodec.Encode(world.ExportState());
            await GrownAgentHelperMemoryTests.WaitForRequests(world);
            Assert.Equal(frame, WorldRuntimeCodec.Encode(world.ExportState()));
            var result = await world.AdvanceOneTickNonBlockingAsync();
            var decision = Assert.Single(result.Decisions, item => item.AgentId == actor);
            Assert.Same(plan, decision.ActionPlan);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Null(decision.PlanRefusal);
            var stopped = world.Agents.Single(person => person.AgentId == learner);
            Assert.Equal(current with { Stage = expected, LastTransitionTick = world.WorldTick }, stopped.Lesson);
            Assert.Empty(stopped.Skills ?? []);
            world.Validate();
            frame = WorldRuntimeCodec.Encode(world.ExportState());
            using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(frame));
            Assert.Equal(frame, WorldRuntimeCodec.Encode(reloaded.ExportState()));
        }
        finally { provider.Release.TrySetResult(); }
    }

    private sealed class HeldLessonWithdrawalProvider(LearningChoiceKind choice) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public TaskCompletionSource<LearningActionPlan> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = Assert.Single(request.Observation.Candidates, item => item.ActionPlan is LearningActionPlan plan && plan.Choice.Kind == choice);
            Started.TrySetResult(Assert.IsType<LearningActionPlan>(candidate.ActionPlan));
            await Release.Task.WaitAsync(cancellationToken);
            return new(request.RequestId, request.Observation.AgentId, Kind, request.ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                candidate.Id, 1, new Dictionary<string, double> { [candidate.Id] = 1 });
        }
    }
}
