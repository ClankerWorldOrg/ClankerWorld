using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Tests;

internal sealed class PrefixChoiceProvider(string prefix) : IDecisionProvider
{
    public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
    public long ProviderEpoch => 0;
    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = request.Observation.Candidates.FirstOrDefault(item => item.Id.StartsWith(prefix, StringComparison.Ordinal))
            ?? request.Observation.Candidates.Single(item => item.Id == "safe_idle");
        return new DeterministicDecisionProvider().DecideAsync(request with
        {
            Observation = request.Observation with { Candidates = [candidate] },
        }, cancellationToken);
    }
}
