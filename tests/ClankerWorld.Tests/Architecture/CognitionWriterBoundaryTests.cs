namespace ClankerWorld.Tests;

public sealed class CognitionWriterBoundaryTests
{
    private static readonly HashSet<string> CognitionRecords = new(StringComparer.Ordinal)
    {
        "ModelDecisionOutcome", "PendingModelDecision", "ConversationTurnOutcome", "PendingConversationTurn",
        "IdentityMomentOutcome", "PendingIdentityMoment", "PreparedIdentityMoment", "AgentIdentityMoment", "ModelAttempt", "RecentThought",
    };

    [Fact]
    public void CognitionRecordsAndLiveRequestsHaveOneWriter() =>
        RuntimeWriterScan.AssertNamedWriters(CognitionRecords, "CognitionSystem");
}
