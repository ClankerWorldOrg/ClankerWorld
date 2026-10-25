using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal const int MaximumRecentThoughts = 8;

    internal static IReadOnlyList<RecentThought> AppendPrivateThought(IReadOnlyList<RecentThought>? thoughts, long tick, string text) =>
        (thoughts ?? []).Append(new RecentThought(tick, text)).TakeLast(MaximumRecentThoughts).ToArray();

    internal static void ValidateLocalAgentCognition(AgentState person)
    {
        ValidateLocalModelAttempt(person.LastModelAttempt);
        ValidateLocalIdentityMoments(person.IdentityMoments);
        ValidateLocalThoughts(person.RecentThoughts);
    }

    internal static void ValidateLocalModelAttempt(ModelAttempt? attempt)
    {
        if (attempt is not null && (!CognitionProviderFailures.IsStatus(attempt.Status) || attempt.WorldTick < 0 ||
            (attempt.LastAcceptedCandidateId is null) != (attempt.LastAcceptedTick is null) ||
            attempt.LastAcceptedTick is < 0 || attempt.LastAcceptedTick > attempt.WorldTick ||
            attempt.LastAcceptedCandidateId is { } candidate && (string.IsNullOrWhiteSpace(candidate) || candidate.Any(char.IsControl)) ||
            attempt.SetupBlocker is not (null or "unsupported_request") || attempt.SetupBlocker is not null && attempt.Status != "model_unavailable"))
            throw new InvalidDataException("The saved model attempt is invalid.");
    }

    internal static void ValidateLocalIdentityMoments(IReadOnlyList<AgentIdentityMoment>? moments)
    {
        if (moments is null) return;
        if (moments.Count > 5 || moments.Any(item => item is null) || moments.Select(item => item.Kind).Distinct().Count() != moments.Count)
            throw new InvalidDataException("The saved life moments exceed their per-life limit.");
        foreach (var item in moments)
            if (!Enum.IsDefined(item.Kind) || item.TriggeredTick < 0 ||
                item.Outcome is not ("waiting" or "requested" or "accepted" or "kept" or "interrupted") ||
                item.RequestedTick is { } requested && requested < item.TriggeredTick ||
                item.CompletedTick is { } completed && completed < (item.RequestedTick ?? item.TriggeredTick) ||
                item.Outcome == "waiting" && (item.RequestedTick is not null || item.CompletedTick is not null) ||
                item.Outcome == "requested" && (item.RequestedTick is null || item.CompletedTick is not null) ||
                item.Outcome is "accepted" or "kept" or "interrupted" && item.CompletedTick is null ||
                item.Outcome == "accepted" && (item.RequestedTick is null || item.Personality is null && item.Aspiration is null) ||
                item.Outcome != "accepted" && (item.Personality is not null || item.Aspiration is not null) ||
                item.Personality is not null && CognitionDecisionResponse.NormalizeIdentityText(item.Personality) != item.Personality ||
                item.Aspiration is not null && CognitionDecisionResponse.NormalizeIdentityText(item.Aspiration) != item.Aspiration)
                throw new InvalidDataException("The saved life moment is invalid.");
    }

    internal static void ValidateLocalThoughts(IReadOnlyList<RecentThought>? thoughts)
    {
        if (thoughts is null) return;
        if (thoughts.Count > MaximumRecentThoughts)
            throw new InvalidDataException("The private-thought history exceeds its size limit.");
        long previous = -1;
        foreach (var thought in thoughts)
        {
            if (thought is null || thought.Text is null || thought.WorldTick < 0 || thought.WorldTick < previous ||
                CognitionDecisionResponse.NormalizePrivateThought(thought.Text) != thought.Text)
                throw new InvalidDataException("The private-thought history contains an invalid entry.");
            previous = thought.WorldTick;
        }
    }
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

        if (!BoundedText.IsValid(selection.ModelId, 200, requireTrimmed: true) ||
            selection.CredentialSlotId is not { } slotId || !Guid.TryParseExact(slotId, "N", out _))
        {
            throw new InvalidDataException("The saved child model or required key-slot reference is invalid.");
        }
    }
}
