using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ModelWorkHarnessTests
{
    private const string Actor = "founder-scout";

    [Fact]
    public async Task RegisteredWorkStartsOnlyWhenRequestedAndAdmissionRequiresACommittedTick()
    {
        var provider = new ScriptedProvider();
        var held = provider.Hold();
        using var world = new WorldRuntime("model-work-stages", id => id == Actor ? provider : new DeterministicDecisionProvider());
        var harness = ModelWorkHarness.For(world);
        Assert.True((await harness.TickAsync(runPendingWork: false)).Advanced);
        var work = Assert.Single(harness.Pending);
        Assert.Equal(ModelWorkStage.Registered, work.Transition.Stage);
        Assert.False(held.IsEntered);
        Assert.False(work.Completion.IsCompleted);
        var frozen = WorldRuntimeCodec.Encode(world.ExportState());
        harness.RunPendingWork();
        Assert.True(held.IsEntered);
        Assert.Equal(ModelWorkStage.Started, Assert.Single(harness.Pending).Transition.Stage);
        held.Reply("safe_idle");
        await work.Completion;
        Assert.Equal(frozen, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.Equal(ModelWorkStage.Finished, Assert.Single(harness.Pending).Transition.Stage);
        Assert.False((await harness.TickAsync(() => false)).Advanced);
        Assert.Equal(frozen, WorldRuntimeCodec.Encode(world.ExportState()));
        Assert.DoesNotContain(harness.Transitions, item => item.Stage == ModelWorkStage.Admitted);
        var expectedTick = world.WorldTick + 1;
        await harness.TickUntilAdmittedAsync(Actor, 1);
        Assert.Equal(expectedTick, Assert.Single(harness.Transitions, item => item.Stage == ModelWorkStage.Admitted).AdmittedTick);
        Assert.Equal(new[] { ModelWorkStage.Registered, ModelWorkStage.Started, ModelWorkStage.Finished, ModelWorkStage.Admitted },
            harness.Transitions.Select(item => item.Stage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PauseCancelsQueuedOrEnteredWorkWithoutAdmittingIt(bool entered)
    {
        var provider = new ScriptedProvider();
        var held = provider.Hold();
        using var world = new WorldRuntime("model-work-cancel", id => id == Actor ? provider : new DeterministicDecisionProvider());
        var harness = ModelWorkHarness.For(world);
        await harness.TickAsync(runPendingWork: entered);
        var work = Assert.Single(harness.Pending);
        world.Pause();
        harness.RunPendingWork();
        await work.Completion;
        Assert.Empty(harness.Pending);
        Assert.Single(harness.Transitions, item => item.Stage == ModelWorkStage.Cancelled);
        Assert.DoesNotContain(harness.Transitions, item => item.Stage == ModelWorkStage.Admitted);
        Assert.Equal(entered, held.IsEntered);
    }

    [Fact]
    public async Task FailedReplyFinishesTheWrapperAndIsDiscardedAtTheNextTick()
    {
        var provider = new ScriptedProvider();
        var held = provider.Hold();
        using var world = new WorldRuntime("model-work-failure", id => id == Actor ? provider : new DeterministicDecisionProvider());
        var harness = ModelWorkHarness.For(world);
        await harness.TickAsync();
        held.Fail();
        await Assert.Single(harness.Pending).Completion;
        var expectedTick = world.WorldTick + 1;
        await harness.TickAsync();
        Assert.Equal(expectedTick, world.WorldTick);
        Assert.Single(harness.Transitions, item => item.Stage == ModelWorkStage.Discarded);
        Assert.DoesNotContain(harness.Transitions, item => item.Stage == ModelWorkStage.Admitted);
    }

    [Fact]
    public async Task PairedRuntimeRepliesUseTheSameAdmissionTicksAndCheckpointBytes()
    {
        var firstProvider = new ScriptedProvider();
        var secondProvider = new ScriptedProvider();
        using var first = new WorldRuntime("model-work-paired", id => id == Actor ? firstProvider : new DeterministicDecisionProvider());
        using var second = WorldRuntime.Restore(first.ExportState(), id => id == Actor ? secondProvider : new DeterministicDecisionProvider());
        var firstHarness = ModelWorkHarness.For(first);
        var secondHarness = ModelWorkHarness.For(second);
        for (var tick = 0; tick < 4; tick++) await firstHarness.AdvanceTogetherAsync(secondHarness);
        Assert.Equal(firstHarness.Transitions.Where(item => item.Stage == ModelWorkStage.Admitted).Select(item => item.AdmittedTick),
            secondHarness.Transitions.Where(item => item.Stage == ModelWorkStage.Admitted).Select(item => item.AdmittedTick));
        Assert.Contains(firstHarness.Transitions, item => item.Stage == ModelWorkStage.Admitted);
    }

    [Fact]
    public async Task IdentityDeadlineFinishesWithoutAdvancingTheWorldClock()
    {
        var clock = new ManualTimeProvider();
        var scripted = new ScriptedProvider();
        var held = scripted.Hold();
        var provider = new IdentityOnlyProvider(scripted);
        using var initial = new WorldRuntime("model-work-clock");
        var state = initial.ExportState();
        state = state with
        {
            Agents = state.Agents.Select(person => person.AgentId == Actor ? person with
            {
                IdentityChoicePending = false,
                IdentityMoments = [new(AgentIdentityMomentKind.Midlife, state.Society.Society.WorldTick)],
            } : person).ToArray(),
        };
        using var world = WorldRuntime.Restore(state, id => id == Actor ? provider : new DeterministicDecisionProvider());
        var harness = ModelWorkHarness.For(world, clock);
        await harness.TickAsync();
        var work = Assert.Single(harness.Pending, item => item.Transition.Kind == ModelWorkKind.IdentityMoment);
        Assert.True(held.IsEntered);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        clock.Advance(TimeSpan.FromSeconds(14));
        Assert.False(work.Completion.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));
        await work.Completion;
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        await harness.TickAsync();
        Assert.Equal("kept", Assert.Single(world.Agents.Single(person => person.AgentId == Actor).IdentityMoments!).Outcome);
        Assert.Single(harness.Transitions, item => item.Kind == ModelWorkKind.IdentityMoment && item.Stage == ModelWorkStage.Discarded);
    }

    private sealed class IdentityOnlyProvider(ScriptedProvider provider) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => provider.ProviderEpoch;
        public DecisionProviderKind KindFor(AgentObservation observation) => observation.IdentityMoment is null
            ? DecisionProviderKind.Deterministic : DecisionProviderKind.LargeLanguageModel;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default) =>
            request.Observation.IdentityMoment is null ? new DeterministicDecisionProvider().DecideAsync(request, cancellationToken)
                : provider.DecideAsync(request, cancellationToken);
    }
}
