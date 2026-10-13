using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyCrowdedTileCardsAsync(OwnerWorldSnapshot map, Window window)
    {
        var originalSize = window.Size;
        var originalRenderSize = window.ContentScaleSize;
        var menuVisible = mainMenuOverlay.Visible;
        var pauseVisible = pauseMenuPanel.Visible;
        var shadeVisible = menuShade.Visible;
        mainMenuOverlay.Hide();
        pauseMenuPanel.Hide();
        menuShade.Hide();
        string[] kinds = ["wood", "stone", "grain", "grain_seed", "cultivated_greens", "cultivated_green_seed",
            "tree_seed", "sapling", "medicinal_herbs", "bandage", "medicine", "gold_ore", "gold", "gold_ornament",
            "diamond_ornament", "iron_fittings", "cloth", "wool", "leather", "milk", "simple_meal", "stew", "berry", "fish"];
        var crowded = map with
        {
            Fields = [],
            GroundStocks = kinds.Select(kind => new OwnerWorldGroundStock(new(1, 1), "household:field-smoke", kind, 1)).ToArray(),
        };
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                window.Size = size;
                window.ContentScaleSize = size;
                RenderMap(crowded);
                selectedTile = new(1, 1);
                selectedTilePanel.Show();
                RenderTileInspection(crowded);
                for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (GetViewportRect().Size != new Vector2(size.X, size.Y))
                    throw new InvalidOperationException($"The crowded-card check must render at {size}, got {GetViewportRect().Size}.");
                if (tileThings.GetChildCount() != kinds.Length ||
                    !GetViewportRect().Encloses(selectedTilePanel.GetGlobalRect()))
                    throw new InvalidOperationException($"Crowded tile cards must preserve all rows inside the viewport at {size}: {selectedTilePanel.GetGlobalRect()}.");
                var close = selectedTilePanel.FindChildren("*", nameof(Button), recursive: true, owned: false)
                    .OfType<Button>().Single(button => button.TooltipText == "Close tile inspection");
                if (!selectedTilePanel.GetGlobalRect().Encloses(close.GetGlobalRect()))
                    throw new InvalidOperationException("Close must stay visible on a crowded tile card.");
                var scroll = selectedTilePanel.FindChildren("*", nameof(ScrollContainer), recursive: true, owned: false)
                    .OfType<ScrollContainer>().SingleOrDefault();
                if (scroll?.GetVScrollBar().IsVisibleInTree() == true)
                {
                    scroll.ScrollVertical = 0;
                    var overScroll = scroll.GetGlobalRect().GetCenter();
                    var zoom = cameraZoom;
                    GetViewport().PushInput(new InputEventMouseMotion { Position = overScroll, GlobalPosition = overScroll }, true);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    GetViewport().PushInput(new InputEventMouseButton
                    {
                        Position = overScroll,
                        GlobalPosition = overScroll,
                        ButtonIndex = MouseButton.WheelDown,
                        Pressed = true,
                    }, true);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (scroll.ScrollVertical <= 0 || !Mathf.IsEqualApprox(cameraZoom, zoom))
                        throw new InvalidOperationException($"The mouse wheel must scroll the crowded card without zooming the map: scroll={scroll.ScrollVertical}, max={scroll.GetVScrollBar().MaxValue}, page={scroll.GetVScrollBar().Page}, zoom={zoom}/{cameraZoom}, hover={GetViewport().GuiGetHoveredControl()?.GetPath()}, rect={scroll.GetGlobalRect()}.");
                    close.GrabFocus();
                    await VerifyKeyboardScrollAsync(scroll, selectedTilePanel);
                    scroll.GetVScrollBar().GrabFocus();
                    await KeyboardKeyAsync(Key.End);
                    if (!scroll.GetGlobalRect().Grow(1).Encloses(tileThings.GetChild<Control>(kinds.Length - 1).GetGlobalRect()))
                        throw new InvalidOperationException("Scrolling to the end must reveal the last ground-goods row.");
                }
                var crowdedHeight = selectedTilePanel.Size.Y;
                RenderMap(crowded with { GroundStocks = [] });
                RenderTileInspection(crowded with { GroundStocks = [] });
                for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (selectedTilePanel.Size.Y >= crowdedHeight || !GetViewportRect().Encloses(selectedTilePanel.GetGlobalRect()))
                    throw new InvalidOperationException("A short tile card must shrink again after inspecting crowded ground.");
            }
        }
        finally
        {
            window.Size = originalSize;
            window.ContentScaleSize = originalRenderSize;
            RenderMap(map);
            selectedTile = new(1, 1);
            RenderTileInspection(map);
            mainMenuOverlay.Visible = menuVisible;
            pauseMenuPanel.Visible = pauseVisible;
            menuShade.Visible = shadeVisible;
        }
    }

    /// <summary>
    /// The hover label shows a picture of the ground, the World Map marks Towns
    /// and agents on parchment with a legend, and each map filter says what it draws.
    /// </summary>
    private void VerifyMapPanels()
    {
        if (renderedMapSnapshot is not { } shown || terrainMap is null)
            throw new InvalidOperationException("The map panel checks need a world on screen.");
        var snapshot = shown with { Agents = [PanelSmokeAgent("map-smoke-mira", "Mira", new OwnerWorldPosition(1, 1))] };
        RenderMap(snapshot);
        try
        {
            VerifyMapPanels(snapshot);
        }
        finally
        {
            RenderMap(shown);
        }
    }

    private void VerifyMapPanels(OwnerWorldSnapshot snapshot)
    {
        UpdateHoverReadout(snapshot, new Vector2I(0, 0));
        if (hoverReadoutSwatch.Texture is null || hoverReadoutLabel.Text.Length == 0)
            throw new InvalidOperationException("The hover label must show a swatch of the ground beside its name.");
        UpdateHoverReadout(snapshot, null);

        var towns = snapshot.Towns.Count(town => town.BorderTiles.Count > 0);
        const int agents = 1;
        if (worldMapAtlas.Backdrop != UiTheme.Current.Inset || worldMapAtlas.TownMarkerCount != towns ||
            worldMapAtlas.AgentMarkerCount != agents)
            throw new InvalidOperationException($"The World Map must sit on parchment and mark each Town and living agent: towns {worldMapAtlas.TownMarkerCount}/{towns}, agents {worldMapAtlas.AgentMarkerCount}/{agents}.");
        // A Town across the east-west seam is marked at the seam, not in the middle of the map.
        var seamTown = new OwnerWorldTown("town:seam", "Seam", "founded", 0, [], [], [new(0, 3), new(9, 3)]);
        var westOfSeam = seamTown with { BorderTiles = [new(0, 3), new(8, 3), new(9, 3)] };
        var inland = seamTown with { BorderTiles = [new(3, 3), new(4, 3), new(5, 3)] };
        if (TownMarkerTile(seamTown, 10, wrapsEastWest: true) is not { X: 0, Y: 3.5f } ||
            TownMarkerTile(seamTown, 10, wrapsEastWest: false) is not { X: 5, Y: 3.5f } ||
            TownMarkerTile(westOfSeam, 10, wrapsEastWest: true) is not { X: 9.5f } ||
            TownMarkerTile(inland, 10, wrapsEastWest: true) is not { X: 4.5f })
            throw new InvalidOperationException($"A Town's World Map mark must sit in the middle of its tiles, the short way round a wrapping map: {TownMarkerTile(seamTown, 10, wrapsEastWest: true)}.");
        var legend = worldMapPanel.FindChildren("*", nameof(Label), recursive: true, owned: false).OfType<Label>().Select(label => label.Text).ToArray();
        if (!legend.Contains("Town") || !legend.Contains("Agent") || !legend.Contains("Your view"))
            throw new InvalidOperationException("The World Map must explain its marks with a legend.");

        var filterText = filtersPanel.FindChildren("*", nameof(Label), recursive: true, owned: false).OfType<Label>().Select(label => label.Text).ToArray();
        string[] descriptions = ["A dashed line around each Town", "Tints the buildings and fields a household owns",
            "A blue edge around land titled to a Town", "Land each household may use, in its colour", "Red stripes where claims overlap or a use right is under review"];
        if (descriptions.Any(line => !filterText.Contains(line)) ||
            filterText.Any(text => text.StartsWith("Only buildings", StringComparison.Ordinal) || text.Contains("separate from building", StringComparison.Ordinal)))
            throw new InvalidOperationException("Each map filter must say in one line what it draws.");
    }
}
