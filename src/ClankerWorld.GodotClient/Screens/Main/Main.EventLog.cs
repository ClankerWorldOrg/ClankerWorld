using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The Event Log: rows with an icon per kind of event under a heading for
/// each day, newest first, with the newcomer offer above them while the
/// continuity rule is on.
/// </summary>
public partial class Main
{
    private readonly VBoxContainer eventRows = new();
    private readonly ScrollContainer eventScroll = new();

    private void RenderEventLog()
    {
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var offersNewcomer = WorldEventText.OffersNewcomer(snapshot);
        var entries = GameUiText.PlayerEvents(knownEvents.Values)
            .OrderByDescending(worldEvent => worldEvent.EventId)
            .Take(ClankerWorld.Presentation.EventVocabulary.VisibleEventCount)
            .Select(worldEvent => (worldEvent.EventId, Located: worldEvent.Position is not null,
                Clock: DisplayWorldClock(worldEvent.WorldTick), Text: DescribeWorldEvent(worldEvent, snapshot)))
            .ToArray();
        // Rebuilding identical rows every refresh would reset the reader's
        // scroll position, so only a changed list is redrawn.
        UpdateUnreadEvents(snapshot?.WorldId ?? eventLogWorldId);
        var content = newEventsAfter + "|" + offersNewcomer + "\n" +
            string.Join("\n", entries.Select(entry => $"{entry.EventId}|{entry.Located}|{entry.Clock}|{entry.Text}"));
        if (renderedEventLog == content) return;
        renderedEventLog = content;
        RenderEventRows(entries, offersNewcomer);
        eventLog.Clear();
        if (offersNewcomer)
        {
            // Keep this current offer above the history even after the original
            // rule-on event leaves the bounded history or the latest thirty rows.
            eventLog.PushColor(UiTheme.Current.Warning);
            eventLog.AddText(WorldEventText.ContinuityRisk + " ");
            eventLog.Pop();
            eventLog.PushMeta("add-newcomer");
            eventLog.PushColor(LinkText);
            eventLog.AddText("Add a newcomer");
            eventLog.Pop();
            eventLog.Pop();
            if (entries.Length > 0)
            {
                eventLog.Newline();
                eventLog.Newline();
            }
        }
        if (entries.Length == 0 && !offersNewcomer)
        {
            eventLog.PushColor(DimText);
            eventLog.AddText("Nothing notable has happened yet.");
            eventLog.Pop();
            FitTextPanel(eventLog);
            return;
        }

        // Newest first, grouped under each day so a time is enough per row.
        // Each row ends where the next begins, so no blank line trails the log.
        string? day = null;
        foreach (var entry in entries)
        {
            var (date, time) = SplitClock(entry.Clock);
            if (day is not null) eventLog.Newline();
            if (date != day)
            {
                if (day is not null) eventLog.Newline();
                eventLog.PushFont(UiFonts.Headings, eventLog.GetThemeFontSize("normal_font_size"));
                eventLog.PushColor(HeadingText);
                eventLog.AddText(date);
                eventLog.Pop();
                eventLog.Pop();
                eventLog.Newline();
                day = date;
            }
            // Rows that arrived since the log was last opened keep a small dot.
            if (entry.EventId > newEventsAfter)
            {
                eventLog.PushColor(UiTheme.Current.Warning);
                eventLog.AddText("● ");
                eventLog.Pop();
            }
            eventLog.PushColor(DimText);
            eventLog.AddText(time + "  ");
            eventLog.Pop();
            if (entry.Located)
            {
                eventLog.PushMeta(entry.EventId.ToString(CultureInfo.InvariantCulture));
                eventLog.PushColor(LinkText);
                eventLog.AddText(GameUiText.PlainEllipses(entry.Text) + " ↗");
                eventLog.Pop();
                eventLog.Pop();
            }
            else eventLog.AddText(GameUiText.PlainEllipses(entry.Text));
        }
        FitTextPanel(eventLog);
    }

    private async Task HandleEventLogActionAsync(string action)
    {
        if (action == "add-newcomer")
        {
            // A link from a held row must not reopen an offer that has ended.
            if (WorldEventText.OffersNewcomer(observationSession.Current?.Baseline.Snapshot))
                await OpenAddAgentAsync();
            return;
        }
        JumpToEvent(action);
    }

    private void JumpToEvent(string eventId)
    {
        if (!long.TryParse(eventId, CultureInfo.InvariantCulture, out var id) ||
            !knownEvents.TryGetValue(id, out var worldEvent) ||
            worldEvent.Position is not { } position)
            return;
        CenterKeyboardCameraAt(new Vector2(position.X + 0.5f, position.Y + 0.5f));
        eventLogPanel.Hide();
    }

