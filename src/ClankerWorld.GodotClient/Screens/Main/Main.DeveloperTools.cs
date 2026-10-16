using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// Developer tools: F12 opens a panel over a world without pausing it. It
/// shows the tile under the pointer, frame time, the host's tick time and
/// the agent count; jumps the camera to any agent and draws their planned
/// path as the server reports it; and holds the older testing tools (life
/// speed, paused world editing and paired devices).
/// </summary>
public partial class Main
{
    private const float DeveloperToolsWidth = 460;
    private const double DeveloperReadoutSeconds = 0.25;

    private readonly PanelContainer developerPanel = new();
    private readonly ScrollContainer developerScroll = new();
    private readonly VBoxContainer developerBody = new();
    private readonly Label developerTileLabel = new();
    private readonly Label developerFrameLabel = new();
    private readonly Label developerTickLabel = new();
    private readonly Label developerRefusedActionLabel = new();
    private readonly Label developerAgentCountLabel = new();
    private readonly ItemList developerAgentList = new();
    private readonly CheckBox developerPathToggle = new();
    private readonly FittedLabel developerRouteLabel = new();
    private readonly PlannedPathLayer plannedPathLayer = new();
    private Vector2I? developerTile;
    private double developerFrameMilliseconds;
    private double developerReadoutAge;
    private string? developerAgentListKey;

