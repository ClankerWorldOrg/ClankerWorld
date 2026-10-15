using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    private readonly Dictionary<string, PendingModelDecision> models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingConversationTurn> conversations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingIdentityMoment> identities = new(StringComparer.Ordinal);
    private readonly List<WorldMemoryCompactionTransition> compactions = [];
    internal IReadOnlyDictionary<string, PendingModelDecision> ModelCalls { get; }
    internal IReadOnlyDictionary<string, PendingConversationTurn> ConversationTurns { get; }
    internal IReadOnlyDictionary<string, PendingIdentityMoment> IdentityMoments { get; }
    internal IReadOnlyList<WorldMemoryCompactionTransition> MemoryCompactions => compactions;
    internal Func<string, IDecisionProvider>? ProviderFactory { get; }
    internal int MaximumDispatch { get; }

    internal CognitionSystem(Func<string, IDecisionProvider>? providerFactory, int maximumDispatch)
    {
        ProviderFactory = providerFactory;
        MaximumDispatch = maximumDispatch;
        ModelCalls = new ReadOnlyDictionary<string, PendingModelDecision>(models);
        ConversationTurns = new ReadOnlyDictionary<string, PendingConversationTurn>(conversations);
        IdentityMoments = new ReadOnlyDictionary<string, PendingIdentityMoment>(identities);
    }

    internal void QueueModelDecision(string id, PendingModelDecision decision)
    {
        if (SystemInvariantChecks.Enabled && id != decision.Request.Observation.AgentId)
            throw new InvalidDataException("A pending model decision must belong to its actor.");
        models.Add(id, decision);
    }
    internal void QueueConversationTurn(string id, PendingConversationTurn turn)
    {
        if (SystemInvariantChecks.Enabled && id != turn.Request.ConversationId)
            throw new InvalidDataException("A pending conversation turn must belong to its conversation.");
        conversations.Add(id, turn);
    }
    internal void QueueIdentityMoment(string id, PendingIdentityMoment moment)
    {
        if (SystemInvariantChecks.Enabled && id != moment.Request.Observation.AgentId)
            throw new InvalidDataException("A pending identity moment must belong to its actor.");
        identities.Add(id, moment);
    }
    internal bool ForgetModelDecision(string id, out PendingModelDecision decision) => models.Remove(id, out decision!);
    internal bool ForgetConversationTurn(string id, out PendingConversationTurn turn) => conversations.Remove(id, out turn!);
    internal bool ForgetIdentityMoment(string id, out PendingIdentityMoment moment) => identities.Remove(id, out moment!);
    internal bool ForgetCompletedModelDecision(string id) => models.Remove(id);
    internal bool ForgetCompletedConversationTurn(string id) => conversations.Remove(id);
    internal bool ForgetCompletedIdentityMoment(string id) => identities.Remove(id);
    internal void RecordMemoryCompaction(WorldMemoryCompactionTransition transition) => compactions.Add(transition);
}
