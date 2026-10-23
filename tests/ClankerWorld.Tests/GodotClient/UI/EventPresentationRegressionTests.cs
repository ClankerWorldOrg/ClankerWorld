using System.Text.Json;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.Server.Observation;
using ClankerWorld.Server.Saves;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class EventPresentationRegressionTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ConnectedRetainedRowsRefreshNamesAfterNativeHistoryCompaction()
    {
        var directory = Directory.CreateTempSubdirectory("clanker-event-presentation-history-");
        try
        {
            using var world = NormalPathWorld.CreateGenerated("event-presentation-history", _ => new DeterministicDecisionProvider());
            world.Pause();
            var reset = world.ExportState();
            var founder = NormalPathWorld.Founder1;
            for (var index = 0; index < 1025; index++)
                world.RenameAgent(founder, index % 2 == 0 ? "Aster Vale" : "Corin Vale");
            world.SubmitInstruction(new OwnerInstructionRequest("retained-event-text", "owner", founder, OwnerInstructionKind.Order, "dance"));
            var store = new OwnerWorldObservationStore(world);
            var initial = store.GetReconnectBaseline(0);
            var row = initial.Events.Events.Last(item => item.Kind == "instruction_not_understood");
            Assert.Contains("Aster Vale", row.Text, StringComparison.Ordinal);
            var known = initial.Events.Events.ToDictionary(item => item.EventId);
            for (var index = 0; index < 3; index++)
                world.RenameAgent(founder, index % 2 == 0 ? "Corin Vale" : "Aster Vale");
            var held = store.GetReconnectBaseline(initial.Snapshot.LatestEventId);
            foreach (var item in held.Events.Events) known[item.EventId] = item;
            for (var index = 0; index < 1022; index++)
                world.RenameAgent(founder, index % 2 == 0 ? "Aster Vale" : "Corin Vale");
            Assert.True(new WorldStateFile(Path.Combine(directory.FullName, "world.json")).Save(world));
            var saved = WorldRuntimeCodec.Encode(world.ExportState());
            var current = store.GetReconnectBaseline(held.Snapshot.LatestEventId);
            foreach (var item in current.Events.Events) known[item.EventId] = item;
            foreach (var id in known.Keys.OrderDescending().Skip(ClankerWorld.Presentation.EventVocabulary.RetainedEventCount).ToArray()) known.Remove(id);

            Assert.Equal(WorldHistory.RecentEventLimit, world.ExportState().Events.Count);
            Assert.True(current.Events.EventHistoryFloor > row.EventId);
            Assert.False(current.Events.ResetRequired);
            Assert.DoesNotContain(world.ExportState().Events, item => item.EventId == row.EventId);
            Assert.Contains(row.EventId, known.Keys);
            Assert.Contains(row.EventId, current.Snapshot.EventTexts.Keys);
            var snapshot = RoundTrip<OwnerWorldSnapshot>(current.Snapshot);
            var clientRow = RoundTrip<OwnerWorldEvent>(row);
            var visible = GameUiText.PlayerEvents(known.Values.Select(RoundTrip<OwnerWorldEvent>))
                .OrderByDescending(item => item.EventId).Take(ClankerWorld.Presentation.EventVocabulary.VisibleEventCount).ToArray();
            Assert.Contains(visible, item => item.EventId == row.EventId);
            Assert.Contains("Corin Vale", WorldEventText.Describe(clientRow, snapshot), StringComparison.Ordinal);
            Assert.InRange(snapshot.EventTexts.Count, 1, ClankerWorld.Presentation.EventVocabulary.RetainedEventCount);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));
            _ = store.GetSnapshot();
            _ = store.GetEventsAfter(held.Snapshot.LatestEventId);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));

            var compacted = world.ExportState();
            world.LoadPausedCheckpoint(compacted);
            var reloaded = store.GetReconnectBaseline(0);
            Assert.Empty(reloaded.Snapshot.EventTexts);
            Assert.DoesNotContain(row.EventId, reloaded.Snapshot.EventTexts.Keys);
            Assert.NotEqual(current.Timeline, reloaded.Timeline);
            Assert.Equal(saved, WorldRuntimeCodec.Encode(world.ExportState()));

            world.LoadPausedCheckpoint(reset);
            var restored = store.GetReconnectBaseline(0);
            Assert.DoesNotContain(row.EventId, restored.Snapshot.EventTexts.Keys);
            Assert.NotEqual(current.Timeline, restored.Timeline);
            Assert.Equal(WorldRuntimeCodec.Encode(reset), WorldRuntimeCodec.Encode(world.ExportState()));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void LaterProjectionCannotEvictTheEarlierCapturesVisibleRow()
    {
        var directory = Directory.CreateTempSubdirectory("clanker-event-presentation-capture-");
        try
        {
            using var world = NormalPathWorld.CreateGenerated("event-presentation-capture", _ => new DeterministicDecisionProvider());
            world.Pause();
            var founder = NormalPathWorld.Founder1;
            Assert.True(world.RenameAgent(founder, "Aster Vale"));
            world.SubmitInstruction(new OwnerInstructionRequest("capture-event-text", "owner", founder, OwnerInstructionKind.Order, "dance"));
            var store = new OwnerWorldObservationStore(world);
            var initial = store.GetReconnectBaseline(0);
            var session = new OwnerWorldObservationSession();
            Assert.True(session.TryAccept(new(RoundTrip<OwnerWorldHandshake>(store.GetOwnerHandshake()),
                RoundTrip<OwnerWorldReconnectBaseline>(initial)), 0, out var initialFailure), initialFailure);
            var row = initial.Events.Events.Last(item => item.Kind == "instruction_not_understood");
            var target = row.EventId + ClankerWorld.Presentation.EventVocabulary.RetainedEventCount - 1;
            var count = checked((int)(target - initial.Snapshot.LatestEventId));
            for (var index = 0; index < count; index++)
                Assert.True(world.RenameAgent(founder, index == count - 1 ? "Zoë 🌿 Vale" : index % 2 == 0 ? "Corin Vale" : "Aster Vale"));
            var held = store.GetReconnectBaseline(initial.Snapshot.LatestEventId);
            Assert.Equal(target, held.Snapshot.LatestEventId);
            Assert.True(session.TryAccept(new(RoundTrip<OwnerWorldHandshake>(store.GetOwnerHandshake()),
                RoundTrip<OwnerWorldReconnectBaseline>(held)), initial.Snapshot.LatestEventId, out var heldFailure), heldFailure);
            Assert.Contains("Zoë 🌿 Vale", held.Snapshot.EventTexts[row.EventId], StringComparison.Ordinal);
            Assert.True(new WorldStateFile(Path.Combine(directory.FullName, "world.json")).Save(world));
            Assert.DoesNotContain(world.ExportState().Events, item => item.EventId == row.EventId);

            // Delay the actual projection on its existing map-cache gate. The
            // owning thread can re-enter it to project another device's reply.
            // No world events, snapshot values or public test hooks are injected.
            var projectionGate = typeof(OwnerWorldObservationStore).GetField("mapProjectionGate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(store)!;
            ReconnectBaselineView? earlier = null;
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try { earlier = store.GetReconnectBaseline(held.Snapshot.LatestEventId); }
                catch (Exception exception) { failure = exception; }
            })
            { IsBackground = true };
            Monitor.Enter(projectionGate);
            try
            {
                worker.Start();
                Assert.True(SpinWait.SpinUntil(() => (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(30)), "The earlier native projection did not reach its held gate.");
                Assert.True(world.RenameAgent(founder, "Noël Vale"));
                var later = store.GetSnapshot();
                Assert.Equal(target + 1, later.LatestEventId);
                Assert.DoesNotContain(row.EventId, later.EventTexts.Keys);
            }
            finally
            {
                Monitor.Exit(projectionGate);
                Assert.True(worker.Join(TimeSpan.FromSeconds(30)), "The held native projection did not finish.");
            }
            Assert.Null(failure);
            var captured = Assert.IsType<ReconnectBaselineView>(earlier);
            Assert.Equal(target, captured.Snapshot.LatestEventId);
            Assert.False(captured.Events.ResetRequired);
            Assert.Empty(captured.Events.Events);
            Assert.True(session.TryAccept(new(RoundTrip<OwnerWorldHandshake>(store.GetOwnerHandshake()),
                RoundTrip<OwnerWorldReconnectBaseline>(captured)), held.Snapshot.LatestEventId, out var acceptedFailure), acceptedFailure);
            Assert.Contains(row.EventId, captured.Snapshot.EventTexts.Keys);
            Assert.Contains("Zoë 🌿 Vale", WorldEventText.Describe(RoundTrip<OwnerWorldEvent>(row),
                RoundTrip<OwnerWorldSnapshot>(captured.Snapshot)), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static T RoundTrip<T>(object value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Wire), Wire)!;
}
