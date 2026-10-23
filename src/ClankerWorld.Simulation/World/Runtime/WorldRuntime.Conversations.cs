using PendingConversationTurn = ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn;
using ConversationTurnOutcome = ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const int MaximumConversationProviderCallsPerTick = 2;

    public IReadOnlyList<AgentConversation> Conversations => conversations
        .OrderByDescending(item => item.LastUpdatedTick)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<AgentConversationDailyBudget> ConversationBudgets => conversationBudgets
        .OrderBy(item => item.AgentId, StringComparer.Ordinal)
        .ToArray();

    private void SuspendRestoredConversations() =>
        conversationSystem.SuspendRestoredConversations(conversationWorld);

    private bool HasExplicitConversationProvider(string agentId) =>
        CognitionSystem.HasExplicitConversationProvider(CognitionConversationTasksWorld, agentId);

    private AgentConversation? ConversationFor(string agentId) =>
        conversationSystem.ConversationFor(agentId);

    private bool IsConversationBusy(string agentId) =>
        conversationSystem.IsConversationBusy(agentId);

    private bool ShouldDispatchConversationChoice(string agentId) =>
        conversationSystem.ShouldDispatchConversationChoice(agentId);

    private string? ConversationChoiceContextFor(string agentId) =>
        conversationSystem.ConversationChoiceContextFor(agentId);

    private bool HasConversationAllowance(string agentId) =>
        conversationSystem.HasConversationAllowance(conversationWorld, agentId);

    private List<CognitionCandidate> ConversationCandidates(string agentId) =>
        conversationSystem.ConversationCandidates(conversationWorld, agentId);

    private bool ApplyConversationCandidate(string agentId, string candidateId) =>
        conversationSystem.ApplyConversationCandidate(conversationWorld, agentId, candidateId);

    private bool ProposeConversation(string initiatorId, string inviteeId) =>
        conversationSystem.ProposeConversation(conversationWorld, initiatorId, inviteeId);

    private bool CanContinueConversation(AgentConversation conversation) =>
        conversationSystem.CanContinueConversation(conversationWorld, conversation);

    private void UpdateConversationsForTick(long worldTick) =>
        conversationSystem.UpdateConversationsForTick(conversationWorld, worldTick);

    private void CompleteConversationTurns(
        IReadOnlyList<PendingConversationTurn> completed,
        long worldTick,
        Func<PendingConversationTurn, bool> providerRouteIsCurrent) =>
        CognitionSystem.CompleteConversationTurns(CognitionConversationTasksWorld, completed, worldTick, providerRouteIsCurrent);

    private void StartConversationTurns() =>
        CognitionSystem.StartConversationTurns(CognitionConversationTasksWorld);

    private void SuspendConversation(string conversationId, AgentConversationInterruption interruption) =>
        conversationSystem.SuspendConversation(conversationWorld, conversationId, interruption);

    private void SuspendAllConversations(AgentConversationInterruption interruption) =>
        conversationSystem.SuspendAllConversations(conversationWorld, interruption);

    private void CancelPendingConversationTurn(
        string conversationId,
        AgentConversationInterruption interruption,
        bool suspendCurrent = true,
        bool underRuntimeGate = true) =>
        CognitionSystem.CancelPendingConversationTurn(CognitionConversationTasksWorld, conversationId, interruption, suspendCurrent, underRuntimeGate);

    private void ReconcilePendingConversationTurns() =>
        CognitionSystem.ReconcilePendingConversationTurns(CognitionConversationTasksWorld);

    private bool IsConversationTurnProviderCurrent(PendingConversationTurn pending) =>
        CognitionSystem.IsConversationTurnProviderCurrent(CognitionConversationTasksWorld, pending);

    private void CancelNoLongerAwaitingConversationTurns() =>
        CognitionSystem.CancelNoLongerAwaitingConversationTurns(CognitionConversationTasksWorld);
}