    /// <summary>An icon for each kind of event, in the colors its subject uses elsewhere.</summary>
    private static ImageTexture EventIcon(OwnerWorldEvent worldEvent)
    {
        var dark = UiTheme.Current.Name == "dark";
        var ink = UiTheme.Current.Ink;
        var food = dark ? new Color("E8B04A") : new Color("B77C10");
        var wood = dark ? new Color("C99A62") : new Color("9C6C42");
        var green = dark ? new Color("8DBA6A") : new Color("4A7033");
        var pink = UiTheme.Current.Partner;
        var stone = dark ? new Color("A89C8C") : new Color("84715A");
        return worldEvent.Kind switch
        {
            "weather_changed" => PixelIcons.Weather((worldEvent.Payload as ClankerWorld.Simulation.World.WeatherChangedEventPayload)?.AnchorWeather ?? "clear", 1),
            "food_harvested" or "food_consumed" => PixelIcons.Texture(PixelGlyph.Basket, ink, food, 1),
            "exploration_started" or "exploration_return_started" or "exploration_completed" or "exploration_aborted"
                => PixelIcons.Texture(PixelGlyph.Map, ink, green, 1),
            "skill_learned" => PixelIcons.Texture(PixelGlyph.Book, ink, wood, 1),
            "animal_tamed" or "animal_born" or "animal_transferred" or "animals_arrived" => PixelIcons.Texture(PixelGlyph.Flower, ink, green, 1),
            "build_started" or "build_completed" or "building_placed" or "recipe_started" or "recipe_completed" or "house_tool_made" or "bridge_built" or
                "town_project_worked" or "town_project_completed" or "market_built" or "market_stall_built"
                => PixelIcons.Texture(PixelGlyph.Hammer, ink, wood, 1),
            "tree_planted" or "tree_replanted" or "crop_moisture_effect" => PixelIcons.Texture(PixelGlyph.Leaf, ink, green, 1),
            "child_born" or "partnership_accepted" or "partnership_ended" or "caregiver_assigned" or
                "guardian_needed" or "guardian_assigned" or "guardian_placement_pending" or
                "guardian_placement_completed" or "guardian_placement_cancelled" or "marriage_accepted" or "marriage_surname_agreed"
                => PixelIcons.Texture(PixelGlyph.Heart, pink, pink, 1),
            "agent_removed" or "estate_will_accepted" or "estate_will_default" or "animal_died" => PixelIcons.Texture(PixelGlyph.Grave, ink, stone, 1),
            "town_founded" or "town_founding_started" or "town_border_expanded" or
                "town_project_approved" or "town_project_resumed"
                => PixelIcons.Texture(PixelGlyph.Flag, ink, green, 1),
            "town_resident_joined" or "town_resident_left" or "town_membership_evaluated" or "town_building_assigned" or
                "town_abandoned" or "town_revived" or "town_resettled" or
                "town_admission_accepted" or "town_admission_approved" or "town_admission_lapsed" or
                "market_stall_borrowed" or "market_stall_left"
                => PixelIcons.Texture(PixelGlyph.House, ink, wood, 1),
            "paused" => PixelIcons.Texture(PixelGlyph.Pause, ink, ink, 1),
            "resumed" => PixelIcons.Texture(PixelGlyph.Play, ink, ink, 1),
            "instruction_not_understood" or "agent_building_proposed" => PixelIcons.Texture(PixelGlyph.Speech, ink, UiTheme.Current.Paper, 1),
            "barter_completed" or "town_project_donated" or "town_project_material_picked_up" or
                "town_project_material_delivered" or "town_project_material_returned" or "market_stock_loaded" or "market_stock_delivered" or
                "market_stock_collected" or "market_trade_offered" or "market_trade_completed" or "market_trade_cancelled"
                or "town_stock_salvaged"
                => PixelIcons.Texture(PixelGlyph.Box, ink, wood, 1),
            _ => PixelIcons.Texture(PixelGlyph.Globe, ink, green, 1),
        };
    }

    private void BuildEventRows(Control body)
    {
        eventScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        eventScroll.CustomMinimumSize = new Vector2(400, 120);
        eventRows.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        eventRows.AddThemeConstantOverride("separation", 2);
        // Keep the Find buttons clear of the scrollbar, as in Settings.
        var gap = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        gap.AddThemeConstantOverride("margin_right", SettingsScrollGap);
        gap.AddChild(eventRows);
        eventScroll.AddChild(gap);
        body.AddChild(eventScroll);
        eventRows.MinimumSizeChanged += QueueHudListsFit;
    }

