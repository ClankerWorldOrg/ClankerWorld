using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// In-world keyboard shortcuts and the F1 controls list. Shortcuts press the
/// same top-bar buttons the mouse does, so they obey the same availability:
/// nothing here works behind a modal menu, only F12 works while typing in a
/// text field, and none of them commits a world change the buttons would not.
/// </summary>
public partial class Main
{
    private readonly PanelContainer controlsPanel = new();

    private static readonly (string Section, bool Right, (string[] Keys, string Action)[] Rows)[] ControlGroups =
    [
        ("Map", false, [
            (["W", "A", "S", "D"], "Move the map (or the arrows)"),
            (["+", "−"], "Zoom in or out"),
            (["H"], "Back to the first Town"),
            (["M"], "World Map"),
            (["K"], "Focus a tile: arrows move, Enter selects"),
        ]),
        ("Time and agents", false, [
            (["Space"], "Pause or resume (or P)"),
            (["N"], "Next agent"),
            (["Shift", "N"], "Previous agent"),
            (["C"], "Center on the selected agent"),
        ]),
        ("Mouse", true, [
            (["Click"], "Select an agent or inspect a tile"),
            (["Wheel"], "Zoom toward the pointer"),
            (["Middle-drag"], "Move the map"),
        ]),
        ("Keyboard focus", false, [
            (["Tab"], "Next control"),
            (["Shift", "Tab"], "Previous control"),
            (["Enter"], "Use the focused control"),
        ]),
        ("Panels", true, [
            (["F"], "Map filters"),
            (["I"], "World Info"),
            (["T"], "Towns"),
            (["R"], "Agents"),
            (["E"], "Event Log"),
            (["F12"], "Developer tools"),
            (["F1"], "This list (or ?)"),
            (["Esc"], "Close a panel, or open the Pause Menu"),
        ]),
    ];

    /// <summary>The controls list's two columns of grouped keycaps.</summary>
    private readonly HBoxContainer controlsColumns = new();
    private string? renderedControlsTheme;

    private void BuildControlsPanel(Control content) => BuildControlsGroups(content);

    private void PositionControlsPanel()
    {
        controlsPanel.Size = controlsPanel.GetCombinedMinimumSize();
        controlsPanel.Position = new Vector2(
            Math.Max(14, (UiSize.X - controlsPanel.Size.X) / 2),
            Math.Max(14, (UiSize.Y - controlsPanel.Size.Y) / 2));
    }

    private void ToggleControlsPanel()
    {
        controlsPanel.Visible = !controlsPanel.Visible;
        if (controlsPanel.Visible) PositionControlsPanel();
    }

    /// <summary>World keys apply only in a world with no modal menu open and no text field focused.</summary>
    private bool WorldKeysAvailable() =>
        NoMenuOverWorld() && GetViewport().GuiGetFocusOwner() is not (LineEdit or TextEdit);

    private bool NoMenuOverWorld() =>
        isInWorld && !mainMenuOverlay.Visible && !pauseMenuPanel.Visible && !topBarShade.Visible &&
        !worldMenuOverlay.Visible && !manualSaveOverlay.Visible;

