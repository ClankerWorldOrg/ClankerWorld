namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    public const string ChildModelRole = "personal";
    public const string ChildModelChoiceParentsAgreed = "parents_agreed";
    public const string ChildModelChoiceInitiatingParent = "initiating_parent";
    public const string ChildModelChoiceNoParentModel = "no_parent_model";

    public const string DeterministicModelEndpointIdentity = "built-in-rules-v1";
    public const string OpenAiModelEndpointIdentity = "openai-chat-completions-v1";
    public const string OllamaCloudModelEndpointIdentity = "ollama-cloud-chat-completions-v1";
    public const string AnthropicModelEndpointIdentity = "anthropic-messages-v1";

    /// <summary>Records the selected personal model after the birth tick commits.</summary>
    public void ApplyChildModelSelection(string childId, ChildPersonalModelSelection selection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childId);
        ArgumentNullException.ThrowIfNull(selection);
        gate.Wait();
        try
        {
            if (!agents.TryGetValue(childId, out var child) ||
                !society.Checkpoint.Births.Any(item => item.ChildId == childId))
            {
                throw new InvalidOperationException("A personal model can only be bound to a child born in this world.");
            }
            if (child.ChildModelSelection is { } existing)
            {
                if (existing != selection)
                    throw new InvalidOperationException("A child's birth model choice is already recorded.");
                return;
            }

            ValidateChildModelSelection(selection);
            agentStore.SelectChildModel(childId, child, selection);
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent(new ChildModelSelectedEventPayload(childId, selection.ChoiceReason));
        }
        finally
        {
            gate.Release();
        }
    }

    private static void ValidateChildModelSelection(ChildPersonalModelSelection selection) =>
        CognitionSystem.ValidateChildModelSelection(selection);
}
