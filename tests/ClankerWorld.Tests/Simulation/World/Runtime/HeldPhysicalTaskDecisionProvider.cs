using System.Collections.Concurrent;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

internal sealed class HeldPhysicalTaskDecisionProvider : IDecisionProvider
{
    public Func<bool>? CanStart { get; set; }
    public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
    public long ProviderEpoch => 1;
    public ConcurrentQueue<AgentObservation> Requests { get; } = new();
    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        while (CanStart?.Invoke() == false) await Task.Delay(1, cancellationToken);
        request.Validate();
        Requests.Enqueue(request.Observation);
        Started.TrySetResult(true);
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("The held physical-task reply must be cancelled, not completed.");
    }
}
