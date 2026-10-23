using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

internal sealed class EconomyIdleProvider : IDecisionProvider
{
    public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
    public long ProviderEpoch => 0;

    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
        CancellationToken cancellationToken = default) =>
        new DeterministicDecisionProvider().DecideAsync(request with
        {
            Observation = request.Observation with
            {
                Candidates = [request.Observation.Candidates.Single(candidate => candidate.Id == "safe_idle")],
            },
        }, cancellationToken);
}
