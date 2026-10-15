using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private CognitionParentIdentity[]? InitialChildFamilyBackground(string id, AgentState physical)
    {
        if (!physical.IdentityChoicePending || !society.Checkpoint.Births.Any(birth => birth.ChildId == id)) return null;
        return society.Checkpoint.Relationships.Where(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == id)
            .Select(edge => edge.ProposerId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(parentId =>
            {
                var identity = agents.GetValueOrDefault(parentId) ?? deceasedAgents.GetValueOrDefault(parentId)?.LastPhysical;
                return new CognitionParentIdentity(parentId, society.Checkpoint.GetAgent(parentId).Name,
                    identity is { IdentityChoicePending: false } ? identity.Personality : null,
                    identity is { IdentityChoicePending: false } ? identity.Aspiration : null);
            }).ToArray();
    }

    private void ApplyPersonalIdentityChoice(string id, CognitionDecisionRequest request,
        CognitionDecisionResponse? response, CognitionAdmissionResult admission)
    {
        if (!admission.Accepted || admission.FellBack ||
            response is not { Provider: DecisionProviderKind.LargeLanguageModel } ||
            !request.Observation.NeedsPersonality && !request.Observation.NeedsAspiration ||
            !agents.TryGetValue(id, out var physical) || !physical.IdentityChoicePending)
            return;

        var personality = CognitionDecisionResponse.NormalizeIdentityText(response.ChosenPersonality);
        var aspiration = CognitionDecisionResponse.NormalizeIdentityText(response.ChosenAspiration);
        // A world-born child's first identity needs both choices. An unusable
        // reply leaves the ordinary next decision to ask again, without a
        // separate identity request or inheriting a parent's traits.
        if (society.Checkpoint.Births.Any(birth => birth.ChildId == id) && (personality is null || aspiration is null)) return;
        agentStore.ChooseIdentity(id, physical, personality ?? physical.Personality, aspiration ?? physical.Aspiration, false);
        checkpointSchemaVersion = StateSchemaVersion;
        AppendEvent(personality is not null || aspiration is not null ? "agent_identity_chosen" : "agent_identity_kept", id);
    }
}
