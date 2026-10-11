using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public interface IWorldEventWorld
{
    long WorldTick { get; }
    GridPoint? LivingPosition(string id);
    GridPoint? DeceasedPosition(string id);
}

/// <summary>The single ordered event sink; prepared runtimes own separate logs.</summary>
public sealed class WorldEventLog
{
    private readonly List<PlaytestWorldEvent> events;
    public ReadOnlyCollection<PlaytestWorldEvent> Events { get; }
    public long NextEventId { get; private set; }
    public long HistoryFloor { get; }
    public string? ArchiveHead { get; }
    public object Version { get; private set; } = new();

    public WorldEventLog(IReadOnlyList<PlaytestWorldEvent> savedEvents, long historyFloor = 0, string? archiveHead = null)
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
        events.Add(new PlaytestWorldEvent(NextEventId++, world.WorldTick, kind, detail, position));
        Version = new();
    }
}
