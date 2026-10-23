using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

/// <summary>Content-free telemetry for an accepted Jev memory-index update.</summary>
public sealed record WorldMemoryCompactionTransition(
    long WorldTick,
    string OwnerId,
    int AssessedCount,
    int IndexSize);
