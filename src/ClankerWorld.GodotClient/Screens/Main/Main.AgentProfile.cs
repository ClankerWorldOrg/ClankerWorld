using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The selected agent's Profile, docked on the left with everything the quick
/// card leaves out, and the thoughts reader it opens. Historical profiles
/// open straight to the Profile, because the dead have no place on the map.
/// </summary>
public partial class Main
{
    private const int AgentProfileWidth = 300;
    private const int ReaderWidth = 560;

    private readonly PanelContainer agentProfilePanel = new();
    private readonly PanelContainer agentPortraitFrame = new();
    private readonly AtlasTexture agentPortraitTexture = new();
    private readonly Button renameToggleButton = new();
    private readonly HBoxContainer renameRow = new();
    private readonly Label profileActivityLabel = new();
    private readonly Label profileOrderLabel = new();
    private readonly Button profileFindButton = new();
    private readonly Button profileCloseButton = new();
    private readonly GridContainer profileMeters = new() { Columns = 2 };
    private readonly PixelMeter profileFullnessMeter = new() { Kind = MeterKind.Fullness, Caption = "Fullness", CaptionWidth = 54 };
    private readonly PixelMeter profileWarmthMeter = new() { Kind = MeterKind.Warmth, Caption = "Warmth", CaptionWidth = 50 };
    private readonly PixelMeter profileDietMeter = new() { Kind = MeterKind.Diet, Caption = "Diet", CaptionWidth = 54 };
    private readonly PixelMeter profileIllnessMeter = new() { Kind = MeterKind.Illness, Caption = "Illness", CaptionWidth = 50 };
    private readonly Label thoughtsHeading = new() { ThemeTypeVariation = "SectionLabel" };
    private readonly VBoxContainer speakSection = new();
    private readonly RichTextLabel instructionHistory = new();
    private readonly ScrollContainer agentOverviewScroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
    private readonly MarginContainer agentOverviewGap = new();
    private bool agentProfileFitQueued;
    private readonly Button instructionSuggestButton = new();
    private readonly Button instructionOrderButton = new();
    private readonly CheckButton instructionQueueToggle = new();
    private readonly Button instructionCancelButton = new();
    private readonly PanelContainer thoughtsInset = new() { ThemeTypeVariation = "InsetPanel" };
    private readonly Button readThoughtsButton = new();
    private readonly PanelContainer thoughtsPanel = new();
    private readonly Label thoughtsReaderTitle = new();
    private readonly RichTextLabel thoughtsReaderText = new();
    private string? renderedThoughtsReader;
    private bool agentProfileRequested;

