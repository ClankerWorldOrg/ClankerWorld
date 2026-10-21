using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CognitionSystem
{
    internal sealed record ModelDecisionOutcome(CognitionDecisionResponse? Response, string? Failure);
    internal sealed record PendingModelDecision(
        CognitionDecisionRequest Request,
        Task<ModelDecisionOutcome> Task,
        CancellationTokenSource Cancellation,
        string? DecisionContext);
    internal sealed record ConversationTurnOutcome(AgentConversationTurnResponse? Response, Exception? Failure);
    internal sealed record PendingConversationTurn(
        AgentConversationTurnRequest Request,
        Task<ConversationTurnOutcome> Task,
        CancellationTokenSource Cancellation,
        long ProviderEpoch);

    internal sealed record IdentityMomentOutcome(CognitionDecisionResponse? Response);
    internal sealed record PendingIdentityMoment(AgentIdentityMomentKind Kind,
        CognitionDecisionRequest Request, Task<IdentityMomentOutcome> Task, CancellationTokenSource Cancellation);

    internal sealed record PreparedIdentityMoment(AgentIdentityMomentKind Kind,
        CognitionDecisionRequest Request, IDecisionProvider Provider);

}
