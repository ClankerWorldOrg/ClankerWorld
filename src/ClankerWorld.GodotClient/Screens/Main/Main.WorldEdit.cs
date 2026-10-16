using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The world edit form in Developer tools: one operation at a time on a
/// paused world, which the world server checks before it applies it.
/// </summary>
public partial class Main
{
    private readonly OptionButton worldEditTool = new();
    private readonly OptionButton worldEditPick = new();
    private readonly CheckBox worldEditAdvanced = new() { Text = "Advanced: typed operations" };
    private readonly CheckBox worldEditPaint = new() { Text = "Pick tiles on the map" };
    private readonly Button worldEditClear = new() { Text = "Clear preview" };
    private readonly VBoxContainer worldEditAdvancedBody = new();
    private readonly WorldEditPreviewLayer worldEditPreview = new();
    private readonly HashSet<Vector2I> worldEditTiles = [];
    private IReadOnlyList<OwnerWorldEditTool> worldEditTools = [];
    private string? worldEditWorldId;
    private (long? Epoch, string? Digest, int Width, int Height) worldEditMapContext;

    private void BuildDeveloperWorldEdit()
    {
        var worldEditBody = new VBoxContainer();
        worldEditBody.AddChild(worldEditTool);
        worldEditBody.AddChild(worldEditPick);
        worldEditBody.AddChild(worldEditPaint);
        worldEditBody.AddChild(worldEditClear);
        worldEditTool.ItemSelected += _ =>
        {
            ConfigureWorldEditPicker();
            worldEditPaint.ButtonPressed = true;
        };
        worldEditPick.ItemSelected += _ =>
        {
            ClearWorldEditPreview();
            worldEditPaint.ButtonPressed = true;
        };
        worldEditClear.Pressed += ClearWorldEditPreview;
        worldEditPaint.Toggled += active =>
        {
            if (active) UpdateWorldEditPreview();
            else ClearWorldEditPreview();
        };
        worldEditBody.AddChild(worldEditAdvanced);
        worldEditAdvanced.Toggled += advanced =>
        {
            worldEditAdvancedBody.Visible = advanced;
            worldEditTool.Visible = worldEditPick.Visible = worldEditPaint.Visible = !advanced;
            worldEditPaint.ButtonPressed = false;
            ClearWorldEditPreview();
        };
        worldEditBody.AddChild(worldEditAdvancedBody);
        worldEditAdvancedBody.Hide();
        worldEditKind.ItemSelected += _ => UpdateWorldEditHint();
        AddWorldEditKinds();
        worldEditAdvancedBody.AddChild(worldEditKind);
        worldEditId.PlaceholderText = "ID (resource/object/draft/asset as required)";
        worldEditAdvancedBody.AddChild(worldEditId);
        worldEditValue.PlaceholderText = "Value (terrain, kind, name, weather, digest...)";
        worldEditAdvancedBody.AddChild(worldEditValue);
        worldEditSecondaryValue.PlaceholderText = "Secondary value (season for set_weather_season)";
        worldEditAdvancedBody.AddChild(worldEditSecondaryValue);
        var coordinateRow = new HBoxContainer();
        ConfigureCoordinate(worldEditX, "x");
        ConfigureCoordinate(worldEditY, "y");
        coordinateRow.AddChild(worldEditX);
        coordinateRow.AddChild(worldEditY);
        worldEditRenewable.Text = "Renewable resource";
        coordinateRow.AddChild(worldEditRenewable);
        worldEditAdvancedBody.AddChild(coordinateRow);
        worldEditHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        worldEditBody.AddChild(worldEditHintLabel);
        submitWorldEditButton.Text = "Apply";
        submitWorldEditButton.Pressed += () => _ = SubmitWorldEditAsync();
        worldEditBody.AddChild(submitWorldEditButton);
        developerBody.AddChild(NewPanel("Edit world", worldEditBody));
    }

    private void RenderWorldEditPicker(OwnerWorldSnapshot snapshot)
    {
        var (width, height) = MapDimensions(snapshot);
        var context = (snapshot.Authoring?.RunEpoch, snapshot.Authoring?.CurrentMapManifestDigest ?? snapshot.MapManifestDigest, width, height);
        if (worldEditWorldId != snapshot.WorldId || worldEditMapContext != context || snapshot.Authoring?.IsPaused != true)
        {
            worldEditWorldId = snapshot.WorldId;
            worldEditMapContext = context;
            worldEditPaint.ButtonPressed = false;
            ClearWorldEditPreview();
        }
        var tools = snapshot.Authoring?.EditTools ?? [];
        if (tools.Count != worldEditTools.Count || !tools.Zip(worldEditTools).All(pair =>
            pair.First.Operation == pair.Second.Operation && pair.First.DisplayName == pair.Second.DisplayName &&
            pair.First.Kinds.SequenceEqual(pair.Second.Kinds)))
        {
            var previousOperation = PickedWorldEditTool?.Operation;
            var previousKind = PickedWorldEditKind?.Value;
            worldEditTools = tools;
            worldEditTool.Clear();
            foreach (var tool in tools)
            {
                worldEditTool.AddItem(tool.DisplayName);
                var index = worldEditTool.ItemCount - 1;
                worldEditTool.SetItemMetadata(index, tool.Operation);
                if (tool.Operation == previousOperation) worldEditTool.Select(index);
            }
            ConfigureWorldEditPicker(PickedWorldEditTool?.Operation == previousOperation ? previousKind : null);
        }
        UpdateWorldEditHint();
    }