    private void BuildDeveloperTools(Control content)
    {
        developerBody.AddThemeConstantOverride("separation", 8);

        var readouts = new VBoxContainer();
        readouts.AddThemeConstantOverride("separation", 4);
        readouts.AddChild(new Label
        {
            Text = BuildInformation.Display,
            TooltipText = "Source commit: " + BuildInformation.SourceRevision,
        });
        developerTileLabel.Text = "Point at the map.";
        readouts.AddChild(MetricRow("Tile", developerTileLabel));
        readouts.AddChild(MetricRow("Frame time", developerFrameLabel));
        developerTickLabel.TooltipText = "How long the world server took to work out the latest world tick, not counting saving it.";
        readouts.AddChild(MetricRow("Tick time", developerTickLabel));
        readouts.AddChild(MetricRow("Refused actions", developerRefusedActionLabel));
        readouts.AddChild(MetricRow("Agents", developerAgentCountLabel));
        developerBody.AddChild(NewPanel("Readouts", readouts));

        var agents = new VBoxContainer();
        agents.AddThemeConstantOverride("separation", 6);
        developerAgentList.CustomMinimumSize = new Vector2(0, 96);
        developerAgentList.AllowReselect = true;
        developerAgentList.TooltipText = "Choose an agent to select them and move the camera to them.";
        developerAgentList.ItemSelected += JumpToDeveloperAgent;
        agents.AddChild(developerAgentList);
        developerPathToggle.Text = "Show planned path";
        developerPathToggle.TooltipText = "Draw the route the selected agent is walking, as the world server planned it.";
        developerPathToggle.Toggled += _ => RenderDeveloperRoute();
        agents.AddChild(developerPathToggle);
        developerRouteLabel.ThemeTypeVariation = "DimLabel";
        developerRouteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        agents.AddChild(developerRouteLabel);
        developerBody.AddChild(NewPanel("Jump to an agent", agents));

        developerBody.AddChild(new Label
        {
            Text = "The tools below act on the running world. The aging override and world editing need it paused: press Space.",
            ThemeTypeVariation = "DimLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        BuildDeveloperEdits();
        BuildDeveloperAgingOverride();
        BuildDeveloperWorldEdit();
        BuildDeveloperDevices();

        developerScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        developerScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        // Keep the scrollbar clear of the boxes' edges, as in Settings.
        var scrollGap = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scrollGap.AddThemeConstantOverride("margin_right", SettingsScrollGap);
        scrollGap.AddChild(developerBody);
        developerScroll.AddChild(scrollGap);
        AddClosablePanelContents(developerPanel, "Developer tools", developerScroll, CloseDeveloperTools);
        developerPanel.ZIndex = 86;
        developerPanel.Hide();
        content.AddChild(developerPanel);
        // Readouts change length as they update; refit once the new text is laid out.
        developerBody.MinimumSizeChanged += () => Callable.From(PositionDeveloperTools).CallDeferred();
        UpdateWorldEditHint();
    }

    private void BuildDeveloperAgingOverride()
    {
        var agingOverrideRow = new HBoxContainer();
        agingOverrideRow.AddChild(new Label { Text = "Speed" });
        agingOverrideChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        agingOverrideChoice.AddItem("Calendar", 1);
        agingOverrideChoice.AddItem("Generations", 365);
        agingOverrideChoice.AddItem("Fast generations", 1_460);
        agingOverrideChoice.SetItemTooltip(0, "Original aging: one biological year per 365 world days.");
        agingOverrideChoice.SetItemTooltip(1, "One biological year per world day.");
        agingOverrideChoice.SetItemTooltip(2, "One biological year per quarter of a world day.");
        agingOverrideChoice.TooltipText = "A testing control that changes how fast agents age from now on, without changing the calendar, seasons or how often models are called; it is not the planned 40-day year or six-hour lifespan.";
        agingOverrideRow.AddChild(agingOverrideChoice);
        applyAgingOverrideButton.Text = "Apply";
        StyleButton(applyAgingOverrideButton);
        applyAgingOverrideButton.Pressed += () => _ = SaveAgingOverrideAsync();
        agingOverrideRow.AddChild(applyAgingOverrideButton);
        var prototypePaceBody = new VBoxContainer();
        prototypePaceBody.AddChild(new Label
        {
            Text = "For testing only. This does not set the world's planned calendar or lifespan.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        prototypePaceBody.AddChild(agingOverrideRow);
        developerBody.AddChild(NewPanel("Aging override", prototypePaceBody));
    }

    /// <summary>F12: opens or closes the panel. Time keeps running either way.</summary>
    private void ToggleDeveloperTools()
    {
        if (developerPanel.Visible)
        {
            CloseDeveloperTools();
            return;
        }
        developerPanel.Show();
        developerReadoutAge = DeveloperReadoutSeconds;
        if (renderedMapSnapshot is { } snapshot) RenderDeveloperTools(snapshot);
        PositionDeveloperTools();
    }

    private void CloseDeveloperTools()
    {
        // A field left focused in the closed panel would keep blocking map keys.
        if (GetViewport().GuiGetFocusOwner() is { } focus && developerPanel.IsAncestorOf(focus))
            focus.ReleaseFocus();
        developerPanel.Hide();
        worldEditPaint.ButtonPressed = false;
        ClearWorldEditPreview();
        RenderPlannedPath();
    }

    /// <summary>
    /// The panel sits at the right edge below the HUD, as wide as its tools
    /// need, and scrolls when the screen is too short for all of them.
    /// </summary>
    private void PositionDeveloperTools()
    {
        if (!developerPanel.Visible) return;
        var ui = UiSize;
        if (ui.X <= 0 || ui.Y <= 0) return;
        developerPanel.CustomMinimumSize = new Vector2(Math.Min(DeveloperToolsWidth, Math.Max(1, ui.X - 28)), 0);
        developerScroll.CustomMinimumSize = Vector2.Zero;
        var around = developerPanel.GetCombinedMinimumSize().Y;
        var contents = developerBody.GetCombinedMinimumSize().Y;
        var top = Math.Min(HudTop, Math.Max(14, ui.Y - 134 - around));
        developerScroll.CustomMinimumSize = new Vector2(0, Math.Clamp(contents, 120, Math.Max(120, ui.Y - top - 14 - around)));
        developerPanel.Size = developerPanel.GetCombinedMinimumSize();
        developerPanel.Position = new Vector2(Math.Max(14, ui.X - developerPanel.Size.X - 14), top);
    }

    /// <summary>Refreshes the readouts and agent list from a new observation.</summary>
    private void RenderDeveloperTools(OwnerWorldSnapshot snapshot)
    {
        if (!developerPanel.Visible) return;
        var living = snapshot.Agents.Count(person => !person.IsDraft && IsLiving(person));
        developerRefusedActionLabel.Text = snapshot.RefusedActionCount.ToString("N0", CultureInfo.InvariantCulture);
        var died = snapshot.Agents.Count(person => !person.IsDraft && !IsLiving(person));
        developerAgentCountLabel.Text = died == 0 ? $"{living} living" : $"{living} living · {died} died";
        developerTickLabel.Text = snapshot.LastTickMilliseconds is { } milliseconds
            ? $"{milliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms on the world server · tick {snapshot.WorldTick.ToString("N0", CultureInfo.InvariantCulture)}"
            : $"Not reported by this world server yet · tick {snapshot.WorldTick.ToString("N0", CultureInfo.InvariantCulture)}";
        RenderDeveloperAgentList(snapshot);
        RenderDeveloperTile();
        RenderDeveloperRoute();
    }

    private void RenderDeveloperAgentList(OwnerWorldSnapshot snapshot)
    {
        var people = snapshot.Agents.Where(person => !person.IsDraft && IsLiving(person))
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(person => person.Id, StringComparer.Ordinal)
            .ToArray();
        // Rebuild only when the people change, so the list keeps its scroll position.
        var key = string.Join('\n', people.Select(person => person.Id + "\t" + person.DisplayName));
        if (key != developerAgentListKey)
        {
            developerAgentListKey = key;
            developerAgentList.Clear();
            foreach (var person in people)
            {
                var row = developerAgentList.AddItem(person.DisplayName);
                developerAgentList.SetItemMetadata(row, person.Id);
            }
            if (people.Length == 0)
                developerAgentList.SetItemMetadata(developerAgentList.AddItem("No one lives here yet.", selectable: false), string.Empty);
        }
        developerAgentList.DeselectAll();
        for (var index = 0; index < developerAgentList.ItemCount; index++)
        {
            if (string.Equals(developerAgentList.GetItemMetadata(index).AsString(), selectedAgentId, StringComparison.Ordinal))
                developerAgentList.Select(index);
        }
    }

    /// <summary>Selects the chosen agent and brings them into view.</summary>
    private void JumpToDeveloperAgent(long index)
    {
        if (index < 0 || index >= developerAgentList.ItemCount) return;
        var id = developerAgentList.GetItemMetadata((int)index).AsString();
        if (string.IsNullOrEmpty(id)) return;
        if (!string.Equals(id, selectedAgentId, StringComparison.Ordinal)) SelectAgent(id);
        CenterOnAgent(id);
        RenderDeveloperRoute();
    }

    /// <summary>Keeps the last tile the pointer was over, so moving onto the panel does not lose it.</summary>
    private void NoteDeveloperTile(Vector2I? tile)
    {
        if (tile is null || tile == developerTile) return;
        developerTile = tile;
        if (developerPanel.Visible) RenderDeveloperTile();
    }

    private void RenderDeveloperTile()
    {
        if (developerTile is not { } tile || renderedMapSnapshot is not { } snapshot || terrainMap is null ||
            !MapContains(snapshot, tile.X, tile.Y))
        {
            developerTileLabel.Text = "Point at the map.";
            return;
        }
        var extra = new List<string>();
        if (terrainMap.ElevationAt(tile.X, tile.Y) is { } elevation) extra.Add($"Elevation {elevation}/255");
        if (WorldTerrainMap.ClimateName(terrainMap.ClimateAt(tile.X, tile.Y)) is { } climate) extra.Add(climate);
        developerTileLabel.Text = $"{tile.X}, {tile.Y}\n{HoverSummary(snapshot, terrainMap, tile)}" +
            (extra.Count == 0 ? string.Empty : "\n" + string.Join(" · ", extra));
    }

    /// <summary>Frame time is smoothed and shown a few times a second, so it can be read.</summary>
    private void UpdateDeveloperFrameTime(double delta)
    {
        if (!developerPanel.Visible) return;
        developerFrameMilliseconds = developerFrameMilliseconds <= 0
            ? delta * 1000
            : developerFrameMilliseconds + (delta * 1000 - developerFrameMilliseconds) * 0.1;
        developerReadoutAge += delta;
        if (developerReadoutAge < DeveloperReadoutSeconds) return;
        developerReadoutAge = 0;
        developerFrameLabel.Text = $"{developerFrameMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms · " +
            $"{Engine.GetFramesPerSecond().ToString("0", CultureInfo.InvariantCulture)} frames a second";
    }

    private void RenderDeveloperRoute()
    {
        var person = renderedMapSnapshot?.Agents.FirstOrDefault(item =>
            string.Equals(item.Id, selectedAgentId, StringComparison.Ordinal));
        developerRouteLabel.Text = person is null ? "Select an agent to see their route." :
            person.PlannedRoute is not { } route ? $"{person.DisplayName} is not walking anywhere right now." :
            $"{person.DisplayName}: {route.StepCount} {(route.StepCount == 1 ? "step" : "steps")} left toward " +
            $"{route.Destination.X}, {route.Destination.Y} ({Pretty(route.Reason).ToLowerInvariant()}).";
        RenderPlannedPath();
    }

    /// <summary>
    /// Draws the selected agent's reported route while the panel is open and
    /// Show planned path is on. On a wrapped map each step follows the short
    /// way round from the agent as drawn.
    /// </summary>
    private void RenderPlannedPath()
    {
        var snapshot = renderedMapSnapshot;
        var person = snapshot?.Agents.FirstOrDefault(item =>
            string.Equals(item.Id, selectedAgentId, StringComparison.Ordinal));
        if (!developerPanel.Visible || !developerPathToggle.ButtonPressed || snapshot is null || terrainMap is null ||
            person is null || person.IsDraft || !IsLiving(person) || person.PlannedRoute is not { } route)
        {
            plannedPathLayer.Clear();
            return;
        }
        var stride = currentTileSize + TileGap;
        var width = terrainMap.Width;
        float Nearer(int fromX, int toX)
        {
            var step = toX - fromX;
            return snapshot.WrapsEastWest && Math.Abs(step) > width / 2 ? step - Math.Sign(step) * width : step;
        }
        var x = WrappedMarkerX(person.Position.X * stride, width, stride, snapshot.WrapsEastWest) / stride;
        var previous = person.Position.X;
        var points = new List<Vector2> { new(x + 0.5f, person.Position.Y + 0.5f) };
        foreach (var step in route.Steps)
        {
            x += Nearer(previous, step.X);
            previous = step.X;
            points.Add(new Vector2(x + 0.5f, step.Y + 0.5f));
        }
        var goal = new Vector2(x + Nearer(previous, route.Destination.X) + 0.5f, route.Destination.Y + 0.5f);
        plannedPathLayer.SetPath(points.Select(point => point * stride).ToArray(), goal * stride, currentTileSize);
    }

    private async Task SaveAgingOverrideAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current?.Baseline.Snapshot.WorldId is not { } worldId) return;
        var rate = agingOverrideChoice.GetSelectedId();
        await RunOwnerActionAsync(async () =>
        {
            _ = await AwaitCurrentWorldResultAsync(ownerApi.SetAgingOverrideAsync(ResolveWorldUri(), authority, deviceId, rate, worldId, signer, OwnerRequestToken));
            return "Aging override saved. Current ages stay the same; agents age at the new speed from now on.";
        });
    }
}
