using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorldAtomicTickTests
{
    [Fact]
    public async Task CancellationDuringProviderWorkLeavesEverySubsystemUnchangedAndRetryCommitsOnce()
    {
        var provider = new ControlledProvider();
        using var runtime = new WorldRuntime("atomic-tick", _ => provider);
        var before = WorldRuntimeCodec.Encode(runtime.ExportState());
        using var cancellation = new CancellationTokenSource();
        var pending = runtime.AdvanceOneTickAsync(cancellation.Token).AsTask();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var observed = await Task.Run(runtime.ExportState).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(before, WorldRuntimeCodec.Encode(observed));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(before, WorldRuntimeCodec.Encode(runtime.ExportState()));
        provider.Release.TrySetResult(true);
        Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(1, runtime.WorldTick);
        Assert.Single(runtime.ExportState().Events, item => item.Kind == "tick_advanced");
    }

    [Fact]
    public async Task PauseIsResponsiveAndInvalidatesPendingProviderWork()
    {
        var provider = new ControlledProvider();
        using var runtime = new WorldRuntime("atomic-tick", _ => provider);
        var pending = runtime.AdvanceOneTickAsync().AsTask();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        // Start the owner call even when unrelated fixture work fills the thread pool.
        var pause = Task.Factory.StartNew(runtime.Pause, CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        await ModelWorkHarness.WaitForSignalAsync(pause, TimeSpan.FromSeconds(3));
        runtime.Pause();
        Assert.Single(runtime.ExportState().Events, item => item.Kind == "paused");
        var paused = WorldRuntimeCodec.Encode(runtime.ExportState());
        provider.Release.TrySetResult(true);
        Assert.False((await pending).Advanced);
        Assert.Equal(paused, WorldRuntimeCodec.Encode(runtime.ExportState()));
        Assert.Equal(0, runtime.WorldTick);
        var pausedTick = await runtime.AdvanceOneTickAsync();
        Assert.False(pausedTick.Advanced);
        Assert.Equal("paused", pausedTick.Outcome);
        Assert.Equal(paused, WorldRuntimeCodec.Encode(runtime.ExportState()));
        Assert.False(runtime.StageStarterContent());
        Assert.Equal(paused, WorldRuntimeCodec.Encode(runtime.ExportState()));
        runtime.Resume();
        Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
    }

    [Fact]
    public async Task ConcurrentTicksCommitInOrderWithoutDuplicatingOrLosingTicks()
    {
        var provider = new ControlledProvider();
        using var runtime = new WorldRuntime("atomic-tick", _ => provider);
        var first = runtime.AdvanceOneTickAsync().AsTask();
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = runtime.AdvanceOneTickAsync().AsTask();
        provider.Release.TrySetResult(true);
        Assert.Equal(1, (await first).WorldTick);
        Assert.Equal(2, (await second).WorldTick);
    }

    private sealed class ControlledProvider : IDecisionProvider
    {
        public InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
        public InlineCompletionSource<bool> Release { get; } = new(TaskCreationOptions.None);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return await new DeterministicDecisionProvider().DecideAsync(request, cancellationToken);
        }
    }
}
