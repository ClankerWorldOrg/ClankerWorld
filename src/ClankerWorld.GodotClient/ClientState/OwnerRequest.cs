namespace ClankerWorld.GodotClient.ClientState;

/// <summary>A sent request may commit, but only its original world and panel may use the reply.</summary>
public sealed class OwnerRequestContext(Func<bool> isCurrent, CancellationToken cancellationToken)
{
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public void EnsureCurrent()
    {
        if (!isCurrent()) throw new ObsoleteOwnerRequestException();
        CancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<T> AwaitAsync<T>(Task<T> request)
    {
        try
        {
            var result = await request.WaitAsync(CancellationToken);
            EnsureCurrent();
            return result;
        }
        catch (Exception) when (!isCurrent())
        {
            throw new ObsoleteOwnerRequestException();
        }
    }

    public async Task AwaitAsync(Task request)
    {
        await AwaitAsync(CompleteAsync(request));
        static async Task<bool> CompleteAsync(Task task) { await task; return true; }
    }
}

public class ObsoleteOwnerRequestException : OperationCanceledException { }

/// <summary>One lifecycle for controls, context checks and the deadline, including a lost reply.</summary>
public sealed class OwnerRequest<T>(Func<bool> isCurrent, Action<bool> setBusy)
{
    public async Task<T> RunAsync(Func<OwnerRequestContext, Task<T>> send,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var context = new OwnerRequestContext(isCurrent, deadline.Token);
        context.EnsureCurrent();
        try
        {
            setBusy(true);
            return await context.AwaitAsync(send(context));
        }
        finally { setBusy(false); }
    }
}
