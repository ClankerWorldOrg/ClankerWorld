using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal sealed class EconomyPlanChoices(string actor, bool choose, string candidateId) : IDecisionProvider
{
    public ActionPlan? Plan { get; private set; }
    public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
    public long ProviderEpoch => 0;
    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = request.Observation.AgentId == actor
            ? request.Observation.Candidates.SingleOrDefault(item => item.Id == candidateId) : null;
        if (candidate is not null) Plan = candidate.ActionPlan;
        var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
        return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
            Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
            request.Observation.ObservationDigest, selected, 1,
            request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
    }
}