    private void BuildAgentProfile()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 5);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        agentPortraitFrame.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        agentPortraitFrame.AddChild(new TextureRect
        {
            Texture = agentPortraitTexture,
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        });
        header.AddChild(agentPortraitFrame);
        var identity = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        identity.AddThemeConstantOverride("separation", 2);
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 2);
        selectedActorNameLabel.ThemeTypeVariation = "HeadingLabel";
        selectedActorNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectedActorNameLabel.MaxLinesVisible = 2;
        selectedActorNameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimChar;
        selectedActorNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        selectedActorNameLabel.MouseFilter = Control.MouseFilterEnum.Pass;
        nameRow.AddChild(selectedActorNameLabel);
        // A small pencil beside the name renames; it opens a field only when wanted.
        StyleIconButton(renameToggleButton, PixelGlyph.Pencil);
        renameToggleButton.Flat = true;
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            renameToggleButton.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 3, ContentMarginBottom = 3 });
        renameToggleButton.TooltipText = "Rename";
        renameToggleButton.Pressed += ToggleRenameRow;
        nameRow.AddChild(renameToggleButton);
        identity.AddChild(nameRow);
        selectedActorSummaryLabel.ThemeTypeVariation = "DimLabel";
        selectedActorSummaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        identity.AddChild(selectedActorSummaryLabel);
        header.AddChild(identity);
        var tools = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        tools.AddThemeConstantOverride("separation", 4);
        StyleIconButton(profileFindButton, PixelGlyph.Find);
        profileFindButton.TooltipText = "Center the map on this agent (C)";
        profileFindButton.Pressed += CenterOnSelectedAgent;
        tools.AddChild(profileFindButton);
        UpdateAgentProfileCloseButton(living: true);
        profileCloseButton.Pressed += AgentProfileBack;
        tools.AddChild(profileCloseButton);
        header.AddChild(tools);
        // A wrapped name changes the header height after its width is laid out.
        header.MinimumSizeChanged += QueueAgentProfileFit;
        body.AddChild(header);
        // Full width under the portrait, so what they are doing rarely wraps.
        profileActivityLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(profileActivityLabel);
        ConfigureOrderLabel(profileOrderLabel);
        body.AddChild(profileOrderLabel);

        renameAgentInput.PlaceholderText = "Agent name";
        renameAgentInput.TooltipText = "Changing a married agent's surname also updates their spouse.";
        renameAgentInput.MaxLength = 48;
        renameAgentInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        renameAgentInput.TextChanged += _ => refusedAgentRename.Forget();
        renameAgentInput.TextSubmitted += submitted => _ = RenameSelectedAgentAsync();
        renameRow.AddChild(renameAgentInput);
        renameAgentButton.Text = "Rename";
        StyleButton(renameAgentButton);
        renameAgentButton.Pressed += () => _ = RenameSelectedAgentAsync();
        renameRow.AddChild(renameAgentButton);
        renameRow.Hide();
        body.AddChild(renameRow);

        selectedAgentOverview.AddThemeConstantOverride("separation", 5);
        selectedActorConditionLabel.ThemeTypeVariation = "DimLabel";
        selectedActorConditionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        selectedAgentOverview.AddChild(selectedActorConditionLabel);
        profileMeters.AddThemeConstantOverride("h_separation", 14);
        profileMeters.AddThemeConstantOverride("v_separation", 4);
        foreach (var meter in new[] { profileFullnessMeter, profileWarmthMeter, profileDietMeter, profileIllnessMeter })
            profileMeters.AddChild(meter);
        selectedAgentOverview.AddChild(profileMeters);
        ConfigureTextPanel(inhabitantDetails, 90);
        selectedAgentOverview.AddChild(inhabitantDetails);

        // The Profile shows the latest thoughts; clicking them, or Read all,
        // opens every recent one in a larger reader beside it.
        var thoughtsRow = new HBoxContainer();
        thoughtsHeading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        thoughtsHeading.VerticalAlignment = VerticalAlignment.Center;
        thoughtsRow.AddChild(thoughtsHeading);
        readThoughtsButton.Text = "Read all";
        readThoughtsButton.TooltipText = "Open all of their recent thoughts in a larger reader.";
        StyleCompactToggle(readThoughtsButton);
        readThoughtsButton.Pressed += OpenThoughtsReader;
        thoughtsRow.AddChild(readThoughtsButton);
        selectedAgentOverview.AddChild(thoughtsRow);
        // The newest thought in two lines; clicking it, or Read all, opens the rest.
        privateThoughtHistory.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        privateThoughtHistory.MaxLinesVisible = 2;
        // A long thought stops at a whole word; the body font draws every
        // ellipsis character at mid-height, so none is added.
        privateThoughtHistory.TextOverrunBehavior = TextServer.OverrunBehavior.TrimWord;
        privateThoughtHistory.MouseFilter = Control.MouseFilterEnum.Stop;
        privateThoughtHistory.TooltipText = "Click to read all of their thoughts. Only you can see these; other agents don't know them unless they are told.";
        privateThoughtHistory.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        privateThoughtHistory.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                OpenThoughtsReader();
        };
        privateThoughtHistory.MouseEntered += () => thoughtsInset.ThemeTypeVariation = "InsetPanelHover";
        privateThoughtHistory.MouseExited += () => thoughtsInset.ThemeTypeVariation = "InsetPanel";
        thoughtsInset.AddChild(privateThoughtHistory);
        selectedAgentOverview.AddChild(thoughtsInset);

        selectedAgentOverview.AddChild(new Label { Text = "PEOPLE", ThemeTypeVariation = "SectionLabel" });
        BuildProfilePeople(selectedAgentOverview);

        var profileActions = new HBoxContainer();
        profileActions.AddThemeConstantOverride("separation", 4);
        memoriesButton.Text = "Memories";
        memoriesButton.TooltipText = "See what this agent remembers and the maps they know, including private memories.";
        memoriesButton.Pressed += OpenMemories;
        familyTreeButton.Text = "Family";
        familyTreeButton.TooltipText = "See their family tree, including those who have passed.";
        familyTreeButton.Pressed += OpenFamilyTree;
        modelSettingsButton.Text = "Model";
        modelSettingsButton.TooltipText = "Choose this agent's model and API key.";
        modelSettingsButton.Pressed += OpenAgentModelEditor;
        foreach (var button in new[] { memoriesButton, familyTreeButton, modelSettingsButton })
        {
            StyleButton(button);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            profileActions.AddChild(button);
        }
        selectedAgentOverview.AddChild(profileActions);

        // Speak to them: suggest or order beside the heading, then the message.
        speakSection.AddThemeConstantOverride("separation", 4);
        // Queue and Cancel task wrap onto a second line instead of widening the Profile.
        var speakHeading = new HFlowContainer();
        speakHeading.AddThemeConstantOverride("h_separation", 4);
        speakHeading.AddThemeConstantOverride("v_separation", 4);
        speakHeading.AddChild(new Label
        {
            Text = "SPEAK TO THEM",
            ThemeTypeVariation = "SectionLabel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var kind = new ButtonGroup();
        // One joined switch, like the other two-way choices.
        var speakSwitch = new PanelContainer { ThemeTypeVariation = "SegmentedPanel", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var speakSwitchRow = new HBoxContainer();
        speakSwitchRow.AddThemeConstantOverride("separation", 0);
        speakSwitch.AddChild(speakSwitchRow);
        foreach (var (button, text, tip) in new[]
        {
            (instructionSuggestButton, "Suggest", "They weigh it against their own plans."),
            (instructionOrderButton, "Order", "They try to do it before anything else."),
        })
        {
            button.Text = text;
            button.TooltipText = tip;
            button.ToggleMode = true;
            button.ButtonGroup = kind;
            StyleCompactToggle(button);
            speakSwitchRow.AddChild(button);
        }
        speakHeading.AddChild(speakSwitch);
        instructionSuggestButton.ButtonPressed = true;
        instructionOrderButton.Toggled += pressed => instructionQueueToggle.Visible = pressed;
        instructionQueueToggle.Text = "Queue";
        instructionQueueToggle.TooltipText = "Add this order after the current task instead of replacing it.";
        instructionQueueToggle.Visible = false;
        StyleCompactToggle(instructionQueueToggle);
        speakHeading.AddChild(instructionQueueToggle);
        instructionCancelButton.Text = "Cancel task";
        instructionCancelButton.TooltipText = "Cancel the selected agent’s waiting or active order.";
        StyleCompactToggle(instructionCancelButton);
        instructionCancelButton.Pressed += () => _ = CancelSelectedOrderAsync();
        instructionCancelButton.Hide();
        speakHeading.AddChild(instructionCancelButton);
        speakSection.AddChild(speakHeading);
        var speakRow = new HBoxContainer();
        speakRow.AddThemeConstantOverride("separation", 4);
        instructionText.PlaceholderText = "Say something...";
        instructionText.MaxLength = 512;
        instructionText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        instructionText.TextSubmitted += submitted => _ = SubmitInstructionAsync();
        speakRow.AddChild(instructionText);
        submitInstructionButton.Text = "Send";
        StyleButton(submitInstructionButton, primary: true);
        submitInstructionButton.Pressed += () => _ = SubmitInstructionAsync();
        speakRow.AddChild(submitInstructionButton);
        speakSection.AddChild(speakRow);
        // The four newest messages here; every order the world keeps opens in a reader beside the Profile.
        var messagesRow = new HBoxContainer();
        messagesRow.AddChild(new Label
        {
            Text = "YOUR MESSAGES",
            ThemeTypeVariation = "SectionLabel",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        });
        allOrdersButton.Text = "All orders";
        allOrdersButton.TooltipText = "See all of their current and queued orders and how their latest ones ended.";
        StyleCompactToggle(allOrdersButton);
        allOrdersButton.Pressed += OpenOrdersReader;
        allOrdersButton.Hide();
        messagesRow.AddChild(allOrdersButton);
        speakSection.AddChild(messagesRow);
        ConfigureTextPanel(instructionHistory, 105);
        speakSection.AddChild(instructionHistory);
        selectedAgentOverview.AddChild(speakSection);
        // The details scroll once the Profile would pass the bottom of the screen.
        selectedAgentOverview.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        agentOverviewGap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        agentOverviewGap.AddChild(selectedAgentOverview);
        agentOverviewScroll.AddChild(agentOverviewGap);
        selectedAgentOverview.VisibilityChanged += () => agentOverviewScroll.Visible = selectedAgentOverview.Visible;
        // Wrapped text only knows its height once laid out at its width, so measure again then.
        selectedAgentOverview.MinimumSizeChanged += QueueAgentProfileFit;
        body.AddChild(agentOverviewScroll);

        BuildAgentModelScroll();
        selectedAgentModelScroll.Hide();
        body.AddChild(selectedAgentModelScroll);

        AddPanelContents(agentProfilePanel, body);
        agentProfilePanel.CustomMinimumSize = new Vector2(AgentProfileWidth, 0);
        // Top-bar panels such as Filters open over the Profile while in use.
        agentProfilePanel.ZIndex = 75;
        agentProfilePanel.Hide();
    }

    /// <summary>A flat, short tab-style button that sits beside a section heading.</summary>
    private static void StyleCompactToggle(Button button)
    {
        button.ThemeTypeVariation = "TabButton";
        button.FocusMode = Control.FocusModeEnum.All;
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            var box = (StyleBox)UiTheme.Theme.GetStylebox(state, "TabButton").Duplicate();
            box.ContentMarginTop = box.ContentMarginBottom = 3;
            button.AddThemeStyleboxOverride(state, box);
        }
    }

    /// <summary>Reader headings share a bounded name and keep Close visible for every accepted name.</summary>
    private static HBoxContainer ReaderHeading(PanelContainer panel, Label title, string closeHint)
    {
        title.ThemeTypeVariation = "HeadingLabel";
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.MaxLinesVisible = 2;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimChar;
        title.MouseFilter = Control.MouseFilterEnum.Pass;
        var heading = new HBoxContainer();
        heading.AddChild(title);
        var close = CloseButton(closeHint);
        close.Pressed += () => panel.Hide();
        heading.AddChild(close);
        return heading;
    }

    private void BuildThoughtsReader()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        body.AddChild(ReaderHeading(thoughtsPanel, thoughtsReaderTitle, "Close thoughts (Esc)"));
        body.AddChild(new Label
        {
            Text = "Only you can read these. Other agents don't know them unless they are told.",
            ThemeTypeVariation = "DimLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        ConfigureTextPanel(thoughtsReaderText, 1000);
        thoughtsReaderText.AddThemeConstantOverride("paragraph_separation", 4);
        body.AddChild(thoughtsReaderText);
        AddPanelContents(thoughtsPanel, body);
        thoughtsPanel.ZIndex = 85;
        thoughtsPanel.Resized += () => PlaceReaderPanel(thoughtsPanel);
        thoughtsPanel.Hide();
    }

    private void OpenThoughtsReader()
    {
        if (SelectedInhabitant() is not { } inhabitant || agentCardSnapshot is not { } snapshot) return;
        memoriesPanel.Hide();
        familyTreePanel.Hide();
        ordersPanel.Hide();
        agentsPanel.Hide();
        eventLogPanel.Hide();
        worldMapPanel.Hide();
        worldInfoPanel.Hide();
        conversationPanel.Hide();
        RenderThoughtsReader(snapshot, inhabitant);
        thoughtsPanel.Show();
        ApplyResponsiveLayout();
    }

    /// <summary>Every recent private thought, newest first, under a heading for each day.</summary>
    private void RenderThoughtsReader(OwnerWorldSnapshot snapshot, OwnerWorldInhabitant inhabitant)
    {
        thoughtsReaderTitle.Text = thoughtsReaderTitle.TooltipText = IsDeceased(inhabitant) ? $"{inhabitant.DisplayName} · thoughts, historical" : $"{inhabitant.DisplayName} · thoughts";
        var thoughts = inhabitant.RecentPrivateThoughts.Reverse()
            .Select(thought => (Clock: SplitClock(DisplayWorldClock(thought.WorldTick)), thought.Text)).ToArray();
        var signature = inhabitant.Id + "|" + UiTheme.Current.Name + "\n" +
            string.Join("\n", thoughts.Select(item => $"{item.Clock.Date}|{item.Clock.Time}|{item.Text}"));
        if (renderedThoughtsReader == signature) return;
        renderedThoughtsReader = signature;
        thoughtsReaderText.Clear();
        if (thoughts.Length == 0)
        {
            thoughtsReaderText.PushColor(DimText);
            thoughtsReaderText.AddText("None recorded yet.");
            thoughtsReaderText.Pop();
        }
        string? day = null;
        foreach (var (clock, text) in thoughts)
        {
            if (day is not null) thoughtsReaderText.Newline();
            if (clock.Date != day)
            {
                if (day is not null) thoughtsReaderText.Newline();
                thoughtsReaderText.PushFont(UiFonts.Headings, thoughtsReaderText.GetThemeFontSize("normal_font_size"));
                thoughtsReaderText.PushColor(HeadingText);
                thoughtsReaderText.AddText(clock.Date);
                thoughtsReaderText.Pop();
                thoughtsReaderText.Pop();
                thoughtsReaderText.Newline();
                day = clock.Date;
            }
            thoughtsReaderText.PushColor(DimText);
            thoughtsReaderText.AddText(clock.Time + "  ");
            thoughtsReaderText.Pop();
            thoughtsReaderText.AddText(GameUiText.PlainEllipses(text));
        }
        FitTextPanel(thoughtsReaderText);
    }

    /// <summary>
    /// Readers such as Memories and thoughts open beside the Profile when there
    /// is room, so both stay in view, and in the middle of the screen otherwise.
    /// </summary>
    private void PlaceReaderPanel(PanelContainer panel)
    {
        var size = panel.GetCombinedMinimumSize();
        panel.Size = size;
        var beside = agentProfilePanel.Position.X + agentProfilePanel.Size.X + 12;
        panel.Position = agentProfilePanel.Visible && beside + size.X <= UiSize.X - 14
            ? new Vector2(beside, HudTop)
            : new Vector2(Math.Max(14, (UiSize.X - size.X) / 2), Math.Max(HudTop, (UiSize.Y - size.Y) / 2));
    }

    /// <summary>Opens the Profile from the quick card; Speak also puts the cursor in the message box.</summary>
    private void OpenAgentProfile(bool speak)
    {
        agentProfileRequested = true;
        if (agentCardSnapshot is { } snapshot)
            RenderSelectedInhabitantCard(snapshot);
        if (speak && instructionText.Editable)
        {
            instructionText.CallDeferred(Control.MethodName.GrabFocus);
            QueueAgentProfileFit();
        }
        else if (keyboardNavigation)
            Callable.From(() => FocusKeyboardPanel(agentProfilePanel)).CallDeferred();
    }

    /// <summary>
    /// Steps back one level: from model settings to the Profile, from the
    /// Profile to the quick card beside the agent. A historical profile, which
    /// has no quick card, simply closes.
    /// </summary>
    private void AgentProfileBack()
    {
        if (selectedAgentModelScroll.Visible)
            CloseAgentModelEditor();
        else if (SelectedInhabitant() is { } selected && !IsDeceased(selected))
            agentProfileRequested = false;
        else
        {
            ClearInhabitantSelection();
            return;
        }
        renameRow.Hide();
        if (agentCardSnapshot is { } snapshot)
            RenderSelectedInhabitantCard(snapshot);
    }

    private void ToggleRenameRow()
    {
        refusedAgentRename.Forget();
        renameRow.Visible = !renameRow.Visible;
        if (!renameRow.Visible) return;
        renameAgentInput.Text = selectedActorNameLabel.Text;
        renameAgentInput.CallDeferred(Control.MethodName.GrabFocus);
        renameAgentInput.CallDeferred(LineEdit.MethodName.SelectAll);
    }

    /// <summary>The Profile's corner button goes back while there is somewhere to go back to, and closes otherwise.</summary>
    private void UpdateAgentProfileCloseButton(bool living)
    {
        var back = selectedAgentModelScroll.Visible || living;
        StyleIconButton(profileCloseButton, back ? PixelGlyph.Back : PixelGlyph.Close);
        profileCloseButton.TooltipText = back ? "Back (Esc)" : "Close (Esc)";
    }

    /// <summary>The Profile docks on the left, just below the top bar, and fits its contents.</summary>
    private void PositionAgentProfile()
    {
        if (!agentProfilePanel.Visible) return;
        if (selectedAgentModelScroll.Visible) FitAgentModelScroll();
        agentProfilePanel.CustomMinimumSize = new Vector2(Math.Min(AgentProfileWidth, Math.Max(1, UiSize.X - 28)), 0);
        FitAgentOverviewScroll();
        agentProfilePanel.Size = agentProfilePanel.GetCombinedMinimumSize();
        agentProfilePanel.Position = new Vector2(14, HudTop);
    }

    private void QueueAgentProfileFit()
    {
        if (agentProfileFitQueued) return;
        agentProfileFitQueued = true;
        Callable.From(() =>
        {
            agentProfileFitQueued = false;
            PositionAgentProfile();
            // Keep the message box in view while typing, once the scroll has its new height.
            if (instructionText.HasFocus())
                Callable.From(() => agentOverviewScroll.EnsureControlVisible(instructionText)).CallDeferred();
        }).CallDeferred();
    }

    /// <summary>The Profile's details take the room they need and scroll once the panel would pass the bottom of the screen.</summary>
    private void FitAgentOverviewScroll()
    {
        if (!agentOverviewScroll.Visible) return;
        var content = selectedAgentOverview.GetCombinedMinimumSize().Y;
        var rest = agentProfilePanel.GetCombinedMinimumSize().Y - agentOverviewScroll.CustomMinimumSize.Y;
        var room = Math.Max(120, UiSize.Y - HudTop - 14 - rest);
        var scrolls = content > room;
        agentOverviewGap.AddThemeConstantOverride("margin_right", scrolls ? SettingsScrollGap : 0);
        agentOverviewScroll.CustomMinimumSize = new Vector2(0, scrolls ? room : content);
    }

    private async Task RenameSelectedAgentAsync()
    {
        if (selectedInhabitantId is not { } agentId ||
            observationSession.Current?.Baseline.Snapshot is not { } snapshot ||
            snapshot.Inhabitants.All(person => person.Id != agentId))
            return;
        var attempted = renameAgentInput.Text;
        var name = attempted.Trim();
        if (name.Length is < 1 or > 48 || name.Any(char.IsControl))
        {
            SetStatus("Pick a name of 48 characters or fewer.", good: false);
            return;
        }
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var worldId = snapshot.WorldId;
        var generation = observationSession.RequestGeneration;
        await RunOwnerActionAsync(async () =>
        {
            // Hold the attempt while the host decides: a refresh in the meantime
            // must not reset the field before a refusal can keep it there.
            refusedAgentRename.Remember(worldId, agentId, attempted);
            try
            {
                var result = await AwaitCurrentWorldResultAsync(ownerApi.RenameAgentAsync(ResolveWorldUri(), authority, deviceId,
                    new OwnerAgentRenameAction(agentId, name), signer, CancellationToken.None));
                renamingAgentId = null;
                refusedAgentRename.Forget();
                renameRow.Hide();
                return result.Changed ? $"Renamed to {result.Name}" : "Name unchanged";
            }
            catch (OwnerAgentNameTakenException) when (selectedInhabitantId == agentId &&
                renamingAgentId == agentId && renameRow.Visible && renameAgentInput.Text == attempted &&
                IsCurrentWorldRequest(generation) &&
                observationSession.Current?.Baseline.Snapshot.WorldId == worldId)
            {
                // The field keeps the refused name until the player changes or
                // cancels it; refreshes still show the host's name above it.
                throw;
            }
            catch
            {
                if (IsCurrentWorldRequest(generation)) refusedAgentRename.Forget();
                throw;
            }
        });
    }
}
