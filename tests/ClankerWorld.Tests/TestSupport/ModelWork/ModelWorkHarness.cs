using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ModelWorkHarness : IModelWorkObserver
{
    private static readonly ConditionalWeakTable<WorldRuntime, ModelWorkHarness> Attached = new();
    private readonly ConcurrentQueue<ModelWorkTransition> transitions = new();
    public WorldRuntime World { get; }
    public ManualModelWorkScheduler Scheduler { get; } = new();
    public IReadOnlyList<ModelWorkTransition> Transitions => transitions.ToArray();
    public IReadOnlyList<ModelWorkSnapshot> Pending => World.PendingModelWork;

    private ModelWorkHarness(WorldRuntime world, TimeProvider? timeProvider = null)
    {
        World = world;
        World.ConfigureModelWork(Scheduler, this, timeProvider);
    }

    public static ModelWorkHarness For(WorldRuntime world, TimeProvider? timeProvider = null) =>
        Attached.GetValue(world, world => new(world, timeProvider));
    public void OnModelWork(ModelWorkTransition transition) => transitions.Enqueue(transition);
    public void RunPendingWork() => Scheduler.RunPendingWork();

    public async ValueTask<WorldStepResult> TickAsync(Func<bool>? commitPermitted = null,
        bool runPendingWork = true, CancellationToken cancellationToken = default)
    {
        var step = await World.AdvanceOneTickNonBlockingAsync(commitPermitted, cancellationToken);
        if (runPendingWork) await SettleAsync();
        return step;
    }

    /// <summary>Start queued calls and finish wrappers whose providers have returned; held replies stay held.</summary>
    public async Task SettleAsync()
    {
        RunPendingWork();
        await Task.WhenAll(Pending.Where(item => item.Transition.Stage == ModelWorkStage.Finished)
            .Select(item => item.Completion));
    }

    public async Task WaitAsync(Task signal)
    {
        RunPendingWork();
        await signal.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public async Task<WorldStepResult> TickUntilAdmittedAsync(string actor, int maxTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTicks);
        var first = transitions.Count;
        for (var tick = 0; tick < maxTicks; tick++)
        {
            var step = await TickAsync();
            if (Transitions.Skip(first).Any(item => item.ActorId == actor && item.Stage == ModelWorkStage.Admitted))
                return step;
        }
        throw new InvalidOperationException($"No reply for {actor} admitted within {maxTicks} ticks.");
    }

    public async Task<(WorldStepResult First, WorldStepResult Second)> AdvanceTogetherAsync(ModelWorkHarness other)
    {
        await SettleAsync();
        await other.SettleAsync();
        var first = await TickAsync();
        var second = await other.TickAsync();
        Assert.Equal(WorldRuntimeCodec.Encode(World.ExportState()), WorldRuntimeCodec.Encode(other.World.ExportState()));
        return (first, second);
    }
}

public static class ModelWorkTestExtensions
{
    public static ValueTask<WorldStepResult> TickModelWorkAsync(this WorldRuntime world,
        Func<bool>? commitPermitted = null, CancellationToken cancellationToken = default) =>
        ModelWorkHarness.For(world).TickAsync(commitPermitted, cancellationToken: cancellationToken);
}
