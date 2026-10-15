using ClankerWorld.Simulation.Kernel;
using System.Text.Json;

namespace ClankerWorld.Simulation.World;

public sealed record ArchivedEventBatch(string Name, long PreviousFloor, long ThroughEventId, JsonElement Events);
public sealed record WorldHistorySegment(string? Parent, IReadOnlyList<ArchivedEventBatch> Streams,
    IReadOnlyList<BoatTripRequest>? ClosedBoatRequests = null);
public sealed record WorldHistoryCompaction(WorldRuntimeState State, WorldHistorySegment Segment);

/// <summary>Pure compaction plan; persistence must durably archive the segment before installing its state.</summary>
public static class WorldHistory
{
    public const int RecentEventLimit = 1024;
    public const int CompactionThreshold = 2048;
    public const int RecentBoatRequestLimit = 40;

    public static WorldHistoryCompaction Prepare(WorldRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var (transport, retired) = TransportSystem.PrepareBoatHistory(state);
        var archived = new List<ArchivedEventBatch>();
        var world = Window("world", state.Events, item => item.EventId, state.EventHistoryFloor, archived);
        var society = state.Society.Society;
        var social = Window("society", society.Events, item => item.EventId, society.EventHistoryFloor, archived);
        var inventory = society.Inventory;
        var items = Window("inventory", inventory.Events, item => item.EventId, inventory.EventHistoryFloor, archived);
        var cognition = state.Society.Cognition;
        var scheduler = Window("scheduler", cognition.Events, item => item.EventId, cognition.EventHistoryFloor, archived);
        var runtimes = cognition.Runtimes.Select(runtime =>
        {
            var window = Window($"cognition:{runtime.AgentId}", runtime.Events, item => item.EventId, runtime.EventHistoryFloor, archived);
            return runtime with { Events = window.Events, EventHistoryFloor = window.Floor };
        }).ToArray();
        if (archived.Count == 0 && retired.Length == 0)
        {
            return new(state, new(state.HistoryArchiveHead, []));
        }
        var compacted = state with
        {
            SchemaVersion = WorldRuntime.StateSchemaVersion,
            BoatTransport = transport,
            Events = world.Events,
            EventHistoryFloor = world.Floor,
            Society = state.Society with
            {
                Society = society with
                {
                    Events = social.Events,
                    EventHistoryFloor = social.Floor,
                    Inventory = InventoryStore.PrepareArchivedEventWindow(inventory, items.Events, items.Floor),
                },
                Cognition = cognition with { Events = scheduler.Events, EventHistoryFloor = scheduler.Floor, Runtimes = runtimes },
            },
        };
        return new(compacted, new(state.HistoryArchiveHead, archived, retired.Length == 0 ? null : retired));
    }

    private static (IReadOnlyList<T> Events, long Floor) Window<T>(string name, IReadOnlyList<T> events,
        Func<T, long> eventId, long floor, List<ArchivedEventBatch> archived)
    {
        if (events.Count <= CompactionThreshold)
        {
            return (events, floor);
        }
        var count = events.Count - RecentEventLimit;
        var prefix = events.Take(count).ToArray();
        var through = eventId(prefix[^1]);
        archived.Add(new(name, floor, through, JsonSerializer.SerializeToElement(prefix)));
        return (events.Skip(count).ToArray(), through);
    }
}
