using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal static bool HasWaitingIdentityMoment(AgentState physical) => !physical.IdentityChoicePending && (physical.IdentityMoments ?? []).Any(moment => moment.Outcome is "waiting" or "requested");

    internal static string? CognitionHouseholdId(string? id) => id is null ? null : DerivedId.Create(id, "household-sha256:");

    internal static string GuardianCandidateContext(IEnumerable<CognitionCandidate> candidates)
    {
        var ids = candidates.Where(candidate => (candidate.Id.StartsWith("guardian_accept:", StringComparison.Ordinal) || candidate.Id.StartsWith("guardian_relocate:", StringComparison.Ordinal) || candidate.Id.StartsWith("guardian_follow:", StringComparison.Ordinal))).Select(candidate => Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate.Id)).TrimEnd('=').Replace('+', '-').Replace('/', '_')).Order(StringComparer.Ordinal).ToArray();
        return string.Join(',', ids);
    }

    internal static bool HasNewGuardianCandidate(string? lastDecisionContext, IEnumerable<CognitionCandidate> candidates)
    {
        var previouslyOffered = GuardianCandidateContextFromLastDecision(lastDecisionContext);
        return candidates.Where(candidate => (candidate.Id.StartsWith("guardian_accept:", StringComparison.Ordinal) || candidate.Id.StartsWith("guardian_relocate:", StringComparison.Ordinal) || candidate.Id.StartsWith("guardian_follow:", StringComparison.Ordinal))).Select(candidate => Convert.ToBase64String(Encoding.UTF8.GetBytes(candidate.Id)).TrimEnd('=').Replace('+', '-').Replace('/', '_')).Any(candidate => !previouslyOffered.Contains(candidate));
    }

    internal static HashSet<string> GuardianCandidateContextFromLastDecision(string? context)
    {
        const string marker = "|guardian_candidates=";
        if (context is null)
            return new(StringComparer.Ordinal);
        var start = context.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return new(StringComparer.Ordinal);
        start += marker.Length;
        var end = context.IndexOf('|', start);
        var encodedIds = end < 0 ? context[start..] : context[start..end];
        return encodedIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    }

    internal static bool HasPromptedConversationChoice(string? lastDecisionContext, string conversationChoiceContext) => lastDecisionContext?.EndsWith($"|conversation_choice={ConversationChoiceContextDigest(conversationChoiceContext)}", StringComparison.Ordinal) == true;

    internal static string ConversationChoiceContextDigest(string conversationChoiceContext) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(conversationChoiceContext)));

    internal static string ObservationDigest(string agentId, string worldId, AgentState state, IReadOnlyList<CognitionCandidate> candidates, IReadOnlyList<CognitionMemoryExcerpt> memories, IReadOnlyList<CognitionMemoryCompactionCandidate> compactionCandidates, IReadOnlyList<CognitionKnowledgeFact> knownMapFacts, CognitionSelfContext self, IReadOnlyList<CognitionObserverGuidance> observerGuidance, IReadOnlyList<CognitionMemorySummaryOption>? summaryOptions = null)
    {
        var text = new StringBuilder().Append("clankerworld.world-observation/v2|").Append(agentId).Append('|').Append(worldId.Length).Append(':').Append(worldId).Append('|').Append(state.Position.X).Append(',').Append(state.Position.Y).Append('|').Append(state.FullnessBasisPoints).Append('|').Append(string.Join(',', candidates.Select(candidate => candidate.Id)));
        foreach (var memory in memories)
            text.Append('|').Append(memory.Id.Length).Append(':').Append(memory.Id).Append('|').Append(memory.SourceTick).Append('|').Append(memory.SubjectId.Length).Append(':').Append(memory.SubjectId).Append('|').Append(memory.Summary.Length).Append(':').Append(memory.Summary).Append('|').Append(memory.Kind).Append('|').Append(memory.Visibility ?? "none").Append('|').Append(memory.Provenance ?? "none").Append('|').Append(memory.ConfidenceBasisPoints ?? -1).Append('|').Append(memory.SourceAgentId ?? "none").Append('|').Append(memory.SourceEventId ?? -1).Append('|').Append(memory.IsCorrected).Append('|').Append(memory.ImportanceBasisPoints).Append('|').Append(memory.ImportanceConfidenceBasisPoints);
        text.Append("|summaries=").Append(JsonSerializer.Serialize(summaryOptions ?? []));
        text.Append("|summary_sources=").Append(JsonSerializer.Serialize(memories.Select(memory => memory.SummarySources)));
        foreach (var memory in compactionCandidates)
            text.Append('|').Append(memory.Kind).Append('|').Append(memory.Id.Length).Append(':').Append(memory.Id).Append('|').Append(memory.SourceTick).Append('|').Append(memory.SubjectId.Length).Append(':').Append(memory.SubjectId).Append('|').Append(memory.Summary.Length).Append(':').Append(memory.Summary).Append('|').Append(memory.Visibility ?? "none").Append('|').Append(memory.Provenance ?? "none").Append('|').Append(memory.ConfidenceBasisPoints ?? -1).Append('|').Append(memory.SourceAgentId ?? "none").Append('|').Append(memory.SourceEventId ?? -1).Append('|').Append(memory.IsCorrected);
        foreach (var fact in knownMapFacts)
            text.Append("|map=").Append(fact.X).Append(',').Append(fact.Y).Append('|').Append(fact.Terrain).Append('|').Append(string.Join(',', fact.ResourceKinds)).Append('|').Append(fact.DiscovererId).Append('|').Append(fact.Acquisition).Append('|').Append(fact.LearnedTick);
        foreach (var message in observerGuidance)
            text.Append("|observer=").Append(message.InstructionId.Length).Append(':').Append(message.InstructionId).Append('|').Append(message.IssuerId.Length).Append(':').Append(message.IssuerId).Append('|').Append(message.TargetAgentId.Length).Append(':').Append(message.TargetAgentId).Append('|').Append(message.Kind).Append('|').Append(message.Text.Length).Append(':').Append(message.Text).Append('|').Append(message.SubmittedTick).Append('|').Append(message.RunEpoch).Append('|').Append(message.SubmissionSequence).Append('|').Append(message.UnderstoodTask ?? "none").Append('|').Append(message.ReplyAllowed);
        text.Append("|self=").Append(JsonSerializer.Serialize(self)).Append("|identity_pending=").Append(state.IdentityChoicePending);
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))}";
    }

    internal static async Task<ConversationTurnOutcome> RunConversationProviderAsync(IAgentConversationProvider provider, AgentConversationTurnRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await AgentConversationProviderExecution.SpeakAsync(provider, request, ConversationProviderTimeout, cancellationToken).ConfigureAwait(false);
            return new ConversationTurnOutcome(response, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ConversationTurnOutcome(null, exception);
        }
    }
}
