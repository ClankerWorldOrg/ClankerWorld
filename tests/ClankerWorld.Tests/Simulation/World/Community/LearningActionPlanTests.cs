using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class LearningTests
{
    [Theory]
    [InlineData(LearningChoiceKind.Accept, "accepted")]
    [InlineData(LearningChoiceKind.Decline, "declined")]
    public async Task ALessonResponseRetainsItsStudentSkillAndRequestAcrossRollbackReplayAndReload(LearningChoiceKind kind, string expected)
    {
        var (state, learner, teacher) = await RequestedLessonState();
        var chooser = new LessonPlanChooser(teacher, kind);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new LessonPlanChooser(teacher, kind));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<LearningActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == teacher).ActionPlan);
        Assert.Equal((learner, teacher, SkillKind.Building), (plan.StudentId, plan.TeacherId, plan.Skill));
        Assert.Equal(plan.Choice, LearningChoice.Decode(plan.CandidateId));
        Assert.Equal(expected, world.Agents.Single(person => person.AgentId == learner).Lesson!.Stage);
        Assert.Empty(world.Agents.Single(person => person.AgentId == learner).Skills ?? []);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task ALessonAcceptanceRefusesAWithdrawnRequestWithoutGrantingTheSkill()
    {
        var (state, learner, teacher) = await RequestedLessonState();
        var chooser = new LessonPlanChooser(teacher, LearningChoiceKind.Accept, choose: false);
        using var capture = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await capture.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<LearningActionPlan>(chooser.Plan);
        using var changed = WorldRuntime.Restore(capture.ExportState() with
        {
            Agents = capture.Agents.Select(person => person.AgentId == learner ? person with
            { Lesson = person.Lesson! with { Stage = "cancelled", LastTransitionTick = capture.WorldTick } } : person).ToArray(),
        });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Empty(changed.Agents.Single(person => person.AgentId == learner).Skills ?? []);
    }

    private static async Task<(WorldRuntimeState State, string Learner, string Teacher)> RequestedLessonState()
    {
        var state = await PreparedState();
        var learner = state.Society.Society.Agents.Single(person => person.CurrentRole == ClankerWorld.Simulation.Society.SocietyWorkRole.Trader).Id;
        using var requested = WorldRuntime.Restore(state, actor => new LessonProvider(actor == learner ? "learn:building:" : "safe_idle"));
        Assert.True((await requested.AdvanceOneTickAsync()).Advanced);
        var teacher = requested.Agents.Single(person => person.AgentId == learner).Lesson!.TeacherId;
        return (requested.ExportState() with
        { Agents = requested.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() }, learner, teacher);
    }

    private sealed class LessonPlanChooser(string actor, LearningChoiceKind kind, bool choose = true) : IDecisionProvider
    {
        public LearningActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is LearningActionPlan plan && plan.Choice.Kind == kind) : null;
            if (candidate is not null) Plan = Assert.IsType<LearningActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
