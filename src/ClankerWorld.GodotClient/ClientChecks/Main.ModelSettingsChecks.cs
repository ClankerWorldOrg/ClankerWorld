using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyHelperKeyDeletionRefusalAsync(OwnerWorldSnapshot sample)
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousEndpointInvalid = registeredEndpointInvalid;
        var previousObservation = observationSession.Current;
        var previousConfiguration = providerConfiguration;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        try
        {
            const string slotId = "1234567890abcdef1234567890abcdef";
            registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
            deviceKey = signer;
            registeredEndpointInvalid = false;
            observationSession.ReplaceRegistration(registration);
            var snapshot = sample with
            {
                WorldId = host.Authority.WorldId,
                LatestEventId = 0,
                RoutineHelperProvider = "decisions",
                RoutineHelperModel = "gpt-6-luna",
                RoutineHelperCredentialSlotId = slotId,
            };
            var handshake = new OwnerWorldHandshake(new(1, 1),
                ["owner-observation.read.v1", "owner-event-presentation.v1", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                 "owner-control.request.v1", "paused-authoring.request.v1"], []);
            if (!observationSession.TryAccept(new(handshake, new(snapshot, new(0, 0, []))), 0, out var failure))
                throw new InvalidOperationException("Helper key observation refused: " + failure);
            providerConfiguration = new("deterministic", "deterministic", 0, [], Assignments: [],
                CredentialSlots: [new(slotId, "openai", "Helper")]);
            agentModelCredentialChoice.Clear();
            agentModelCredentialChoice.AddItem("Helper");
            agentModelCredentialChoice.SetItemMetadata(0, slotId);
            agentModelCredentialChoice.Select(0);
            await DeleteCredentialSlotAsync();
            if (!statusLabel.Text.StartsWith("The world's routine helper is using this key.", StringComparison.Ordinal) ||
                providerConfiguration.CredentialSlots?.Count != 1 || isOwnerAction)
                throw new InvalidOperationException("Deleting a helper-only key must explain how to release it and keep the saved key.");
        }
        finally
        {
            registration = previousRegistration;
            deviceKey = previousKey;
            registeredEndpointInvalid = previousEndpointInvalid;
            providerConfiguration = previousConfiguration;
            observationSession.ReplaceRegistration(previousRegistration);
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            PopulateCredentialChoices();
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
    }

    private static readonly string[] ModelFieldCaptions = ["Provider", "API key", "Model", "Thinking"];
    private static readonly string[] ThinkingLevels = ["Model default", "Low", "Medium", "High"];

    /// <summary>
    /// Model choices are captioned Provider, API key, Model and Thinking in
    /// both the Agent model box and Add an agent, with the same thinking levels. On an agent's own page the world's
    /// summary, Refresh and Delete this key give way to a link to the model
    /// settings, and Add an agent says plainly to click on land.
    /// </summary>
    private void VerifyModelSettingsLayout()
    {
        static string[] Captions(Control panel) => panel.FindChildren("*", nameof(Label), recursive: true, owned: false)
            .OfType<Label>().Where(label => label.ThemeTypeVariation == "DimLabel").Select(label => label.Text).ToArray();
        foreach (var (name, panel) in new (string, Control)[] { ("Agent model", agentModelPanel), ("Add an agent", founderSetupPanel) })
        {
            var captions = Captions(panel);
            if (!ModelFieldCaptions.All(captions.Contains) ||
                panel.FindChildren("*", nameof(Label), recursive: true, owned: false).OfType<Label>().Any(label => label.Text.Contains("Who decides", StringComparison.Ordinal)))
                throw new InvalidOperationException($"{name} must caption its fields Provider, API key, Model and Thinking: {string.Join(", ", captions)}");
        }
        foreach (var choice in new[] { agentModelThinkingChoice, founderThinkingChoice })
        {
            var levels = Enumerable.Range(0, choice.ItemCount).Select(index => choice.GetItemText(index)).ToArray();
            if (!levels.SequenceEqual(ThinkingLevels) || !choice.ClipText)
                throw new InvalidOperationException($"Thinking must offer {string.Join(", ", ThinkingLevels)}: {string.Join(", ", levels)}");
        }
        if (!agentModelProviderChoice.ClipText || !agentModelCredentialChoice.ClipText || !agentModelRoleChoice.ClipText ||
            deleteAgentModelCredentialSlotButton.GetParent() is not HFlowContainer)
            throw new InvalidOperationException("Long model choices must be cut short and the Agent model buttons must wrap, so the box fits an agent's Profile.");

        var wasAgentPage = selectedAgentModelScroll.Visible;
        try
        {
            selectedAgentModelScroll.Show();
            RenderProviderConfiguration();
            if (refreshAgentModelProviderButton.Visible || deleteAgentModelCredentialSlotButton.Visible || !openModelSettingsButton.Visible ||
                openModelSettingsButton.Text != "Open model settings")
                throw new InvalidOperationException("An agent's model page must offer Open model settings instead of Refresh and Delete this key.");
            selectedAgentModelScroll.Hide();
            RenderProviderConfiguration();
            if (!refreshAgentModelProviderButton.Visible || openModelSettingsButton.Visible)
                throw new InvalidOperationException("The world's Agent model box keeps Refresh and has no link to itself.");
        }
        finally
        {
            selectedAgentModelScroll.Visible = wasAgentPage;
            RenderProviderConfiguration();
        }

        var hint = founderSetupHint.Text;
        ResetAddAgentPlacementHint();
        var reset = founderSetupHint.Text;
        founderSetupHint.Text = hint;
        if (reset != "Click on land to place them. Point first to see which household and Town they would join." ||
            founderPlacementIcon.Texture != PixelIcons.Texture(PixelGlyph.Mouse, UiTheme.Current.Ink, UiTheme.Current.Primary, 1))
            throw new InvalidOperationException($"Add an agent must say to click on land, beside a mouse icon: {reset}");
    }
}
