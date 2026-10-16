using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyProfileHistoryLayoutAsync()
    {
        var window = GetWindow();
        var previousSize = window.Size;
        var previousRenderSize = window.ContentScaleSize;
        var previousSnapshot = agentCardSnapshot;
        var previousSelection = selectedAgentId;
        var previousRequested = agentProfileRequested;
        var previousMenu = mainMenuOverlay.Visible;
        var partner = PanelSmokeAgent("history-partner", "Élodie 🌲 River and the very long household name", new(1, 0));
        var adult = PanelSmokeAgent("history-adult", "Tobin Hale", new(0, 0)) with
        {
            DecisionFactors = [new("age-days", "15"), new("age-band", "adult"),
                new("personality", "Curious, patient and careful with tools."),
                new("last-model-choice", "gather"), new("last-model-activity", "bringing wood from the river bank to the household Store")],
            Relationships = [new("history-partnership", partner.Id, "partnership", "accepted", "family", 1),
                new("history-household", "history-house", "household_membership", "accepted", "family", 1)],
            SocialNotes = [.. Enumerable.Range(1, 24).Select(index =>
                $"Day {index}: Bought 1 of Store: 1 food for 1 tree. Brought the purchase home before returning to work by the river.")],
            RecentPrivateThoughts = [.. Enumerable.Range(1, 24).Select(index => new OwnerWorldPrivateThought(index,
                $"Day {index}: I should bring the wood home before dusk, then help Élodie 🌲 prepare the next harvest."))],
        };
        var snapshot = new OwnerWorldSnapshot("profile-history", 24_000, "profile-history-map", [new(0, 0, "meadow")], [], [], null, 0)
        {
            Agents = [adult, partner],
            Instructions = [.. Enumerable.Range(1, 12).Select(index => new OwnerWorldInstruction($"history-order-{index}", adult.Id,
                "order", $"Carry wood from the river bank to the household Store before dusk, trip {index}.", "completed", 0, 0, index, index,
                "I brought the wood home and stored it.", Task: new("gather", "finished", 1, 1, "items", false)))],
        };
        async Task Settle()
        {
            for (var frame = 0; frame < 12; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void CheckLayout()
        {
            var content = selectedAgentOverview.GetGlobalRect();
            var notes = profilePeople.GetChildren().OfType<Label>().ToArray();
            if (notes.Length != adult.SocialNotes.Count || !notes.Any(label => label.Text.Contains("Bought 1 of Store", StringComparison.Ordinal)))
                throw new InvalidOperationException("The history check must render all recent trades in the real Profile.");
            foreach (var label in notes)
                if (label.Size.X < selectedAgentOverview.Size.X - 1 || label.GetGlobalRect().Position.X < content.Position.X - 1 ||
                    label.GetGlobalRect().End.X > content.End.X + 1 || label.GetLineCount() < 2)
                    throw new InvalidOperationException($"Every history line must wrap at the Profile content width: label={label.GetGlobalRect()}, content={content}.");
            void Stacked(Control parent)
            {
                var children = parent.GetChildren().OfType<Control>().Where(child => child.Visible).ToArray();
                for (var index = 1; index < children.Length; index++)
                    if (children[index - 1].GetGlobalRect().End.Y > children[index].GetGlobalRect().Position.Y + 1)
                        throw new InvalidOperationException($"Profile sections overlap in {parent.GetType().Name}: {children[index - 1].GetGlobalRect()} and {children[index].GetGlobalRect()}.");
            }
            Stacked(selectedAgentOverview);
            Stacked(profilePeople);
            Stacked(speakSection);
            foreach (var label in selectedAgentOverview.GetChildren().Concat(speakSection.GetChildren()).OfType<Control>()
                .Where(control => control.Visible && control is Label or RichTextLabel))
                if (label.Size.X < selectedAgentOverview.Size.X - 1 ||
                    label is RichTextLabel rich && rich.GetContentHeight() > rich.Size.Y + 1)
                    throw new InvalidOperationException($"Profile text must use the content width and reserve its entire wrapped height: {label.GetGlobalRect()}, content={content}.");
            if (profileOrderLabel.GetParent() != selectedAgentOverview || profileActivityLabel.GetParent() != selectedAgentOverview)
                throw new InvalidOperationException("Activity and orders must scroll with the other Profile sections.");
            if (!GetViewportRect().Grow(1).Encloses(agentProfilePanel.GetGlobalRect()) ||
                agentOverviewScroll.GetVScrollBar().MaxValue <= agentOverviewScroll.GetVScrollBar().Page)
                throw new InvalidOperationException("A long Profile must stay in the window and scroll all its history.");
        }
        try
        {
            mainMenuOverlay.Hide();
            selectedAgentId = adult.Id;
            agentProfileRequested = true;
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                window.Size = size;
                window.ContentScaleSize = size;
                await Settle();
                ApplyUiScale();
                RenderSelectedAgentCard(snapshot);
                await Settle();
                if (GetViewportRect().Size != size || uiLayer.Factor != (size.X == 1920 ? 2 : 1))
                    throw new InvalidOperationException($"The history check must use the requested window and automatic scale: requested={size}, viewport={GetViewportRect()}, factor={uiLayer.Factor}.");
                CheckLayout();
                if (System.Environment.GetEnvironmentVariable("CLANKERWORLD_PROFILE_HISTORY_EVIDENCE") is { Length: > 0 } evidence)
                {
                    agentOverviewScroll.EnsureControlVisible(profilePeople.GetChild<Control>(2));
                    await Settle();
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    Directory.CreateDirectory(evidence);
                    GetViewport().GetTexture().GetImage().SavePng(Path.Combine(evidence, $"profile-history-{size.X}.png"));
                }
                agentOverviewScroll.ScrollVertical = int.MaxValue;
                await Settle();
                CheckLayout();
                if (instructionHistory.GetGlobalRect().End.Y > agentOverviewScroll.GetGlobalRect().End.Y + 1)
                    throw new InvalidOperationException("Scrolling to the end must reach the final message without clipping its bottom.");
                agentOverviewScroll.EnsureControlVisible(submitInstructionButton);
                await Settle();
                if (!agentOverviewScroll.GetGlobalRect().Grow(1).Encloses(submitInstructionButton.GetGlobalRect()))
                    throw new InvalidOperationException("Speak and Send must remain reachable after a long history.");
                agentOverviewScroll.ScrollVertical = 0;
                RenderSelectedAgentCard(snapshot with { Agents = [adult with { SocialNotes = [] }, partner], Instructions = [] });
                await Settle();
                if (profilePeople.GetChildren().OfType<Label>().Any() || profilePeople.Size.Y >= 200)
                    throw new InvalidOperationException("Refreshing without trade history must remove its rows and shrink the People section.");
                RenderSelectedAgentCard(snapshot);
                await Settle();
                CheckLayout();
            }
        }
        finally
        {
            selectedAgentId = previousSelection;
            agentProfileRequested = previousRequested;
            if (previousSnapshot is not null) RenderSelectedAgentCard(previousSnapshot);
            else { agentCardSnapshot = null; agentProfilePanel.Hide(); }
            mainMenuOverlay.Visible = previousMenu;
            window.Size = previousSize;
            window.ContentScaleSize = previousRenderSize;
            ApplyUiScale();
        }
    }
}
