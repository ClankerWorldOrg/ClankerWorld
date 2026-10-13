using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyEventLogLayoutAsync()
    {
        var window = GetWindow();
        var previousWindowSize = window.Size;
        var previousWindowPosition = window.Position;
        var previousRenderSize = window.ContentScaleSize;
        var previousScaleMode = window.ContentScaleMode;
        var previousScaleAspect = window.ContentScaleAspect;
        var previousFactor = uiLayer.Factor;
        var previousInWorld = isInWorld;
        var previousRefreshing = isRefreshing;
        var previousVisibility = HudPanels().Append(familyTreePanel).Append(mainMenuOverlay)
            .Select(panel => (Panel: panel, panel.Visible)).ToArray();
        var previousEvents = knownEvents.ToArray();
        var previousRenderedEvents = renderedEventLog;
        var previousEventsWorld = eventLogWorldId;
        var previousSeen = lastSeenEventId;
        var previousNewAfter = newEventsAfter;
        var previousUnread = unreadEvents;
        var previousBadge = (eventLogBadge.Text, eventLogBadge.Visible, eventLogButton.TooltipText);
        var previousScroll = (eventScroll.ScrollHorizontal, eventScroll.ScrollVertical);
        var previousTextScroll = eventLog.GetVScrollBar().Value;

        async Task Settle()
        {
            // Container sorting and wrapped-label minima settle on separate frames.
            for (var frame = 0; frame < 8; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        async Task Resize(Vector2I size)
        {
            window.Size = size;
            window.ContentScaleSize = size;
            await Settle();
            if (GetViewport().GetVisibleRect().Size != new Vector2(size.X, size.Y) ||
                uiLayer.Factor != 1 || Math.Abs(UiSize.Y - size.Y) > 1)
                throw new InvalidOperationException($"Event Log resize must reach the native viewport and UI: requested {size}, viewport {GetViewport().GetVisibleRect().Size}, UI {UiSize}.");
        }

        void Rows(int count, bool wrapped)
        {
            knownEvents.Clear();
            var entries = Enumerable.Range(0, count).Select(index =>
            {
                long id = 1_000 + index;
                var worldEvent = new OwnerWorldEvent(id, index, wrapped ? "model_call_warning" : "tree_planted",
                    wrapped ? "used:812:limit:1000" : "founder-scout:planted-tree-1-1:broadleaf", index == 0 ? new OwnerWorldPosition(0, 0) : null)
                { Text = wrapped ? "Model calls: 812 of 1,000 used across all worlds. Your worlds pause at the limit; raise it in Settings → Game." : "Someone planted a tree." };
                knownEvents[id] = worldEvent;
                return (EventId: id, Located: worldEvent.Position is not null,
                    Clock: (index == 0 ? "2 Spring, Year 1" : "1 Spring, Year 1") + " · 12:00",
                    Text: WorldEventText.Describe(worldEvent, null));
            }).ToArray();
            RenderEventRows(entries, offersNewcomer: false);
            eventScroll.ScrollVertical = 0;
        }

        HBoxContainer[] EventRows() => eventRows.GetChildren().OfType<HBoxContainer>()
            .Where(row => row.GetChildren().OfType<TextureRect>().Any()).ToArray();

        void CheckPanelBounds(string phase)
        {
            var panel = eventLogPanel.GetGlobalRect();
            var screen = GetViewport().GetVisibleRect();
            if (!eventLogPanel.IsVisibleInTree() || panel.Position.Y < screen.Position.Y - 1 || panel.End.Y > screen.End.Y + 1)
                throw new InvalidOperationException($"Event Log must fit its screen {phase}: panel {panel}, screen {screen}.");
        }

        void CheckShortHistory(string phase)
        {
            CheckPanelBounds(phase);
            var viewport = eventScroll.GetGlobalRect();
            var rows = EventRows();
            if (eventScroll.GetVScrollBar().Visible || rows.Any(row =>
                    row.GetGlobalRect().Position.Y < viewport.Position.Y - 1 || row.GetGlobalRect().End.Y > viewport.End.Y + 1))
                throw new InvalidOperationException($"A short Event Log must show every row without scrolling {phase}: content {eventRows.GetCombinedMinimumSize().Y}, viewport {viewport}.");
        }

        try
        {
            isRefreshing = true;
            isInWorld = true;
            mainMenuOverlay.Hide();
            window.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
            await Resize(new(1280, 900));
            eventLogPanel.Hide();
            eventLogButton.EmitSignal(BaseButton.SignalName.Pressed);
            Rows(2, wrapped: true);
            await Settle();
            var shortRows = EventRows();
            var headings = eventRows.GetChildren().OfType<HBoxContainer>().Count(row =>
                row.GetChildren().OfType<Label>().Any(label => label.ThemeTypeVariation == "SectionLabel"));
            if (shortRows.Length != 2 || headings != 2 ||
                shortRows.Sum(row => row.GetChildren().OfType<Button>().Count(ShowsFind)) != 1 ||
                shortRows.Any(row => row.GetChildren().OfType<Label>().Last().GetLineCount() <= 1))
                throw new InvalidOperationException("Event Log geometry fixture must contain two genuinely wrapped rows on different days and one Find button.");
            CheckShortHistory("with two wrapped days");

            Rows(30, wrapped: true);
            await Settle();
            var longHeight = eventLogPanel.Size.Y;
            CheckPanelBounds("with thirty rows");
            if (EventRows().Length != 30 || !eventScroll.GetVScrollBar().Visible)
                throw new InvalidOperationException("A long Event Log must reach its height cap and scroll.");
            eventScroll.ScrollVertical = (int)eventScroll.GetVScrollBar().MaxValue;
            await Settle();
            var lastRow = EventRows().Last().GetGlobalRect();
            var scrolledViewport = eventScroll.GetGlobalRect();
            if (eventScroll.ScrollVertical <= 0 || lastRow.Position.Y < scrolledViewport.Position.Y - 1 ||
                lastRow.End.Y > scrolledViewport.End.Y + 1)
                throw new InvalidOperationException($"Native Event Log scrolling must reach the whole last row: row {lastRow}, viewport {scrolledViewport}.");

            // Keep these rows mounted: no explicit sizing calls may mask a missed resize signal.
            await Resize(new(1280, 540));
            CheckPanelBounds("after shrinking the window");
            var smallHeight = eventLogPanel.Size.Y;
            if (!eventScroll.GetVScrollBar().Visible || smallHeight >= longHeight)
                throw new InvalidOperationException("A shorter window must reduce the mounted Event Log's available height.");
            await Resize(new(1280, 900));
            CheckPanelBounds("after growing the window");
            if (!eventScroll.GetVScrollBar().Visible || eventLogPanel.Size.Y <= smallHeight ||
                Math.Abs(eventLogPanel.Size.Y - longHeight) > 1)
                throw new InvalidOperationException("A taller window must restore the mounted Event Log's available height.");

            Rows(2, wrapped: true);
            await Settle();
            CheckShortHistory("after replacing a long history");
            if (eventLogPanel.Size.Y >= longHeight)
                throw new InvalidOperationException("Replacing a long Event Log with a short history must shrink its panel.");
            Rows(1, wrapped: false);
            await Settle();
            if (EventRows().Single().GetChildren().OfType<Label>().Last().GetLineCount() != 1)
                throw new InvalidOperationException("The ordinary short Event Log control must remain one line.");
            CheckShortHistory("with one ordinary row");
            knownEvents.Clear();
            knownEvents[2_000] = new(2_000, 0, "model_attempt_status", "founder-scout:waiting") { Payload = new ClankerWorld.Simulation.World.ModelAttemptStatusEventPayload("founder-scout", "waiting") };
            knownEvents[2_001] = new(2_001, 1, "model_attempt_status", "founder-scout:ready") { Payload = new ClankerWorld.Simulation.World.ModelAttemptStatusEventPayload("founder-scout", "ready") };
            renderedEventLog = null;
            RenderEventLog();
            await Settle();
            if (EventRows().Length != 0) throw new InvalidOperationException("A slow successful model reply must add no Event Log row.");
            knownEvents[2_002] = new(2_002, 2, "model_attempt_status", "founder-scout:timed_out") { Payload = new ClankerWorld.Simulation.World.ModelAttemptStatusEventPayload("founder-scout", "timed_out"), Text = "Scout could not get a model reply: the model reply timed out." };
            knownEvents[2_003] = new(2_003, 3, "model_attempt_status", "founder-scout:waiting") { Payload = new ClankerWorld.Simulation.World.ModelAttemptStatusEventPayload("founder-scout", "waiting") };
            knownEvents[2_004] = new(2_004, 4, "model_attempt_status", "founder-scout:timed_out") { Payload = new ClankerWorld.Simulation.World.ModelAttemptStatusEventPayload("founder-scout", "timed_out"), Text = "Scout could not get a model reply: the model reply timed out." };
            RenderEventLog();
            await Settle();
            var failureRow = EventRows().Single();
            if (!failureRow.GetChildren().OfType<Label>().Last().Text.Contains("model reply timed out", StringComparison.Ordinal))
                throw new InvalidOperationException("An actual model failure must have one readable Event Log row.");
            RenderEventLog();
            if (EventRows().Single() != failureRow) throw new InvalidOperationException("An unchanged failure refresh must retain its row.");
        }
        finally
        {
            window.Size = previousWindowSize;
            window.Position = previousWindowPosition;
            window.ContentScaleMode = previousScaleMode;
            window.ContentScaleAspect = previousScaleAspect;
            window.ContentScaleSize = previousRenderSize;
            knownEvents.Clear();
            foreach (var entry in previousEvents) knownEvents[entry.Key] = entry.Value;
            eventLogPanel.Hide();
            eventLogWorldId = previousEventsWorld;
            lastSeenEventId = previousSeen;
            newEventsAfter = previousNewAfter;
            renderedEventLog = null;
            RenderEventLog();
            renderedEventLog = previousRenderedEvents;
            foreach (var (panel, visible) in previousVisibility) panel.Visible = visible;
            isInWorld = previousInWorld;
            await Settle();
            SetUiFactor(previousFactor);
            await Settle();
            lastSeenEventId = previousSeen;
            newEventsAfter = previousNewAfter;
            unreadEvents = previousUnread;
            (eventLogBadge.Text, eventLogBadge.Visible, eventLogButton.TooltipText) = previousBadge;
            (eventScroll.ScrollHorizontal, eventScroll.ScrollVertical) = previousScroll;
            eventLog.GetVScrollBar().Value = previousTextScroll;
            isRefreshing = previousRefreshing;
        }
    }
}
