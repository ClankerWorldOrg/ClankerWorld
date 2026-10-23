using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

public sealed class ManualModelWorkScheduler : IModelWorkScheduler
{
    private readonly Queue<Action> pending = new();
    public Func<bool>? CanStart { get; set; }
    public Task<T> Schedule<T>(Func<Task<T>> work, bool startInline = false)
    {
        var result = new TaskCompletionSource<T>();
        pending.Enqueue(() => { _ = RunAsync(); });
        return result.Task;

        async Task RunAsync()
        {
            try { result.SetResult(await work().ConfigureAwait(false)); }
            catch (OperationCanceledException exception) { result.SetCanceled(exception.CancellationToken); }
            catch (Exception exception) { result.SetException(exception); }
        }
    }

    public void RunPendingWork()
    {
        if (CanStart?.Invoke() == false) return;
        // Provider continuations use inline gates; xUnit's context must not decide their order.
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var count = pending.Count;
            for (var index = 0; index < count; index++) pending.Dequeue()();
        }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
    }
}
