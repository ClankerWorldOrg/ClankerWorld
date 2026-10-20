using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private void ApplyChosenNameOutcome(
        CognitionDecisionRequest request,
        CognitionDecisionResponse response,
        CognitionAdmissionResult admission)
    {
        if (response.Provider != DecisionProviderKind.LargeLanguageModel ||
            !admission.Accepted ||
            admission.FellBack && admission.Outcome != "candidate_not_legal" &&
            !admission.Outcome.StartsWith("low_confidence:", StringComparison.Ordinal))
        {
            return;
        }

        var agentId = request.Observation.AgentId;
        var agent = society.Checkpoint.GetAgent(agentId);
        if (!agent.NeedsName)
        {
            return;
        }

        var chosenName = CognitionDecisionResponse.NormalizeChosenName(response.ChosenName);
        var chosenKey = chosenName is null ? null : AgentNameRules.CanonicalKey(chosenName);
        if (chosenName is null || chosenKey is null)
        {
            CloseNameRequest(agentId);
            AppendEvent(new AgentNameUnavailableEventPayload(agentId));
            return;
        }

        var duplicate = AgentNameRules.IsTaken(society.Checkpoint, agentId, chosenName);
        if (!duplicate)
        {
            if (!AgentNameRules.IsAllowedChildName(society.Checkpoint, agentId, chosenName))
            {
                CloseNameRequest(agentId);
                AppendEvent(new AgentNameUnavailableEventPayload(agentId));
                return;
            }
            society.Apply(checkpoint => SocietyRules.RenameAgent(checkpoint, agentId, chosenName));
            AppendEvent(new AgentNamedEventPayload(agentId));
            return;
        }

        AppendEvent(new AgentNameRejectedEventPayload(agentId));
        if (request.Observation.IsNameRetry)
        {
            CloseNameRequest(agentId);
            AppendEvent(new AgentNameRetryExhaustedEventPayload(agentId));
            return;
        }

        var queued = society.EnqueueCognition(new SocietyCognitionScheduleEntry(
            $"name-retry:{request.RequestId}",
            agentId,
            PriorityFor(agents[agentId]),
            WorldTick,
            [SocietyCognitionScheduler.NameRetryTriggerId],
            request.Observation with { IsNameRetry = false }));
        if (queued)
        {
            AppendEvent(new AgentNameRetryRequestedEventPayload(agentId));
            return;
        }

        // The original queue entry has just been consumed, so this is only a
        // defensive bound for an unexpectedly full or unavailable queue.
        CloseNameRequest(agentId);
        AppendEvent(new AgentNameRetryUnavailableEventPayload(agentId));
    }

    private void CloseNameRequest(string agentId) =>
        society.Apply(checkpoint => SocietyRules.CloseNaming(checkpoint, agentId));

    private void CloseUnresolvedNameRetry(CognitionDecisionRequest request)
    {
        if (!request.Observation.IsNameRetry)
        {
            return;
        }

        var agentId = request.Observation.AgentId;
        var agent = society.Checkpoint.GetAgent(agentId);
        if (!agent.NeedsName)
        {
            return;
        }

        CloseNameRequest(agentId);
        AppendEvent(new AgentNameRetryUnusableEventPayload(agentId));
    }

}