    private OwnerWorldEditTool? PickedWorldEditTool => worldEditTool.Selected >= 0 && worldEditTool.Selected < worldEditTools.Count
        ? worldEditTools[worldEditTool.Selected] : null;

    private OwnerWorldEditKind? PickedWorldEditKind => PickedWorldEditTool is { } tool && worldEditPick.Selected >= 0 && worldEditPick.Selected < tool.Kinds.Count
        ? tool.Kinds[worldEditPick.Selected] : null;

    private void ConfigureWorldEditPicker(string? previousKind = null)
    {
        worldEditPick.Clear();
        if (PickedWorldEditTool is { } tool)
            foreach (var kind in tool.Kinds)
            {
                worldEditPick.AddItem(kind.DisplayName);
                var index = worldEditPick.ItemCount - 1;
                worldEditPick.SetItemMetadata(index, kind.Value);
                if (kind.Value == previousKind) worldEditPick.Select(index);
            }
        ClearWorldEditPreview();
    }

    private bool CanPickWorldEditTile => developerMode == true && developerPanel.Visible && worldEditPaint.ButtonPressed &&
        !worldEditAdvanced.ButtonPressed && !worldEditPaint.Disabled && PickedWorldEditKind is not null;

    private void PickWorldEditTile(OwnerWorldSnapshot snapshot, Vector2I tile)
    {
        if (!CanPickWorldEditTile || !MapContains(snapshot, tile.X, tile.Y)) return;
        // Keep a single request small enough to preview and validate as one batch.
        if (worldEditTiles.Count < 64) worldEditTiles.Add(tile);
        UpdateWorldEditPreview();
        RefreshControlAvailability();
    }

    private void ClearWorldEditPreview()
    {
        worldEditTiles.Clear();
        UpdateWorldEditPreview();
        RefreshControlAvailability();
    }

    private void UpdateWorldEditPreview()
    {
        worldEditPreview.SetPreview(CanPickWorldEditTile ? worldEditTiles : [], PickedWorldEditKind?.DisplayName ?? "");
        UpdateWorldEditHint();
    }

    private void AddWorldEditKinds()
    {
        AddWorldEditKind("set_terrain", "Set terrain — value: meadow, water, or mountain; x/y required");
        AddWorldEditKind("place_resource", "Place resource — ID, value=kind, x/y, renewable required");
        AddWorldEditKind("remove_resource", "Remove resource — ID required");
        AddWorldEditKind("place_object", "Place object — ID, value=kind, x/y required");
        AddWorldEditKind("remove_object", "Remove object — ID required");
        AddWorldEditKind("place_building", "Place building — ID, value=building kind, x/y required");
        AddWorldEditKind("remove_building", "Remove building — ID required");
        AddWorldEditKind("place_plant", "Place plant — ID, value=plant kind, x/y required");
        AddWorldEditKind("remove_plant", "Remove plant — ID required");
        AddWorldEditKind("create_founder_draft", "Create founder draft — ID, value=display name, x/y required");
        AddWorldEditKind("remove_founder_draft", "Remove founder draft — ID required");
        AddWorldEditKind("set_weather", "Set weather — value required");
        AddWorldEditKind("set_season", "Set season — value: spring, summer, autumn, or winter");
        AddWorldEditKind("set_weather_season", "Set weather + season — value=weather, secondary value=season");
        AddWorldEditKind("add_approved_asset_reference", "Add approved asset reference — ID and exact lowercase sha256 digest must already be in the world server's asset catalog");
        AddWorldEditKind("remove_approved_asset_reference", "Remove approved asset reference — ID required");
    }

    private void AddWorldEditKind(string kind, string description)
    {
        worldEditKind.AddItem(kind);
        worldEditKind.SetItemMetadata(worldEditKind.ItemCount - 1, description);
    }

    private void UpdateWorldEditHint()
    {
        if (worldEditKind.ItemCount == 0)
        {
            return;
        }

        worldEditHintLabel.Text = worldEditAdvanced.ButtonPressed
            ? worldEditKind.GetItemMetadata(worldEditKind.Selected).AsString()
            : PickedWorldEditKind is not { } kind ? "This server has no world-edit choices available."
            : $"{kind.DisplayName}: {worldEditTiles.Count}/64 tiles in the preview. Click or drag across tiles, then Apply; the server checks the whole edit before saving it.";
    }

    private static void ConfigureCoordinate(SpinBox box, string placeholder)
    {
        box.MinValue = 0;
        box.MaxValue = 0;
        box.Step = 1;
        box.CustomMinimumSize = new Vector2(72, 0);
        box.TooltipText = placeholder;
    }

    private string SelectedWorldEditKind() => worldEditKind.GetItemText(worldEditKind.Selected);
}
