namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static void ValidateChildModelSelection(ChildPersonalModelSelection selection)
    {
        if (selection.Role != WorldRuntime.ChildModelRole || selection.ChoiceReason is not
            (WorldRuntime.ChildModelChoiceParentsAgreed or WorldRuntime.ChildModelChoiceInitiatingParent or WorldRuntime.ChildModelChoiceNoParentModel))
        {
            throw new InvalidDataException("The saved child model choice has an unsupported role or reason.");
        }

        if (selection.Provider is null)
        {
            if (selection.EndpointIdentity is not null || selection.ModelId is not null || selection.CredentialSlotId is not null ||
                selection.ChoiceReason == WorldRuntime.ChildModelChoiceParentsAgreed)
            {
                throw new InvalidDataException("The saved child model choice is incomplete.");
            }
            return;
        }

        if (selection.ChoiceReason == WorldRuntime.ChildModelChoiceNoParentModel)
            throw new InvalidDataException("A child with no parental model choice cannot have a provider assignment.");

        var endpoint = selection.Provider switch
        {
            "deterministic" => WorldRuntime.DeterministicModelEndpointIdentity,
            "openai" => WorldRuntime.OpenAiModelEndpointIdentity,
            "ollama-cloud" => WorldRuntime.OllamaCloudModelEndpointIdentity,
            "anthropic" => WorldRuntime.AnthropicModelEndpointIdentity,
            _ => throw new InvalidDataException("The saved child model provider is unsupported."),
        };
        if (selection.EndpointIdentity != endpoint)
            throw new InvalidDataException("The saved child model endpoint does not match its provider.");

        if (selection.Provider == "deterministic")
        {
            if (selection.ModelId is not null || selection.CredentialSlotId is not null)
                throw new InvalidDataException("Built-in child decisions cannot reference a model or key slot.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selection.ModelId) || selection.ModelId.Length > 200 ||
            selection.ModelId != selection.ModelId.Trim() || selection.ModelId.Any(char.IsControl) ||
            selection.CredentialSlotId is not { } slotId || !Guid.TryParseExact(slotId, "N", out _))
        {
            throw new InvalidDataException("The saved child model or required key-slot reference is invalid.");
        }
    }
}
