using ClankerWorld.Presentation;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Server.Observation;

public sealed partial class OwnerWorldObservationStore
{
    private readonly object eventPresentationGate = new();
    private readonly SortedDictionary<long, WorldEventEntry> eventPresentationEvents = [];
    private WorldObserverTimeline? eventPresentationTimeline;

    // Connected clients retain more events than a compacted checkpoint. Capture
    // their bounded observer window with the state, before projecting either.
    private (WorldRuntimeState State, WorldDiagnostics Diagnostics, WorldObserverTimeline Timeline,
        WorldEventEntry[] Events) CapturePresentationObservation()
    {
        lock (eventPresentationGate)
        {
            var (state, diagnostics, timeline) = worldRuntime.ExportObservation();
            if (eventPresentationTimeline != timeline)
            {
                eventPresentationEvents.Clear();
                eventPresentationTimeline = timeline;
            }
            foreach (var item in state.Events.TakeLast(EventVocabulary.RetainedEventCount))
                eventPresentationEvents[item.EventId] = item;
            while (eventPresentationEvents.Count > EventVocabulary.RetainedEventCount)
                eventPresentationEvents.Remove(eventPresentationEvents.First().Key);
            return (state, diagnostics, timeline, eventPresentationEvents.Values.ToArray());
        }
    }

    private static bool NeedsCurrentEventText(WorldEventEntry item) =>
        EventVocabulary.IsPlayerFacingEvent(item.Kind) || item.Payload is ModelAttemptStatusEventPayload status &&
        EventVocabulary.IsPlayerFacingModelStatus(status.Status);
}
