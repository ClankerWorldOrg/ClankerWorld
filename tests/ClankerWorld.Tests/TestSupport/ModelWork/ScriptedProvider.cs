using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

public sealed class ScriptedProvider : IDecisionProvider
{
    private readonly Queue<HeldCall> scripted = new();
    public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
    public long ProviderEpoch => 1;
    public HeldCall Hold()
    {
        var call = new HeldCall();
        scripted.Enqueue(call);
        return call;
    }

    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        var call = scripted.Count > 0 ? scripted.Dequeue() : new HeldCall { IsHeld = false };
        call.Enter(request, ProviderEpoch, cancellationToken);
        if (!call.IsHeld) call.Reply("safe_idle");
        return new(call.Completion);
    }
}

public sealed class HeldCall
{
    private readonly InlineCompletionSource<CognitionDecisionResponse> reply = new();
    private CognitionDecisionRequest? request;
    private long providerEpoch;
    private CancellationTokenRegistration cancellation;
    public bool IsEntered => request is not null;
    internal bool IsHeld { get; set; } = true;
    public CognitionDecisionRequest Request => request ?? throw new InvalidOperationException("Run pending work first.");
    internal Task<CognitionDecisionResponse> Completion => reply.Task;
    internal void Enter(CognitionDecisionRequest entered, long epoch, CancellationToken token)
    {
        request = entered;
        providerEpoch = epoch;
        cancellation = token.Register(() => reply.TrySetCanceled(token));
    }

    public void Reply(string choice)
    {
        var observation = Request.Observation;
        reply.TrySetResult(new(Request.RequestId, observation.AgentId, DecisionProviderKind.LargeLanguageModel,
            providerEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
            choice, 1, observation.Candidates.ToDictionary(item => item.Id, item => item.Id == choice ? 1d : 0d)));
        cancellation.Dispose();
    }

    public void Fail()
    {
        reply.TrySetException(new InvalidOperationException("Scripted provider failure."));
        cancellation.Dispose();
    }
}
