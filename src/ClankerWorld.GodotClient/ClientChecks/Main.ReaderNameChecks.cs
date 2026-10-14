using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyLongReaderNamesAsync()
    {
        var window = GetWindow();
        var previousSize = window.Size;
        var previousRenderSize = window.ContentScaleSize;
        var previousMap = renderedMapSnapshot;
        var previousSelection = selectedAgentId;
        var previousProfileRequested = agentProfileRequested;
        var previousMainMenuVisible = mainMenuOverlay.Visible;
        var previousInWorld = isInWorld;
        var previousZoom = cameraZoom;
        var previousCenter = cameraCenterTiles;
        async Task SettleAsync()
        {
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ApplyResponsiveLayout();
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        try
        {
            mainMenuOverlay.Hide();
            isInWorld = true;
            foreach (var (name, otherName) in new[]
                     { ("Rowan Lake", "Mira Vale"), (new string('界', 48), new string('森', 48)), (new string('W', 48), new string('V', 48)) })
                foreach (var (width, height, factor) in new[] { (1280, 720, 1), (1920, 1080, 2), (3840, 2160, 3) })
                {
                    window.Size = new(width, height);
                    window.ContentScaleSize = new(width, height);
                    ApplyUiScale();
                    var person = PanelSmokeAgent("reader-name-a", name, new(1, 1)) with
                    {
                        RecentPrivateThoughts = [new(1, "I will rest here.")],
                    };
                    var other = PanelSmokeAgent("reader-name-b", otherName, new(2, 1));
                    var third = PanelSmokeAgent("reader-name-c", "Aster Ash", new(1, 2));
                    var snapshot = new OwnerWorldSnapshot("reader-name-world", 1, "reader-name-map",
                        Enumerable.Range(0, 36).Select(i => new OwnerWorldTile(i % 6, i / 6, "meadow")).ToArray(), [], [], null, 0)
                    {
                        PackedTerrain = new(6, 6, "terrain-kind-v1", Convert.ToBase64String(new byte[36])),
                        Agents = [person, other, third],
                        Instructions = [new("reader-name-order", person.Id, "order", "Eat berries.", "queued", 1, 0, 1,
                            Task: new("consume_food", "queued", 1, 0, "food_items", false))],
                        Conversations = [new("reader-name-conversation", person.Id, name, other.Id, otherName,
                            "closed", null, "agreed", 0, 1, [new("reader-name-turn", person.Id, name, "Hello.", 1, [other.Id, third.Id], false)])],
                    };
                    RenderMap(snapshot);
                    selectedAgentId = person.Id;
                    agentProfileRequested = true;
                    RenderSelectedAgentCard(snapshot);
                    await SettleAsync();
                    if (uiLayer.Factor != factor)
                        throw new InvalidOperationException("Reader name checks must use the window's automatic UI scale.");
                    async Task CheckAsync(PanelContainer panel, Label title, string expected)
                    {
                        await SettleAsync();
                        var close = panel.FindChildren("*", nameof(Button), recursive: true, owned: false).OfType<Button>()
                            .Single(button => button.TooltipText.StartsWith("Close", StringComparison.Ordinal));
                        var viewport = GetViewportRect().Grow(1);
                        if (!panel.IsVisibleInTree() || title is not ClankerWorld.GodotClient.UI.FittedLabel fitted || fitted.FullText != expected || title.TooltipText != expected ||
                            !viewport.Encloses(panel.GetGlobalRect()) || !viewport.Encloses(close.GetGlobalRect()) ||
                            panel.GetGlobalRect().Size.X > (ReaderWidth + 1) * factor)
                            throw new InvalidOperationException($"Reader and Close must fit and retain the complete {name.Length}-character heading at {window.Size}: title={title.Text}, expected={expected}, panel={panel.GetGlobalRect()}, close={close.GetGlobalRect()}.");
                        close.GrabFocus();
                        if (!close.HasFocus()) throw new InvalidOperationException("Reader Close must remain reachable by keyboard.");
                        close.EmitSignal(BaseButton.SignalName.Pressed);
                        if (panel.Visible) throw new InvalidOperationException("The actual reader Close action must hide its panel.");
                    }
                    readThoughtsButton.EmitSignal(BaseButton.SignalName.Pressed);
                    if (!thoughtsReaderText.GetParsedText().Contains("I will rest here.", StringComparison.Ordinal))
                        throw new InvalidOperationException("Wrapping the heading must retain the thought text.");
                    await CheckAsync(thoughtsPanel, thoughtsReaderTitle, $"{name} · thoughts");
                    allOrdersButton.EmitSignal(BaseButton.SignalName.Pressed);
                    if (!ordersReaderText.GetParsedText().Contains("You said: “Eat berries.”", StringComparison.Ordinal))
                        throw new InvalidOperationException("Wrapping the heading must retain the order text.");
                    await CheckAsync(ordersPanel, ordersReaderTitle, $"{name} · orders");
                    foreach (var kind in new[] { "ordinary", "marriage_surname", "nearby" })
                    {
                        var conversation = snapshot.Conversations[0] with { Kind = kind == "nearby" ? "ordinary" : kind };
                        var displayed = snapshot with { Conversations = [conversation] };
                        var viewer = kind == "nearby" ? third.Id : person.Id;
                        OpenConversationReader(displayed, viewer, conversation.Id);
                        await SettleAsync();
                        RenderConversationReader(displayed, conversation, viewer, 0);
                        var heading = kind switch
                        {
                            "marriage_surname" => $"Shared surname · {name} and {otherName}",
                            "nearby" => $"Nearby conversation · {name} and {otherName}",
                            _ => $"{name} and {otherName}",
                        };
                        if (!conversationReaderSummary.Text.Contains("Hello.", StringComparison.Ordinal))
                            throw new InvalidOperationException("Wrapping the heading must retain the conversation text.");
                        await CheckAsync(conversationPanel, conversationReaderTitle, heading);
                    }
                    var historical = snapshot with { Agents = [person with { Lifecycle = "dead" }, other, third] };
                    RenderMap(historical);
                    RenderSelectedAgentCard(historical);
                    readThoughtsButton.EmitSignal(BaseButton.SignalName.Pressed);
                    await CheckAsync(thoughtsPanel, thoughtsReaderTitle, $"{name} · thoughts, historical");
                }
            GD.Print("Reader headings passed: short/CJK/Latin names at 100/200/300%, two participants, nearby and surname conversations, historical thoughts, complete tooltips and actual Close actions.");
        }
        finally
        {
            thoughtsPanel.Hide();
            ordersPanel.Hide();
            conversationPanel.Hide();
            selectedAgentId = previousSelection;
            agentProfileRequested = previousProfileRequested;
            window.Size = previousSize;
            window.ContentScaleSize = previousRenderSize;
            ApplyUiScale();
            cameraZoom = previousZoom;
            cameraCenterTiles = previousCenter;
            if (previousMap is not null)
            {
                RenderMap(previousMap);
                RenderSelectedAgentCard(previousMap);
            }
            mainMenuOverlay.Visible = previousMainMenuVisible;
            isInWorld = previousInWorld;
            ApplyResponsiveLayout();
        }
    }
}
