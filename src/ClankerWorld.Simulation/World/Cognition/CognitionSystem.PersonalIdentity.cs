using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static CognitionParentIdentity[]? InitialChildFamilyBackground(ICognitionPersonalIdentityWorld world, string id, AgentState physical)
    {
        if (!physical.IdentityChoicePending || !world.Society.Births.Any(birth => birth.ChildId == id)) return null;
        return world.Society.Relationships.Where(edge => edge.Type == SocietyRelationshipType.BiologicalParentage && edge.TargetId == id)
            .Select(edge => edge.ProposerId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(parentId =>
            {
                var identity = world.Agents.GetValueOrDefault(parentId) ?? world.DeceasedAgents.GetValueOrDefault(parentId)?.LastPhysical;
                return new CognitionParentIdentity(parentId, world.Society.GetAgent(parentId).Name,
                    identity is { IdentityChoicePending: false } ? identity.Personality : null,
                    identity is { IdentityChoicePending: false } ? identity.Aspiration : null);
            }).ToArray();
    }


    internal static void ApplyPersonalIdentityChoice(ICognitionPersonalIdentityWorld world, string id, CognitionDecisionRequest request,
        CognitionDecisionResponse? response, CognitionAdmissionResult admission)
    {
        if (!admission.Accepted || admission.FellBack ||
            response is not { Provider: DecisionProviderKind.LargeLanguageModel } ||
            !request.Observation.NeedsPersonality && !request.Observation.NeedsAspiration ||
            !world.Agents.TryGetValue(id, out var physical) || !physical.IdentityChoicePending)
            return;

        var personality = CognitionDecisionResponse.NormalizeIdentityText(response.ChosenPersonality);
        var aspiration = CognitionDecisionResponse.NormalizeIdentityText(response.ChosenAspiration);
        // A world-born child's first identity needs both choices. An unusable
        // reply leaves the ordinary next decision to ask again, without a
        // separate identity request or inheriting a parent's traits.
        if (world.Society.Births.Any(birth => birth.ChildId == id) && (personality is null || aspiration is null)) return;
        world.PhysicalAgents.ChooseIdentity(id, physical, personality ?? physical.Personality, aspiration ?? physical.Aspiration, false);
        world.MarkCheckpointCurrent();
        world.AppendEvent(personality is not null || aspiration is not null ? new AgentIdentityChosenEventPayload(id) : new AgentIdentityKeptEventPayload(id));
    }

}