    /// <summary>
    /// Newest first under a heading for each day; a located event gets a button
    /// that finds it on the map. While the continuity rule is on, the newcomer
    /// offer sits above the history.
    /// </summary>
    private void RenderEventRows((long EventId, bool Located, string Clock, string Text)[] entries, bool offersNewcomer)
    {
        const string actionKey = "keyboard_event_action";
        var focused = GetViewport().GuiGetFocusOwner();
        var focusedAction = focused is not null && eventRows.IsAncestorOf(focused) && focused.HasMeta(actionKey)
            ? focused.GetMeta(actionKey).AsString() : null;
        void RestoreActionFocus()
        {
            if (focusedAction is null) return;
            var replacement = KeyboardControls(eventRows).FirstOrDefault(control =>
                control.HasMeta(actionKey) && control.GetMeta(actionKey).AsString() == focusedAction);
            if (replacement is not null) replacement.GrabFocus();
            else FocusKeyboardPanel(eventLogPanel);
        }
        foreach (var child in eventRows.GetChildren())
        {
            eventRows.RemoveChild(child);
            child.QueueFree();
        }
        if (offersNewcomer)
        {
            AddNewcomerOffer();
            if (entries.Length > 0)
            {
                eventRows.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
            }
        }
        if (entries.Length == 0)
        {
            if (!offersNewcomer)
                eventRows.AddChild(new Label { Text = "Nothing notable has happened yet.", ThemeTypeVariation = "DimLabel" });
            FitHudLists();
            QueueHudListsFit();
            RestoreActionFocus();
            return;
        }
        string? day = null;
        foreach (var entry in entries)
        {
            var (date, time) = SplitClock(entry.Clock);
            if (date != day)
            {
                var band = new HBoxContainer();
                band.AddThemeConstantOverride("separation", 8);
                if (day is not null) eventRows.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
                band.AddChild(new Label { Text = date.ToUpperInvariant(), ThemeTypeVariation = "SectionLabel" });
                band.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                eventRows.AddChild(band);
                day = date;
            }
            var worldEvent = knownEvents[entry.EventId];
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
            row.AddThemeConstantOverride("separation", 8);
            var fresh = entry.EventId > newEventsAfter;
            var dot = new ColorRect
            {
                Color = fresh ? UiTheme.Current.Ember : Colors.Transparent,
                CustomMinimumSize = new Vector2(4, 4),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                TooltipText = fresh ? "New since you last looked" : string.Empty,
            };
            row.AddChild(dot);
            row.AddChild(new TextureRect { Texture = EventIcon(worldEvent), StretchMode = TextureRect.StretchModeEnum.KeepCentered, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            row.AddChild(new Label { Text = time, ThemeTypeVariation = "DimLabel", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            row.AddChild(new Label
            {
                Text = GameUiText.PlainEllipses(entry.Text),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(120, 0),
            });
            if (entry.Located)
            {
                var find = new Button
                {
                    TooltipText = "Show where this happened",
                    FocusMode = Control.FocusModeEnum.All,
                    Flat = true,
                    Icon = PixelIcons.Themed(PixelGlyph.Find, UiTheme.Current.Primary, 1),
                    MouseDefaultCursorShape = Control.CursorShape.PointingHand,
                };
                foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
                    find.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2 });
                find.AddThemeColorOverride("icon_hover_color", UiTheme.Current.Link);
                var id = entry.EventId.ToString(CultureInfo.InvariantCulture);
                find.SetMeta(actionKey, id);
                find.Pressed += () => JumpToEvent(id);
                find.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(find);
            }
            eventRows.AddChild(row);
        }
        FitHudLists();
        QueueHudListsFit();
        RestoreActionFocus();
    }

    /// <summary>
    /// The current offer, kept above the history even after the rule-on event
    /// leaves the latest rows. The button opens Add Agent and places nobody.
    /// </summary>
    private void AddNewcomerOffer()
    {
        var offer = new HBoxContainer { Name = "NewcomerOffer" };
        offer.AddThemeConstantOverride("separation", 8);
        var warning = new Label
        {
            Text = WorldEventText.ContinuityRisk,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(120, 0),
        };
        warning.AddThemeColorOverride("font_color", UiTheme.Current.Warning);
        offer.AddChild(warning);
        var add = new Button
        {
            Text = "Add a newcomer",
            TooltipText = "Open Add Agent to place another adult.",
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        add.Pressed += () => _ = HandleEventLogActionAsync("add-newcomer");
        add.SetMeta("keyboard_event_action", "add-newcomer");
        offer.AddChild(add);
        eventRows.AddChild(offer);
    }
}
