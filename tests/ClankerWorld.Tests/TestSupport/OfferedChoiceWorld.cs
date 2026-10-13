using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

/// <summary>Small seeded maps keep an exhaustive choice sweep inexpensive.</summary>
internal static class OfferedChoiceWorld
{
    internal static Task<WorldRuntimeState> CreateAsync(int seed)
    {
        using var world = NormalPathWorld.CreateGenerated("choice-sweep-" + seed, _ => new SweepChoiceProvider(), ticksPerDay: 8);
        var state = world.ExportState();
        return Task.FromResult(SecondGenerationFixture.Add(state).State);
    }
}

internal sealed class SweepChoiceProvider(string? actor = null, string? choice = null) : IDecisionProvider
{
    internal List<AgentObservation> Observations { get; } = [];
    public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
    public long ProviderEpoch => 0;
    public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var observation = request.Observation;
        Observations.Add(observation);
        var selected = observation.AgentId == actor && choice is not null
            ? observation.Candidates.Single(candidate => candidate.Id == choice)
            : observation.Candidates.Single(candidate => candidate.Id == "safe_idle");
        return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId,
            Kind, ProviderEpoch, observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest,
            selected.Id, 1, observation.Candidates.ToDictionary(candidate => candidate.Id,
                candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal),
            ChosenName: observation.NeedsName ? observation.Self!.Name : null,
            ChosenPersonality: observation.NeedsPersonality ? "curious" : null,
            ChosenAspiration: observation.NeedsAspiration ? "explore" : null,
            CivicProposal: selected.Id.Contains("|propose|", StringComparison.Ordinal) ? "Take care of shared places." : null));
    }
}
