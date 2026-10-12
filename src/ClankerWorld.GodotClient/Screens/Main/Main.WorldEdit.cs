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
    private void BuildDeveloperWorldEdit()
    {
        var worldEditBody = new VBoxContainer();
        worldEditKind.ItemSelected += _ => UpdateWorldEditHint();
        AddWorldEditKinds();
        worldEditBody.AddChild(worldEditKind);
        worldEditId.PlaceholderText = "ID (resource/object/draft/asset as required)";
        worldEditBody.AddChild(worldEditId);
        worldEditValue.PlaceholderText = "Value (terrain, kind, name, weather, digest...)";
        worldEditBody.AddChild(worldEditValue);
        worldEditSecondaryValue.PlaceholderText = "Secondary value (season for set_weather_season)";
        worldEditBody.AddChild(worldEditSecondaryValue);
        var coordinateRow = new HBoxContainer();
        ConfigureCoordinate(worldEditX, "x");
        ConfigureCoordinate(worldEditY, "y");
        coordinateRow.AddChild(worldEditX);
        coordinateRow.AddChild(worldEditY);
        worldEditRenewable.Text = "renewable resource";
        coordinateRow.AddChild(worldEditRenewable);
        worldEditBody.AddChild(coordinateRow);
        worldEditHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        worldEditBody.AddChild(worldEditHintLabel);
        submitWorldEditButton.Text = "Apply one paused authoring operation";
        submitWorldEditButton.Pressed += () => _ = SubmitWorldEditAsync();
        worldEditBody.AddChild(submitWorldEditButton);
        developerBody.AddChild(NewPanel("Paused authoring · server validates atomically", worldEditBody));
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
        AddWorldEditKind("add_approved_asset_reference", "Add approved asset reference — ID and exact lowercase sha256 digest must already exist in the host catalog");
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

        worldEditHintLabel.Text = worldEditKind.GetItemMetadata(worldEditKind.Selected).AsString();
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
