namespace ClankerWorld.Tests;

// Completing a gate under xUnit's synchronization context would queue its
// continuation even without RunContinuationsAsynchronously. Keep release inline.
public class InlineCompletionSource<T>(TaskCreationOptions options = TaskCreationOptions.None) : TaskCompletionSource<T>(options)
{
    public new bool TrySetResult(T result) => Complete(() => base.TrySetResult(result));
    public new bool TrySetCanceled(CancellationToken token = default) => Complete(() => base.TrySetCanceled(token));
    public new bool TrySetException(Exception exception) => Complete(() => base.TrySetException(exception));
    public new void SetResult(T result) => Complete(() => { base.SetResult(result); return true; });
    public new void SetCanceled(CancellationToken token = default) => Complete(() => { base.SetCanceled(token); return true; });
    public new void SetException(Exception exception) => Complete(() => { base.SetException(exception); return true; });
    private static bool Complete(Func<bool> complete)
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try { return complete(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
    }
}

public class InlineCompletionSource(TaskCreationOptions options = TaskCreationOptions.None) : TaskCompletionSource(options)
{
    public new bool TrySetResult() => Complete(() => base.TrySetResult());
    public new bool TrySetCanceled(CancellationToken token = default) => Complete(() => base.TrySetCanceled(token));
    public new bool TrySetException(Exception exception) => Complete(() => base.TrySetException(exception));
    private static bool Complete(Func<bool> complete)
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try { return complete(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
    }
}