    /// <summary>
    /// Space pauses even when a clicked button still holds keyboard focus;
    /// otherwise Godot would re-press that button instead. Enter still
    /// activates a focused button. F12 types nothing, so it opens and closes
    /// Developer tools even while one of their fields is being edited.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (HandleKeyboardNavigationInput(@event)) return;
        // Observe releases before a menu or another GUI control consumes them.
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: false })
            draggingMap = false;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key ||
            key.ShiftPressed || key.CtrlPressed || key.AltPressed || key.MetaPressed)
            return;
        if (key.Keycode == Key.Space && WorldKeysAvailable())
        {
            PressTopBarAction(pauseButton);
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.F12 && DeveloperMode && NoMenuOverWorld())
        {
            ToggleDeveloperTools();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Handles one-key shortcuts; returns false for keys it does not own.</summary>
    private bool HandleShortcutKey(InputEventKey key)
    {
        if (key.Echo || key.CtrlPressed || key.AltPressed || key.MetaPressed || !WorldKeysAvailable())
            return false;
        if (key.Keycode == Key.F1 || key.Unicode == '?')
        {
            ToggleControlsPanel();
            return true;
        }
        switch (key.Keycode)
        {
            case Key.P:
                PressTopBarAction(pauseButton);
                return true;
            case Key.M:
                PressTopBarAction(mapButton);
                return true;
            case Key.F:
                PressTopBarAction(filtersButton);
                return true;
            case Key.I:
                PressTopBarAction(worldInfoButton);
                return true;
            case Key.R:
                PressTopBarAction(agentsButton);
                return true;
            case Key.T:
                ToggleTowns();
                return true;
            case Key.E:
                PressTopBarAction(eventLogButton);
                return true;
            case Key.N:
                CycleSelectedAgent(key.ShiftPressed ? -1 : 1);
                return true;
            case Key.C:
                if (selectedAgentId is { } selected) CenterOnAgent(selected);
                return true;
            case Key.H:
                CenterOnHome();
                return true;
            case Key.K:
                mapCanvas.GrabFocus();
                return true;
            case Key.Equal or Key.Plus or Key.KpAdd:
                ZoomAt(mapCanvas.Size / 2, zoomIn: true);
                return true;
            case Key.Minus or Key.KpSubtract:
                ZoomAt(mapCanvas.Size / 2, zoomIn: false);
                return true;
        }
        return false;
    }

    /// <summary>A shortcut does exactly what clicking its visible, enabled top-bar button does.</summary>
    private static void PressTopBarAction(Button button)
    {
        if (button.IsVisibleInTree() && !button.Disabled)
            button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>Selects the next living agent in Agents-list order and brings them into view.</summary>
    private void CycleSelectedAgent(int step)
    {
        if (renderedMapSnapshot is not { } snapshot) return;
        var living = snapshot.Agents
            .Where(person => !person.IsDraft && IsLiving(person))
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(person => person.Id)
            .ToArray();
        if (living.Length == 0) return;
        var current = Array.IndexOf(living, selectedAgentId);
        var next = current < 0
            ? (step > 0 ? 0 : living.Length - 1)
            : ((current + step) % living.Length + living.Length) % living.Length;
        if (living[next] != selectedAgentId) SelectAgent(living[next]);
        CenterOnAgent(living[next]);
    }

    /// <summary>Returns the camera to where the world opens: its first Town, else its people or land.</summary>
    private void CenterOnHome()
    {
        if (renderedMapSnapshot is not { } snapshot || terrainMap is null || !HasMap(snapshot)) return;
        CenterKeyboardCameraAt(InitialCameraCenter(snapshot, terrainMap), immediately: true);
    }

    /// <summary>Zooms one step while keeping the world point under <paramref name="canvasPoint"/> fixed.</summary>
    private void ZoomAt(Vector2 canvasPoint, bool zoomIn)
    {
        if (renderedMapSnapshot is not { } snapshot || !HasMap(snapshot)) return;
        var desired = cameraZooming ? cameraZoomTo : cameraZoom;
        var nextZoom = Math.Clamp(desired * (zoomIn ? 1.25f : 0.8f), minimumCameraZoom, maximumCameraZoom);
        if (Math.Abs(nextZoom - desired) <= 0.001f) return;
        BeginCameraMotion();
        cameraGliding = false;
        cameraZoomAnchor = (canvasPoint - mapStage.Position) / (currentTileSize + TileGap);
        cameraZoomPoint = canvasPoint;
        cameraZoomFrom = cameraZoom;
        cameraZoomTo = nextZoom;
        cameraZoomElapsed = 0;
        cameraZooming = Math.Abs(cameraZoomTo - cameraZoomFrom) > 0.001f;
    }

    private void BuildControlsGroups(Control content)
    {
        controlsColumns.AddThemeConstantOverride("separation", 24);
        FillControlsGroups();
        AddClosablePanelContents(controlsPanel, "Controls", controlsColumns);
        controlsPanel.ZIndex = 85;
        controlsPanel.Resized += PositionControlsPanel;
        controlsPanel.Hide();
        content.AddChild(controlsPanel);
    }

    /// <summary>The keycaps carry the theme's button colours, so the list is redrawn when the theme changes.</summary>
    private void FillControlsGroups()
    {
        if (renderedControlsTheme == UiTheme.Current.Name) return;
        renderedControlsTheme = UiTheme.Current.Name;
        ClearChildren(controlsColumns);
        var columns = controlsColumns;
        VBoxContainer Column()
        {
            var column = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            column.AddThemeConstantOverride("separation", 4);
            columns.AddChild(column);
            return column;
        }
        var left = Column();
        var right = Column();
        foreach (var (section, onRight, rows) in ControlGroups)
        {
            var column = onRight ? right : left;
            if (column.GetChildCount() > 0) column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
            column.AddChild(new Label { Text = section.ToUpperInvariant(), ThemeTypeVariation = "SectionLabel" });
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 12);
            grid.AddThemeConstantOverride("v_separation", 4);
            foreach (var (keys, action) in rows)
            {
                // F12 does nothing outside Developer mode, so the list doesn't offer it.
                if (keys is ["F12"] && !DeveloperMode) continue;
                var caps = new HBoxContainer();
                caps.AddThemeConstantOverride("separation", 3);
                for (var index = 0; index < keys.Length; index++)
                {
                    if (index > 0 && keys[index - 1] == "Shift")
                        caps.AddChild(new Label { Text = "+", ThemeTypeVariation = "DimLabel", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
                    caps.AddChild(Keycap(keys[index]));
                }
                grid.AddChild(caps);
                grid.AddChild(new Label { Text = action, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            }
            column.AddChild(grid);
        }
    }
}
