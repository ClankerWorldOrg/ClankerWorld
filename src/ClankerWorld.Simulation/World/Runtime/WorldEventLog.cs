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

    public void AppendEvent(IWorldEventWorld world, string kind, string detail, GridPoint? eventPosition = null)
    {
        GridPoint? position = eventPosition;
        for (var length = detail.Length; position is null && length > 0; length = detail.LastIndexOf(':', length - 1))
        {
            var prefix = detail[..length];
            position = world.LivingPosition(prefix) ?? world.DeceasedPosition(prefix);
            if (position is not null) break;
        }
        events.Add(new WorldEventEntry(NextEventId++, world.WorldTick, kind, detail, position));
        Version = new();
    }
}
