using System.Threading.Channels;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

internal sealed class CaseBallotGate(string actor, string voteVerb)
{
    private readonly InlineCompletionSource<CognitionDecisionResponse> held = new();
    private CognitionDecisionResponse? response;
    internal bool Armed { get; set; }
    internal bool Released { get; private set; }
    internal Channel<Call> Entered { get; } = Channel.CreateUnbounded<Call>();
    internal InlineCompletionSource<bool> Started { get; } = new(TaskCreationOptions.None);
    internal string? HeldId { get; private set; }

    internal async ValueTask<Call?> EnterAsync(CognitionDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!Armed || Released || request.Observation.AgentId != actor) return null;
        var call = new Call();
        await Entered.Writer.WriteAsync(call, cancellationToken);
        await call.Proceed.Task.WaitAsync(cancellationToken);
        return call;
    }

    internal async ValueTask<CognitionDecisionResponse> CompleteAsync(Call? call, CognitionDecisionResponse value,
        CancellationToken cancellationToken, Action? onHeld = null)
    {
        if (call is not null && value.SelectedCandidateId.Contains(voteVerb, StringComparison.Ordinal))
        {
            response = value;
            HeldId = value.SelectedCandidateId;
            onHeld?.Invoke();
            Started.TrySetResult(true);
            call.Held.TrySetResult(true);
            return await held.Task.WaitAsync(cancellationToken);
        }
        call?.Held.TrySetResult(false);
        return value;
    }

    internal void Release()
    {
        Released = true;
        if (response is { } value) held.TrySetResult(value);
    }

    internal sealed class Call
    {
        internal InlineCompletionSource<bool> Proceed { get; } = new(TaskCreationOptions.None);
        internal InlineCompletionSource<bool> Held { get; } = new(TaskCreationOptions.None);
    }
}
