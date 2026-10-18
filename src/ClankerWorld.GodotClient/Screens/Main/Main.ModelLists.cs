using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Button routineHelperSetupCheckButton = new();
    private readonly Label routineHelperSetupCheckStatus = new();
    private int routineHelperSetupCheckRevision;

    private const double PastedKeyPause = 0.8;
    private int founderKeyEdits;
    private int agentModelKeyEdits;
    private string? agentModelContext;
    private string? agentModelLookup;

    private static bool HasModelList(string provider) => IsModelProvider(provider) || provider is "jev" or "decisions";

    private const string PasteKeyNote = "Paste the key to check which of these it can use.";

    /// <summary>
    /// Fills a model picker with the game's list from the host. When a key is
    /// known, the host checks it with the provider: the saved key, a named key
    /// slot, or a key that was just pasted and not saved. Keys never come back
    /// to this device. Without a key yet, the list is shown unchecked.
    /// </summary>
    private async Task LoadModelListAsync(ModelPicker picker, string provider, string? credentialSlotId, string? apiKey,
        bool checkKey = true)
    {
        var generation = observationSession.RequestGeneration;
        var defaultModel = DefaultProviderModel(provider);
        if (!HasModelList(provider))
        {
            picker.ShowTypedOnly(picker.Model.Length > 0 ? picker.Model : defaultModel);
            return;
        }
        var lookup = picker.BeginLoading(defaultModel);
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            picker.ShowError("Connect this device to see the models.", defaultModel, canRetry: false);
            return;
        }
        try
        {
            var list = await ownerApi.ListProviderModelsAsync(ResolveWorldUri(), authority, deviceId,
                new OwnerProviderModelListAction(provider, credentialSlotId, apiKey, checkKey), signer, OwnerRequestToken);
            if (!IsCurrentWorldRequest(generation) || !picker.IsLatest(lookup)) return;
            if (checkKey) picker.ShowList(list.Models, list.DefaultModel, list.Error);
            else picker.ShowList(list.Models, list.DefaultModel, PasteKeyNote, canRetry: false);
        }
        catch (ClankerWorld.GodotClient.Pairing.OwnerActionCompatibilityException exception)
        {
            if (IsCurrentWorldRequest(generation) && picker.IsLatest(lookup))
                picker.ShowError(FriendlyFailure(exception), defaultModel, canRetry: false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (IsCurrentWorldRequest(generation) && picker.IsLatest(lookup))
                picker.ShowError("Couldn't reach the world server for the model list. Type a model name or try again.", defaultModel);
        }
    }

    private void RequestFounderModels()
    {
        if (!founderSetupPanel.Visible) return;
        var provider = SelectedFounderProvider();
        var choice = SelectedFounderCredential();
        if (choice == "new")
        {
            var pasted = string.IsNullOrWhiteSpace(founderApiKeyInput.Text) ? null : founderApiKeyInput.Text;
            _ = LoadModelListAsync(founderModelPicker, provider, null, pasted, checkKey: pasted is not null);
            return;
        }
        _ = LoadModelListAsync(founderModelPicker, provider, choice == "default" ? null : choice, null);
    }

    /// <summary>
    /// Keeps the Model settings picker in step with the chosen agent, provider
    /// and key. The shown model is reset only when the agent or provider
    /// changes, so switching keys keeps the owner's pick.
    /// </summary>
    private void SyncAgentModelPicker(bool force = false)
    {
        var provider = SelectedProviderId();
        if (provider is "deterministic" or "inherit" || providerConfiguration is null) return;
        var option = providerConfiguration.Providers.FirstOrDefault(item => item.Provider == provider);
        var assigned = SelectedAssignment() is { } assignment && assignment.Provider == provider
            ? assignment.Model ?? option?.Model : option?.Model;
        var model = assigned ?? DefaultProviderModel(provider);
        var context = $"{SelectedAgentModelTarget()}|{SelectedRoleId()}|{provider}|{model}";
        if (context != agentModelContext)
        {
            agentModelContext = context;
            agentModelPicker.SetModel(model);
            force = true;
        }
        if (!agentModelPanel.IsVisibleInTree()) return;

        var agentCredential = SelectedAgentModelTarget() is not null && HasModelList(provider);
        var credential = agentCredential ? SelectedCredentialChoice() : null;
        var pasted = string.IsNullOrWhiteSpace(agentModelApiKeyInput.Text) ? null : agentModelApiKeyInput.Text;
        var lookup = $"{provider}|{credential}|{pasted?.GetHashCode(StringComparison.Ordinal)}";
        if (!force && lookup == agentModelLookup) return;
        agentModelLookup = lookup;
        var slot = credential is null or "default" or "new" ? null : credential;
        var usePasted = credential == "new" || !agentCredential;
        _ = LoadModelListAsync(agentModelPicker, provider, slot, usePasted ? pasted : null,
            checkKey: credential != "new" || pasted is not null);
    }

    // A pasted key is looked up once the owner stops typing, not per keystroke.
    private void OnFounderKeyEdited()
    {
        ClearFounderModelSetupCheck();
        var stamp = ++founderKeyEdits;
        GetTree().CreateTimer(PastedKeyPause).Timeout += () =>
        {
            if (stamp == founderKeyEdits) RequestFounderModels();
        };
    }

    private void OnAgentModelKeyEdited()
    {
        ClearAgentModelSetupCheck();
        var stamp = ++agentModelKeyEdits;
        GetTree().CreateTimer(PastedKeyPause).Timeout += () =>
        {
            if (stamp == agentModelKeyEdits) SyncAgentModelPicker();
        };
    }
    private string SelectedRoutineHelper() => routineHelperChoice.Selected switch { 0 => "off", 2 => "decisions", _ => "jev" };

    private void BuildRoutineHelperSettings()
    {
        routineHelperChoice.AddItem("Off");
        routineHelperChoice.AddItem("Jev");
        routineHelperChoice.AddItem("OpenAI Decisions");
        routineHelperChoice.Select(1);
        routineHelperChoice.TooltipText = "Choose an optional helper for small everyday actions and memory scoring.";
        routineHelperChoice.ItemSelected += _ =>
        {
            ClearRoutineHelperSetupCheck();
            var provider = SelectedRoutineHelper();
            routineHelperModelPicker.SetModel(DefaultProviderModel(provider));
            RefreshRoutineHelperKeys();
            RequestRoutineHelperModels();
            RefreshControlAvailability();
        };
        routineHelperKeyChoice.FitToLongestItem = false;
        routineHelperKeyChoice.ClipText = true;
        routineHelperKeyChoice.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        routineHelperKeyChoice.TooltipText = "Choose a saved TypeSafe key for Jev or an OpenAI key for Decisions.";
        routineHelperKeyChoice.ItemSelected += _ => { ClearRoutineHelperSetupCheck(); RequestRoutineHelperModels(); RefreshControlAvailability(); };
        routineHelperModelPicker.ModelChanged += () => { ClearRoutineHelperSetupCheck(); RefreshControlAvailability(); };
        routineHelperModelPicker.RetryRequested += RequestRoutineHelperModels;
        applyRoutineHelperButton.Text = "Apply helper";
        StyleButton(applyRoutineHelperButton);
        applyRoutineHelperButton.Pressed += () => _ = SaveRoutineHelperAsync();
        routineHelperSetupCheckButton.Text = "Test helper · 1 paid call";
        routineHelperSetupCheckButton.TooltipText = "Tests the selected helper, model and saved key. It counts toward your model-call limit.";
        StyleButton(routineHelperSetupCheckButton);
        routineHelperSetupCheckButton.Pressed += () => _ = RunRoutineHelperSetupCheckAsync();
        routineHelperSetupCheckStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        routineHelperSetupCheckStatus.Hide();
        var hint = new Label
        {
            Text = "Adults and elders use the helper only when all available actions concern food, clothing, warmth, care or waiting. Planning, naming, guidance and children's choices use personal models.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        worldSettingsContent.AddChild(SettingsBox("Routine helper", routineHelperChoice, routineHelperKeyChoice, routineHelperModelPicker, applyRoutineHelperButton, routineHelperSetupCheckButton, routineHelperSetupCheckStatus, hint));
    }

    private void RequestRoutineHelperModels()
    {
        var provider = SelectedRoutineHelper();
        routineHelperSetupCheckButton.Visible = provider != "off";
        routineHelperModelPicker.Visible = provider != "off";
        routineHelperKeyChoice.Visible = provider != "off";
        if (provider == "off")
        {
            _ = routineHelperModelPicker.BeginLoading(string.Empty); // Invalidate a previous provider's lookup.
            return;
        }
        _ = LoadModelListAsync(routineHelperModelPicker, provider, SelectedRoutineHelperKey(), null);
    }

    private void RenderRoutineHelperSettings(OwnerWorldSnapshot snapshot)
    {
        var provider = snapshot.RoutineHelperProvider ?? (snapshot.JevEnabled == true ? "jev" : "off");
        var model = snapshot.RoutineHelperModel ?? DefaultProviderModel(provider);
        var context = $"{snapshot.WorldId}|{observationSession.RequestGeneration}|{provider}|{model}|{snapshot.RoutineHelperCredentialSlotId}";
        if (context == observedRoutineHelperContext) return;
        observedRoutineHelperContext = context;
        ClearRoutineHelperSetupCheck();
        routineHelperChoice.Select(provider switch { "off" => 0, "decisions" => 2, _ => 1 });
        routineHelperModelPicker.SetModel(model);
        RefreshRoutineHelperKeys(snapshot.RoutineHelperCredentialSlotId);
        RequestRoutineHelperModels();
    }

    private async Task RunRoutineHelperSetupCheckAsync()
    {
        var provider = SelectedRoutineHelper();
        var model = routineHelperModelPicker.Model;
        if (provider == "off" || model.Length is < 1 or > 128 || model.Any(char.IsControl)) return;
        await RunModelSetupCheckAsync(routineHelperSetupCheckButton, routineHelperSetupCheckStatus,
            provider, model, new(provider, model, SelectedRoutineHelperKey()), () => routineHelperSetupCheckRevision);
    }

    private void ClearRoutineHelperSetupCheck()
    {
        ++routineHelperSetupCheckRevision;
        routineHelperSetupCheckStatus.Text = string.Empty;
        routineHelperSetupCheckStatus.Hide();
    }

    private string? SelectedRoutineHelperKey()
    {
        if (routineHelperKeyChoice.Selected < 0) return null;
        var id = routineHelperKeyChoice.GetItemMetadata(routineHelperKeyChoice.Selected).AsString();
        return id.Length == 0 ? null : id;
    }

    private void RefreshRoutineHelperKeys(string? selectedSlotId = null)
    {
        routineHelperKeyChoice.Clear();
        var provider = SelectedRoutineHelper() == "decisions" ? "openai" : "jev";
        routineHelperKeyChoice.AddItem($"Default {CredentialProviderDisplayName(provider)} key");
        routineHelperKeyChoice.SetItemMetadata(0, "");
        foreach (var slot in providerConfiguration?.CredentialSlots ?? [])
        {
            if (slot.Provider != provider) continue;
            routineHelperKeyChoice.AddItem(slot.Label);
            routineHelperKeyChoice.SetItemMetadata(routineHelperKeyChoice.ItemCount - 1, slot.Id);
        }
        if (selectedSlotId is not null && !Enumerable.Range(0, routineHelperKeyChoice.ItemCount)
            .Any(index => routineHelperKeyChoice.GetItemMetadata(index).AsString() == selectedSlotId))
        {
            routineHelperKeyChoice.AddItem("Saved key unavailable — choose another");
            routineHelperKeyChoice.SetItemMetadata(routineHelperKeyChoice.ItemCount - 1, selectedSlotId);
        }
        for (var index = 0; index < routineHelperKeyChoice.ItemCount; index++)
            if (routineHelperKeyChoice.GetItemMetadata(index).AsString() == (selectedSlotId ?? "")) routineHelperKeyChoice.Select(index);
    }

    private async Task SaveRoutineHelperAsync()
    {
        if (observationSession.Current?.Baseline.Snapshot is not { } snapshot ||
            !TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var provider = SelectedRoutineHelper();
        var model = provider == "off" ? string.Empty : routineHelperModelPicker.Model;
        if (provider != "off" && (model.Length is < 1 or > 128 || model.Any(char.IsControl)))
        {
            SetStatus("Choose a model name of 128 characters or fewer.", good: false);
            return;
        }
        var action = new OwnerRoutineHelperAction(snapshot.WorldId, provider, model, provider == "off" ? null : SelectedRoutineHelperKey());
        await RunOwnerActionAsync(async () =>
        {
            _ = await AwaitCurrentWorldResultAsync(ownerApi.SetRoutineHelperAsync(ResolveWorldUri(), authority, deviceId,
                action, signer, OwnerRequestToken));
            return provider == "off" ? "Routine helper turned off for this world" : $"{ProviderDisplayName(provider)} selected for this world. Use Test helper to check the saved key and model.";
        });
    }
}
