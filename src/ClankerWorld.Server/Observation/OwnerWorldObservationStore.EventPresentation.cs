using ClankerWorld.Presentation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Observation;

public sealed partial class OwnerWorldObservationStore
{
    private readonly object eventPresentationGate = new();
    private readonly SortedDictionary<long, WorldEventEntry> eventPresentationEvents = [];
    private WorldObserverTimeline? eventPresentationTimeline;

    // Connected clients retain more events than a compacted checkpoint. Keep
    // only that bounded observer window, without changing saved history.
    private IReadOnlyList<WorldEventEntry> PresentationEvents(WorldRuntimeState state,
        WorldObserverTimeline timeline, long latestEventId)
    {
        lock (eventPresentationGate)
        {
            // Concurrent captures can finish projection out of order. An old
            // load generation must not replace the newer observer's cache.
            if (eventPresentationTimeline is { } current && current.InstanceId == timeline.InstanceId &&
                current.Generation > timeline.Generation)
                return state.Events.TakeLast(EventVocabulary.RetainedEventCount).ToArray();
            if (eventPresentationTimeline != timeline)
            {
                eventPresentationEvents.Clear();
                eventPresentationTimeline = timeline;
            }
            foreach (var item in state.Events.TakeLast(EventVocabulary.RetainedEventCount))
                eventPresentationEvents[item.EventId] = item;
            while (eventPresentationEvents.Count > EventVocabulary.RetainedEventCount)
                eventPresentationEvents.Remove(eventPresentationEvents.First().Key);
            // A later capture's events never enter an earlier snapshot.
            return eventPresentationEvents.Values.Where(item => item.EventId <= latestEventId).ToArray();
        }
    }

    private static bool NeedsCurrentEventText(WorldEventEntry item) =>
        EventVocabulary.IsPlayerFacingEvent(item.Kind) || item.Kind == "model_attempt_status" &&
        EventVocabulary.IsPlayerFacingModelStatus(EventVocabulary.ModelStatusFields(item.Detail).Status);
}
