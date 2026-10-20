using System.Collections.ObjectModel;

namespace ClankerWorld.Simulation.World;

public interface IWorldEventWorld
{
    long WorldTick { get; }
    GridPoint? LivingPosition(string id);
    GridPoint? DeceasedPosition(string id);
}

/// <summary>The single ordered event sink; prepared runtimes own separate logs.</summary>
public sealed class WorldEventLog
{
    private readonly List<WorldEventEntry> events;
    public ReadOnlyCollection<WorldEventEntry> Events { get; }
    public long NextEventId { get; private set; }
    public long HistoryFloor { get; }
    public string? ArchiveHead { get; }
    public object Version { get; private set; } = new();

    public WorldEventLog(IReadOnlyList<WorldEventEntry> savedEvents, long historyFloor = 0, string? archiveHead = null)
    {
        events = savedEvents.ToList();
        Events = events.AsReadOnly();
        HistoryFloor = historyFloor;
        ArchiveHead = archiveHead;
        NextEventId = events.Count == 0 ? checked(historyFloor + 1) : checked(events[^1].EventId + 1);
    }

    public void AppendEvent(IWorldEventWorld world, WorldEventPayload payload, GridPoint? eventPosition = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var position = eventPosition ?? (payload.Position is { } recorded ? new GridPoint(recorded.X, recorded.Y) :
            payload.ActorId is { } actor ? world.LivingPosition(actor) ?? world.DeceasedPosition(actor) : null);
        var accepted = payload with { Position = position is { } point ? new(point.X, point.Y) : null };
        events.Add(new WorldEventEntry(NextEventId++, world.WorldTick, accepted));
        Version = new();
    }
}
