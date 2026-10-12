using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Control;

internal static class ChildModelSelectionResolver
{
    public static ChildPersonalModelSelection Choose(
        string initiatingParentId,
        string partnerId,
        RuntimeProviderConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatingParentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(partnerId);
        ArgumentNullException.ThrowIfNull(configuration);

        var initiating = PersonalChoice(configuration, initiatingParentId);
        var partner = PersonalChoice(configuration, partnerId);
        if (initiating is null)
        {
            return new ChildPersonalModelSelection(
                WorldRuntime.ChildModelRole,
                null,
                null,
                null,
                null,
                partner is null
                    ? WorldRuntime.ChildModelChoiceNoParentModel
                    : WorldRuntime.ChildModelChoiceInitiatingParent);
        }

        var reason = partner is not null && SameModel(initiating, partner)
            ? WorldRuntime.ChildModelChoiceParentsAgreed
            : WorldRuntime.ChildModelChoiceInitiatingParent;
        return initiating with { ChoiceReason = reason };
    }

    private static ChildPersonalModelSelection? PersonalChoice(
        RuntimeProviderConfiguration configuration,
        string agentId)
    {
        var assignment = configuration.Assignments?.FirstOrDefault(item =>
            item.AgentId == agentId && item.Role == PlayerDecisionProviders.PlanningRole);
        if (assignment is null) return null;

        var endpoint = assignment.Provider switch
        {
            PlayerDecisionProviders.Deterministic => WorldRuntime.DeterministicModelEndpointIdentity,
            PlayerDecisionProviders.OpenAi => WorldRuntime.OpenAiModelEndpointIdentity,
            PlayerDecisionProviders.OllamaCloud => WorldRuntime.OllamaCloudModelEndpointIdentity,
            PlayerDecisionProviders.Anthropic => WorldRuntime.AnthropicModelEndpointIdentity,
            _ => null,
        };
        if (endpoint is null) return null;
        if (assignment.Provider != PlayerDecisionProviders.Deterministic && string.IsNullOrWhiteSpace(assignment.Model))
            return null;

        return new ChildPersonalModelSelection(
            WorldRuntime.ChildModelRole,
            assignment.Provider,
            endpoint,
            assignment.Provider == PlayerDecisionProviders.Deterministic ? null : assignment.Model,
            assignment.Provider == PlayerDecisionProviders.Deterministic ? null : assignment.CredentialSlotId,
            WorldRuntime.ChildModelChoiceInitiatingParent);
    }

    private static bool SameModel(ChildPersonalModelSelection left, ChildPersonalModelSelection right) =>
        left.Provider == right.Provider && left.EndpointIdentity == right.EndpointIdentity && left.ModelId == right.ModelId;
}
