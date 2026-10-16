using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly PanelContainer apiKeysPanel = new();
    private readonly OptionButton apiKeyProviderChoice = new();
    private readonly LineEdit apiKeyLabelInput = new();
    private readonly LineEdit apiKeyInput = new();
    private readonly Button saveApiKeyButton = new();
    private readonly Label apiKeysStatus = new();
    private readonly FittedLabel savedApiKeys = new();
    private readonly OptionButton savedApiKeyChoice = new();
    private readonly Button deleteApiKeyButton = new();
    private int apiKeysRead;

    private void BuildApiKeysPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 7);
        body.AddChild(new Label
        {
            Text = "Save a key once, then choose it for an agent or the routine helper.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        apiKeyProviderChoice.AddItem("OpenAI");
        apiKeyProviderChoice.SetItemMetadata(0, "openai");
        apiKeyProviderChoice.AddItem("Ollama Cloud");
        apiKeyProviderChoice.SetItemMetadata(1, "ollama-cloud");
        apiKeyProviderChoice.AddItem("Anthropic");
        apiKeyProviderChoice.SetItemMetadata(2, "anthropic");
        apiKeyProviderChoice.AddItem("TypeSafe (Jev)");
        apiKeyProviderChoice.SetItemMetadata(3, "jev");
        apiKeyProviderChoice.ItemSelected += _ => apiKeyInput.Text = string.Empty;
        body.AddChild(apiKeyProviderChoice);
        apiKeyLabelInput.PlaceholderText = "Name this key (for example, Personal account)";
        body.AddChild(apiKeyLabelInput);
        apiKeyInput.Secret = true;
        apiKeyInput.PlaceholderText = "Paste API key";
        body.AddChild(apiKeyInput);
        saveApiKeyButton.Text = "Save API key";
        StyleButton(saveApiKeyButton);
        saveApiKeyButton.Pressed += () => _ = SaveApiKeyAsync();
        body.AddChild(saveApiKeyButton);
        apiKeysStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        apiKeysStatus.Text = "Connect and pair this device to save keys.";
        body.AddChild(apiKeysStatus);
        savedApiKeys.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // Shown once the host has said which keys are saved, so it leaves no blank line before then.
        savedApiKeys.Hide();
        body.AddChild(savedApiKeys);
        savedApiKeyChoice.FitToLongestItem = false;
        savedApiKeyChoice.ClipText = true;
        body.AddChild(savedApiKeyChoice);
        deleteApiKeyButton.Text = "Delete saved key";
        deleteApiKeyButton.TooltipText = "Choose a saved key to delete after its agents and routine helper stop using it.";
        StyleButton(deleteApiKeyButton);
        deleteApiKeyButton.Pressed += () => _ = DeleteApiKeyAsync();
        body.AddChild(deleteApiKeyButton);
        savedApiKeyChoice.Hide();
        deleteApiKeyButton.Hide();
        AddPanelContents(apiKeysPanel, "API keys", body);
        apiKeysPanel.ThemeTypeVariation = "InsetPanel";
        apiKeysPanel.VisibilityChanged += () =>
        {
            if (apiKeysPanel.IsVisibleInTree()) return;
            apiKeyInput.Text = string.Empty;
            apiKeysRead++;
        };
        gameSettingsContent.AddChild(apiKeysPanel);
    }

    private async Task RefreshApiKeysAsync()
    {
        RefreshControlAvailability();
        var read = ++apiKeysRead;
        var owner = registration;
        var generation = observationSession.RequestGeneration;
        savedApiKeys.Text = string.Empty;
        savedApiKeys.Hide();
        savedApiKeyChoice.Hide();
        deleteApiKeyButton.Hide();
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            apiKeysStatus.Text = "Connect and pair this device to save keys.";
            return;
        }
        apiKeysStatus.Text = "Checking saved keys...";
        try
        {
            var status = await AwaitCurrentWorldResultAsync(ownerApi.GetProviderStatusAsync(
                ResolveWorldUri(), authority, deviceId, signer, OwnerRequestToken));
            if (!IsCurrentWorldRequest(generation) || read != apiKeysRead ||
                !ReferenceEquals(owner, registration) || !apiKeysPanel.IsVisibleInTree()) return;
            providerConfiguration = status;
            RefreshRoutineHelperKeys(SelectedRoutineHelperKey());
            RenderSavedApiKeys();
            apiKeysStatus.Text = "Keys stay private on the world server.";
        }
        catch (Exception exception)
        {
            if (IsCurrentWorldRequest(generation) && read == apiKeysRead &&
                ReferenceEquals(owner, registration) && apiKeysPanel.IsVisibleInTree())
                apiKeysStatus.Text = "Could not check saved keys: " + FriendlyFailure(exception);
        }
    }

    private void RenderSavedApiKeys()
    {
        var selectedId = savedApiKeyChoice.Selected < 0 ? null : savedApiKeyChoice.GetItemMetadata(savedApiKeyChoice.Selected).AsString();
        savedApiKeyChoice.Clear();
        foreach (var slot in providerConfiguration?.CredentialSlots ?? [])
        {
            savedApiKeyChoice.AddItem($"{CredentialProviderDisplayName(slot.Provider)} · {slot.Label}");
            var index = savedApiKeyChoice.ItemCount - 1;
            savedApiKeyChoice.SetItemMetadata(index, slot.Id);
            if (slot.Id == selectedId) savedApiKeyChoice.Select(index);
        }
        savedApiKeyChoice.Visible = savedApiKeyChoice.ItemCount > 0;
        deleteApiKeyButton.Visible = savedApiKeyChoice.Visible;
        savedApiKeys.Text = providerConfiguration?.CredentialSlots is { Count: > 0 } slots
            ? "Saved keys:\n" + string.Join('\n', slots.Select(slot => $"{CredentialProviderDisplayName(slot.Provider)} · {slot.Label}"))
            : "No named keys saved yet.";
        savedApiKeys.Show();
    }

    private static string CredentialProviderDisplayName(string provider) => provider == "jev" ? "TypeSafe" : ProviderDisplayName(provider);

    private async Task DeleteApiKeyAsync()
    {
        if (isOwnerAction || savedApiKeyChoice.Selected < 0) return;
        var read = ++apiKeysRead;
        var owner = registration;
        var generation = observationSession.RequestGeneration;
        await DeleteCredentialSlotAsync(savedApiKeyChoice.GetItemMetadata(savedApiKeyChoice.Selected).AsString());
        if (IsCurrentWorldRequest(generation) && read == apiKeysRead &&
            ReferenceEquals(owner, registration) && apiKeysPanel.IsVisibleInTree())
            apiKeysStatus.Text = statusLabel.Text;
    }

    private async Task SaveApiKeyAsync()
    {
        if (isOwnerAction || !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var label = apiKeyLabelInput.Text.Trim();
        if (label.Length is < 1 or > 64 || label.Any(char.IsControl) || string.IsNullOrWhiteSpace(apiKeyInput.Text))
        {
            apiKeysStatus.Text = "Give the key a name of 64 characters or fewer and paste the key.";
            return;
        }
        apiKeysRead++;
        var action = new OwnerCredentialSlotCreationAction(Guid.NewGuid().ToString("N"),
            apiKeyProviderChoice.GetItemMetadata(apiKeyProviderChoice.Selected).AsString(), label, apiKeyInput.Text);
        apiKeyInput.Text = string.Empty;
        apiKeysStatus.Text = "Saving key...";
        var saved = false;
        var generation = observationSession.RequestGeneration;
        var contextRetained = await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await AwaitCurrentWorldResultAsync(ownerApi.CreateCredentialSlotAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, OwnerRequestToken));
            saved = true;
            RefreshRoutineHelperKeys(SelectedRoutineHelperKey());
            RenderSavedApiKeys();
            apiKeyLabelInput.Text = string.Empty;
            apiKeysStatus.Text = $"Saved {label}. Choose it for an agent or the routine helper.";
            PopulateFounderCredentials();
            PopulateCredentialChoices();
            return "API key saved on the world server.";
        });
        if (contextRetained && !saved && IsCurrentWorldRequest(generation))
            apiKeysStatus.Text = "The world server did not confirm that the key was saved. Check the connection and try again.";
    }
}
