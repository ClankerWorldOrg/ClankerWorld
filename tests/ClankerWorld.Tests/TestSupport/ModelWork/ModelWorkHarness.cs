using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Runtime;

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

    public static async Task WaitForExistingAsync(WorldRuntime world, Func<ModelWorkSnapshot, bool>? include = null)
    {
        // Blocking fixtures can still use the production scheduler. Read their
        // public snapshots without changing a scheduler after calls have started.
        if (Attached.TryGetValue(world, out var harness)) harness.RunPendingWork();
        await Task.WhenAll(world.PendingModelWork.Where(item => include?.Invoke(item) ?? true).Select(item => item.Completion))
            .WaitAsync(TimeSpan.FromSeconds(30));
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

    public async Task WaitForAllAsync(Func<ModelWorkSnapshot, bool>? include = null)
    {
        RunPendingWork();
        await Task.WhenAll(Pending.Where(item => include?.Invoke(item) ?? true).Select(item => item.Completion))
            .WaitAsync(TimeSpan.FromSeconds(30));
    }

    public Task WaitForKindAsync(ModelWorkKind kind) => WaitForAllAsync(item => item.Transition.Kind == kind);
    public Task WaitForDecisionAsync(string actor) => WaitForAllAsync(item => item.Transition.Kind == ModelWorkKind.Decision &&
        item.Transition.ActorId == actor);
    public bool DecisionsFinished(params string[] actors) => actors.All(actor => Pending.Any(item =>
        item.Transition.Kind == ModelWorkKind.Decision && item.Transition.ActorId == actor && item.Completion.IsCompleted));

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
    public static async ValueTask<WorldStepResult> TickModelWorkAsync(this WorldRuntime world,
        Func<bool>? commitPermitted,
        Func<WorldRuntime, IReadOnlyList<WorldEventEntry>, IReadOnlyList<PreparedChildModelSelection>> prepareChildModelSelections,
        CancellationToken cancellationToken = default)
    {
        var harness = ModelWorkHarness.For(world);
        var step = await world.AdvanceOneTickNonBlockingAsync(commitPermitted, prepareChildModelSelections, cancellationToken);
        await harness.SettleAsync();
        return step;
    }

    public static async ValueTask<bool> AdvanceWithModelWorkAsync(this WorldRuntimeService service,
        WorldRuntime world, CancellationToken cancellationToken = default)
    {
        var harness = ModelWorkHarness.For(world);
        var result = await service.TryAdvanceOnceAsync(cancellationToken);
        await harness.SettleAsync();
        return result;
    }

    public static ValueTask<WorldStepResult> TickModelWorkAsync(this WorldRuntime world,
        Func<bool>? commitPermitted = null, CancellationToken cancellationToken = default) =>
        ModelWorkHarness.For(world).TickAsync(commitPermitted, cancellationToken: cancellationToken);
}
