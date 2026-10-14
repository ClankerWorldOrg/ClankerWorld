using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WaitingActionPlanTests
{
    [Fact]
    public async Task NativeWaitingUsesItsOfferedOutcomeAndReloads()
    {
        using var world = new WorldRuntime("waiting-action-plans", _ => new WaitingProvider());
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plans = step.Decisions.Where(decision => decision.Admission.Accepted && decision.PlanRefusal is null)
            .Select(decision => decision.ActionPlan).OfType<WaitingActionPlan>().ToArray();
        Assert.NotEmpty(plans);
        foreach (var plan in plans)
        {
            Assert.True(world.CheckActionPlan(plan).Allowed);
            Assert.Contains(step.Events, item => item.Kind == (plan.LegacyAgePermitted ? "agent_idle" : "age_action_rejected") &&
                item.Detail == (plan.LegacyAgePermitted ? plan.Actor : $"{plan.Actor}:{plan.CandidateId}"));
        }
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new WaitingProvider());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
    }

    private sealed class WaitingProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var observation = request.Observation;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, observation.AgentId, Kind, ProviderEpoch,
                observation.RunEpoch, observation.DecisionGeneration, observation.ObservationDigest, WaitingChoice.Candidate, 1,
                observation.Candidates.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == WaitingChoice.Candidate ? 1d : 0d)));
        }
    }
}
