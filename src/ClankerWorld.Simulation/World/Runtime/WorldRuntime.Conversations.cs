using PendingConversationTurn = ClankerWorld.Simulation.World.CognitionSystem.PendingConversationTurn;
using ConversationTurnOutcome = ClankerWorld.Simulation.World.CognitionSystem.ConversationTurnOutcome;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

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

    private long CurrentConversationWorldDay =>
        WorldCalendarRules.FromTick(WorldTick, worldSystems.Config).DayIndex;

    private bool HasConversationAllowance(string agentId) =>
        conversationSystem.HasConversationAllowance(conversationWorld, agentId);

    private List<CognitionCandidate> ConversationCandidates(string agentId) =>
        conversationSystem.ConversationCandidates(conversationWorld, agentId);

    private bool ApplyConversationCandidate(string agentId, string candidateId) =>
        conversationSystem.ApplyConversationCandidate(conversationWorld, agentId, candidateId);

    private bool ProposeConversation(string initiatorId, string inviteeId) =>
        conversationSystem.ProposeConversation(conversationWorld, initiatorId, inviteeId);

    private bool AcceptConversation(string participantId, string conversationId) =>
        conversationSystem.AcceptConversation(conversationWorld, participantId, conversationId);

    private bool DeclineConversation(string participantId, string conversationId) =>
        conversationSystem.DeclineConversation(conversationWorld, participantId, conversationId);

    private bool ResumeConversation(string participantId, string conversationId) =>
        conversationSystem.ResumeConversation(conversationWorld, participantId, conversationId);

    private bool EndConversation(string participantId, string conversationId) =>
        conversationSystem.EndConversation(conversationWorld, participantId, conversationId);

    private bool AcceptConversationWrapUp(string participantId, string conversationId) =>
        conversationSystem.AcceptConversationWrapUp(conversationWorld, participantId, conversationId);

    private bool DeclineConversationWrapUp(string participantId, string conversationId) =>
        conversationSystem.DeclineConversationWrapUp(conversationWorld, participantId, conversationId);

    private bool CanContinueConversation(AgentConversation conversation) =>
        conversationSystem.CanContinueConversation(conversationWorld, conversation);

    private void ReserveConversationAllowance(string agentId) =>
        conversationSystem.ReserveConversationAllowance(conversationWorld, agentId);

    private void TrimConversationHistory() =>
        conversationSystem.TrimConversationHistory(conversationWorld);

    private void UpdateConversationsForTick(long worldTick) =>
        conversationSystem.UpdateConversationsForTick(conversationWorld, worldTick);

    private bool IsUrgentConversationNeed(AgentConversation conversation) =>
        ConversationSystem.IsUrgentConversationNeed(conversationWorld, conversation);

    private bool AreConversationParticipantsTogether(AgentConversation conversation) =>
        conversationSystem.AreConversationParticipantsTogether(conversationWorld, conversation);

    private string[] ConversationListeners(AgentConversation conversation, string speakerId) =>
        ConversationSystem.ConversationListeners(conversationWorld, conversation, speakerId);

    private void ApplyConversationTurnOutcome(PendingConversationTurn pending,
        ConversationTurnOutcome outcome, long worldTick, Func<PendingConversationTurn, bool> providerRouteIsCurrent) =>
        CognitionSystem.ApplyConversationTurnOutcome(CognitionConversationTasksWorld, pending, outcome, worldTick, providerRouteIsCurrent);

    private void ExtractConversationMemoryClaims(AgentConversationTurn turn) =>
        ConversationSystem.ExtractConversationMemoryClaims(conversationWorld, turn);

    private void CompleteConversationTurns(
        IReadOnlyList<PendingConversationTurn> completed,
        long worldTick,
        Func<PendingConversationTurn, bool> providerRouteIsCurrent) =>
        CognitionSystem.CompleteConversationTurns(CognitionConversationTasksWorld, completed, worldTick, providerRouteIsCurrent);

    private void StartConversationTurns() =>
        CognitionSystem.StartConversationTurns(CognitionConversationTasksWorld);

    private AgentConversationTurnRequest CreateConversationRequest(AgentConversation conversation, string speakerId) =>
        CognitionSystem.CreateConversationRequest(CognitionConversationTasksWorld, conversation, speakerId);

    private Task<ConversationTurnOutcome> RunConversationProviderAsync(IAgentConversationProvider provider, AgentConversationTurnRequest request, CancellationToken cancellationToken) => CognitionSystem.RunConversationProviderAsync(provider, request, modelTimeProvider, cancellationToken);

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

    private static string ToWire(AgentConversationInterruption interruption) => ConversationSystem.ToWire(interruption);


}
