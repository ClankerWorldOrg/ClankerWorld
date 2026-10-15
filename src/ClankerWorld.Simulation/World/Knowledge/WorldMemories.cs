using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>Content-free telemetry for an accepted Jev memory-index update.</summary>
public sealed record WorldMemoryCompactionTransition(
    long WorldTick,
    string OwnerId,
    int AssessedCount,
    int IndexSize);

public sealed partial class WorldRuntime
{
    private void ApplyMemorySummary(string ownerId, CognitionMemorySummaryOption summary) => CognitionSystem.ApplyMemorySummary(CognitionMemoriesWorld, ownerId, summary);

    private void ApplyMemoryCompaction(string ownerId, IReadOnlyList<CognitionMemoryCompactionScore> scores) => CognitionSystem.ApplyMemoryCompaction(CognitionMemoriesWorld, ownerId, scores);
}
