using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The Agent model settings: which model and key the world, a role or one
/// agent uses, in World Settings and in an agent's Profile.
/// </summary>
public partial class Main
{
    private async Task RefreshProviderConfigurationAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            agentModelConfigurationStatus.Text = "Connect this device before setting up agent models.";
            RenderProviderConfiguration();
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await AwaitCurrentWorldResultAsync(ownerApi.GetProviderStatusAsync(
                ResolveWorldUri(), authority, deviceId, signer, OwnerRequestToken));
            using var draft = new PanelDraft(agentModelTargetChoice.GetParent());
            RefreshRoutineHelperKeys(SelectedRoutineHelperKey());
            if (worldSettingsContent.Visible) RequestRoutineHelperModels();
            PopulateAgentModelTargets();
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            RenderProviderConfiguration();
            return "Agent model settings loaded.";
        });
    }

    private async Task SaveProviderConfigurationAsync()
    {
        var generation = observationSession.RequestGeneration;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("Connect this device before setting up agent models.", good: false);
            return;
        }

        var role = SelectedRoleId();
        var provider = SelectedProviderId();
        var target = SelectedAgentModelTarget();
        var agentUsesModel = target is not null && IsModelProvider(provider);
        var credentialChoice = agentUsesModel ? SelectedCredentialChoice() : null;
        var creatingSlot = credentialChoice == "new";
        if (creatingSlot && (string.IsNullOrWhiteSpace(agentModelCredentialLabelInput.Text) ||
            string.IsNullOrWhiteSpace(agentModelApiKeyInput.Text)))
        {
            SetStatus("Give the new key a name and paste the key.", good: false);
            return;
        }
        if (HasModelList(provider) && agentModelPicker.Model.Length == 0)
        {
            SetStatus("Pick a model first.", good: false);
            return;
        }
        var action = new OwnerProviderConfigurationAction(
            role,
            provider,
            provider is "deterministic" or "inherit" ? null : EmptyToNull(agentModelPicker.Model),
            provider is "deterministic" or "inherit" || agentUsesModel && !creatingSlot
                ? null : EmptyToNull(agentModelApiKeyInput.Text),
            ForgetCredential: false,
            AgentId: target,
            CredentialSlotId: creatingSlot ? Guid.NewGuid().ToString("N") : credentialChoice is null or "default" ? null : credentialChoice,
            NewCredentialLabel: creatingSlot ? EmptyToNull(agentModelCredentialLabelInput.Text) : null,
            Thinking: agentUsesModel ? SelectedThinking(agentModelThinkingChoice) : null);
        var contextRetained = false;
        try
        {
            contextRetained = await RunOwnerActionAsync(async () =>
            {
                providerConfiguration = await AwaitCurrentWorldResultAsync(ownerApi.ConfigureProviderAsync(
                    ResolveWorldUri(), authority, deviceId, action, signer, OwnerRequestToken));
                PopulateCredentialChoices();
                return $"{ProviderDisplayName(provider)} will handle {RoleDisplayName(role).ToLowerInvariant()} at the agent's next model choice";
            });
        }
        finally
        {
            if (contextRetained && IsCurrentWorldRequest(generation))
            {
                agentModelApiKeyInput.Text = string.Empty;
                agentModelCredentialLabelInput.Text = string.Empty;
                RenderProviderConfiguration();
            }
        }
    }

    private async Task ForgetProviderCredentialAsync()
    {
        var generation = observationSession.RequestGeneration;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("Connect this device before changing keys.", good: false);
            return;
        }

        var role = SelectedRoleId();
        var provider = SelectedProviderId();
        if (provider == "deterministic")
        {
            SetStatus("Built-in rules use no API key", good: false);
            return;
        }

        var action = new OwnerProviderConfigurationAction(
            role,
            provider,
            EmptyToNull(agentModelPicker.Model),
            null,
            ForgetCredential: true);
        var contextRetained = await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await AwaitCurrentWorldResultAsync(ownerApi.ConfigureProviderAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, OwnerRequestToken));
            return $"Forgot the saved {ProviderDisplayName(provider)} key.";
        });
        if (contextRetained && IsCurrentWorldRequest(generation))
        {
            agentModelApiKeyInput.Text = string.Empty;
            RenderProviderConfiguration();
        }
    }

    private async Task DeleteCredentialSlotAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            SetStatus("Connect this device before deleting a saved key.", good: false);
            return;
        }

        var slotId = SelectedCredentialChoice();
        var slot = providerConfiguration?.CredentialSlots?.FirstOrDefault(item => item.Id == slotId);
        if (slot is null)
        {
            SetStatus("Choose a saved key to delete.", good: false);
            return;
        }
        if (providerConfiguration?.Assignments?.Any(item =>
                item.CredentialSlotId == slotId && item.SelectionReason is null) == true)
        {
            SetStatus("An agent is still using this key. Give that agent another key first.", good: false);
            return;
        }
        if (observationSession.Current?.Baseline.Snapshot.RoutineHelperCredentialSlotId == slotId)
        {
            SetStatus("The world's routine helper is using this key. Choose another helper key or turn the helper off first.", good: false);
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            providerConfiguration = await AwaitCurrentWorldResultAsync(ownerApi.DeleteCredentialSlotAsync(
                ResolveWorldUri(), authority, deviceId, slotId, signer, OwnerRequestToken));
            PopulateCredentialChoices();
            RenderProviderConfiguration();
            return $"Deleted the saved key {slot.Label}.";
        });
    }

    private string SelectedRoleId() => SelectedAgentModelTarget() is not null
        ? "personal" : agentModelRoleChoice.Selected == 1 ? "planning" : "routine";

    private string? SelectedAgentModelTarget() => agentModelTargetChoice.Selected <= 0
        ? null : agentModelTargetChoice.GetItemMetadata(agentModelTargetChoice.Selected).AsString();

    private bool SelectedTargetWasBornHere() => observationSession.Current?.Baseline.Snapshot.Agents
        .FirstOrDefault(item => item.Id == SelectedAgentModelTarget())?.Relationships
        .Any(item => item.Type == "biological_parentage" && item.Direction == "child") == true;

    private void PopulateAgentModelTargets()
    {
        var target = SelectedAgentModelTarget();
        agentModelTargetChoice.Clear();
        agentModelTargetChoice.AddItem("World defaults");
        foreach (var agent in observationSession.Current?.Baseline.Snapshot.Agents ?? [])
        {
            agentModelTargetChoice.AddItem(agent.DisplayName);
            var index = agentModelTargetChoice.ItemCount - 1;
            agentModelTargetChoice.SetItemMetadata(index, agent.Id);
            if (agent.Id == target)
            {
                agentModelTargetChoice.Select(index);
            }
        }
    }

    private AgentProviderAssignment? SelectedAssignment() => providerConfiguration?.Assignments?
        .FirstOrDefault(item => item.AgentId == SelectedAgentModelTarget() &&
            item.Role == (SelectedRoleId() == "personal" ? "planning" : SelectedRoleId()));

    private string ActiveProviderForSelectedRole() => SelectedAgentModelTarget() is not null
        ? SelectedAssignment()?.Provider ?? "inherit"
        : providerConfiguration is null
        ? "deterministic"
        : SelectedRoleId() == "planning"
            ? providerConfiguration.PlanningProvider
            : providerConfiguration.RoutineProvider;

    private void PopulateProviderChoices(string selectedProvider)
    {
        agentModelProviderChoice.Clear();
        if (SelectedAgentModelTarget() is not null)
        {
            AddProviderChoice(SelectedTargetWasBornHere()
                ? "No personal model (built-in choices)" : "Use world default", "inherit");
        }
        AddProviderChoice("Built-in rules (no model)", "deterministic");
        if (SelectedRoleId() == "routine" && SelectedAgentModelTarget() is null)
        {
            AddProviderChoice("Jev", "jev");
        }
        if (SelectedRoleId() == "planning" || SelectedAgentModelTarget() is not null)
        {
            AddProviderChoice("OpenAI", "openai");
            AddProviderChoice("Ollama Cloud", "ollama-cloud");
            AddProviderChoice("Anthropic", "anthropic");
        }
        if (SelectedAgentModelTarget() is not null && selectedProvider == "jev")
            AddProviderChoice("Jev (from an older version)", "jev");

        SelectProviderChoice(selectedProvider);
    }

    private void SelectProviderChoice(string provider)
    {
        for (var index = 0; index < agentModelProviderChoice.ItemCount; index++)
        {
            if (agentModelProviderChoice.GetItemMetadata(index).AsString() == provider)
            {
                agentModelProviderChoice.Select(index);
                return;
            }
        }
        agentModelProviderChoice.Select(0);
    }

    private void AddProviderChoice(string label, string id)
    {
        agentModelProviderChoice.AddItem(label);
        agentModelProviderChoice.SetItemMetadata(agentModelProviderChoice.ItemCount - 1, id);
    }

    private string SelectedProviderId() => agentModelProviderChoice.Selected < 0
        ? "deterministic" : agentModelProviderChoice.GetItemMetadata(agentModelProviderChoice.Selected).AsString();

    private string SelectedCredentialChoice() => agentModelCredentialChoice.Selected < 0
        ? "default" : agentModelCredentialChoice.GetItemMetadata(agentModelCredentialChoice.Selected).AsString();

    private void PopulateCredentialChoices()
    {
        agentModelCredentialChoice.Clear();
        agentModelCredentialChoice.AddItem("Provider default key");
        agentModelCredentialChoice.SetItemMetadata(0, "default");
        var provider = SelectedProviderId();
        foreach (var slot in providerConfiguration?.CredentialSlots ?? [])
        {
            if (slot.Provider != provider) continue;
            agentModelCredentialChoice.AddItem(slot.Label);
            agentModelCredentialChoice.SetItemMetadata(agentModelCredentialChoice.ItemCount - 1, slot.Id);
        }
        agentModelCredentialChoice.AddItem("Add another API key...");
        agentModelCredentialChoice.SetItemMetadata(agentModelCredentialChoice.ItemCount - 1, "new");
        // The agent's saved thinking level applies only to the provider it was set for.
        SelectThinking(agentModelThinkingChoice, SelectedAssignment()?.Provider == provider ? SelectedAssignment()?.Thinking : null);
        var assignedSlot = SelectedAssignment()?.Provider == provider ? SelectedAssignment()?.CredentialSlotId : null;
        for (var index = 0; index < agentModelCredentialChoice.ItemCount; index++)
        {
            if (agentModelCredentialChoice.GetItemMetadata(index).AsString() != assignedSlot) continue;
            agentModelCredentialChoice.Select(index);
            return;
        }
        agentModelCredentialChoice.Select(0);
    }

    /// <summary>Providers that host an agent's own paid model, with a key and a thinking setting.</summary>
    private static bool IsModelProvider([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? provider) =>
        provider is "openai" or "ollama-cloud" or "anthropic";

    /// <summary>The thinking levels every model provider accepts; the model default sends nothing.</summary>
    private static void PopulateThinkingChoices(OptionButton choice)
    {
        choice.Clear();
        foreach (var (label, id) in new[] { ("Model default", "default"), ("Low", "low"), ("Medium", "medium"), ("High", "high") })
        {
            choice.AddItem(label);
            choice.SetItemMetadata(choice.ItemCount - 1, id);
        }
        choice.ClipText = true;
        choice.TooltipText = "How much the model thinks before it answers; more thinking takes longer and uses more paid tokens.";
        choice.Select(0);
    }

    private static string? SelectedThinking(OptionButton choice) => choice.Selected < 0 ||
        choice.GetItemMetadata(choice.Selected).AsString() is not { } id || id == "default" ? null : id;

    private static void SelectThinking(OptionButton choice, string? thinking)
    {
        for (var index = 0; index < choice.ItemCount; index++)
        {
            if (choice.GetItemMetadata(index).AsString() != (thinking ?? "default")) continue;
            choice.Select(index);
            return;
        }
        choice.Select(0);
    }

    private void RenderProviderConfiguration()
    {
        var provider = SelectedProviderId();
        var option = providerConfiguration?.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, provider, StringComparison.Ordinal));
        var usesModel = provider is not ("deterministic" or "inherit");
        var personalSetupCheckAvailable = SelectedAgentModelTarget() is not null && HasModelList(provider);
        agentModelRoleChoice.Visible = SelectedAgentModelTarget() is null;
        var agentCredential = usesModel && SelectedAgentModelTarget() is not null && IsModelProvider(provider);
        var newCredential = agentCredential && SelectedCredentialChoice() == "new";
        agentModelPicker.Visible = usesModel;
        agentModelThinkingChoice.Visible = agentCredential;
        agentModelSetupCheckButton.Visible = personalSetupCheckAvailable;
        agentModelCredentialChoice.Visible = agentCredential;
        agentModelCredentialLabelInput.Visible = newCredential;
        agentModelApiKeyInput.Visible = usesModel && (!agentCredential || newCredential);
        agentModelCredentialHint.Visible = usesModel;
        forgetAgentModelCredentialButton.Visible = usesModel && SelectedAgentModelTarget() is null;
        deleteAgentModelCredentialSlotButton.Visible = agentCredential &&
            SelectedCredentialChoice() is not ("default" or "new");
        if (usesModel) SyncAgentModelPicker();
        ResetAgentModelSetupCheckForCurrentChoice();
        agentModelSetupCheckStatus.Visible = personalSetupCheckAvailable && agentModelSetupCheckStatus.Text.Length > 0;

        agentModelApiKeyInput.PlaceholderText = newCredential ? "New API key" : option?.HasCredential == true
            ? "Leave blank to keep saved key"
            : "API key";
        agentModelCredentialHint.Text = newCredential
            ? "A new key is stored privately on the world server and can be reused for other agents."
            : agentCredential && SelectedCredentialChoice() != "default"
            ? "This key is saved on the world server."
            : agentCredential && option?.HasCredential != true
            ? "No provider default key. Select Add another API key to give this agent one."
            : option?.HasCredential == true
            ? "The key is saved on the world server."
            : "No saved key";
        agentModelConfigurationStatus.Text = providerConfiguration is null
            ? "Loading..."
            : SelectedTargetWasBornHere()
            ? SelectedChildModelStatus()
            : $"Routine: {ProviderDisplayName(providerConfiguration.RoutineProvider)} · Planning: {ProviderDisplayName(providerConfiguration.PlanningProvider)}";
        // An agent's own page needs no summary of the world's defaults, and it
        // loads fresh each time it opens, so it needs no Refresh either.
        var agentPage = selectedAgentModelScroll.Visible;
        agentModelConfigurationStatus.Visible = !agentPage || SelectedTargetWasBornHere();
        refreshAgentModelProviderButton.Visible = !agentPage;
        // Saved keys are deleted from the world's settings; an agent's page links there instead.
        deleteAgentModelCredentialSlotButton.Visible &= !agentPage;
        openModelSettingsButton.Visible = agentPage;
        RefreshControlAvailability();
    }

    private string SelectedChildModelStatus()
    {
        var childId = SelectedAgentModelTarget();
        var assignments = providerConfiguration?.Assignments ?? [];
        var routine = assignments.FirstOrDefault(item => item.AgentId == childId && item.Role == "routine");
        var planning = assignments.FirstOrDefault(item => item.AgentId == childId && item.Role == "planning");
        var child = observationSession.Current?.Baseline.Snapshot.Agents.FirstOrDefault(item => item.Id == childId);
        var birthProvider = child?.DecisionFactors.FirstOrDefault(item => item.Key == "birth-model-provider")?.Detail;
        var birthModel = child?.DecisionFactors.FirstOrDefault(item => item.Key == "birth-model-id")?.Detail;
        if ((routine is null || planning is null) && IsModelProvider(birthProvider) &&
            !string.IsNullOrWhiteSpace(birthModel))
        {
            var pendingModelName = $"{ProviderDisplayName(birthProvider)} · {birthModel}";
            if (routine is null && planning is null)
                return $"Model needs setup: {pendingModelName} was chosen at birth; its settings are waiting to be saved. Built-in choices continue until it is set up.";

            var pendingRoute = $"{pendingModelName} needs setup; built-in choices continue until then";
            return $"Routine: {(routine is null ? pendingRoute : ChildModelRouteSummary(routine))} · Planning: {(planning is null ? pendingRoute : ChildModelRouteSummary(planning))}.";
        }
        if (IsUnconfiguredChildRoute(routine) && IsUnconfiguredChildRoute(planning))
            return "No personal model is chosen for this child. Built-in choices keep them safe until you choose one; the world's default models are not used.";

        if (!SameChildProviderRoute(routine, planning))
        {
            return $"Routine: {ChildModelRouteSummary(routine)} · Planning: {ChildModelRouteSummary(planning)}.";
        }

        var assignment = planning ?? routine!;

        var provider = ProviderDisplayName(assignment.Provider);
        var model = assignment.Model ?? providerConfiguration!.Providers.FirstOrDefault(item =>
            item.Provider == assignment.Provider)?.Model;
        var modelName = string.IsNullOrWhiteSpace(model) ? provider : $"{provider} · {model}";
        if (ChildModelNeedsSetup(assignment))
            return $"Model needs setup: {modelName} has no available key. Add or choose a key; built-in choices continue until then, with no other model used.";

        return assignment.SelectionReason switch
        {
            "parents_agreed" => $"Their model choices matched: {modelName}.",
            "initiating_parent" => $"Chosen from the parent who began the family plan: {modelName}.",
            _ => $"Personal model: {modelName}.",
        };
    }

    private string ChildModelRouteSummary(AgentProviderAssignment? assignment)
    {
        if (IsUnconfiguredChildRoute(assignment))
            return "no personal model (built-in choices; the world's default models are not used)";

        var provider = ProviderDisplayName(assignment!.Provider);
        var model = assignment.Model ?? providerConfiguration!.Providers.FirstOrDefault(item =>
            item.Provider == assignment.Provider)?.Model;
        var modelName = string.IsNullOrWhiteSpace(model) ? provider : $"{provider} · {model}";
        return ChildModelNeedsSetup(assignment)
            ? $"{modelName} needs setup; built-in choices continue until a key is available"
            : modelName;
    }

    private static bool IsUnconfiguredChildRoute(AgentProviderAssignment? assignment) =>
        assignment is null || assignment.Provider is "deterministic" or "inherit";

    private static bool SameChildProviderRoute(
        AgentProviderAssignment? left,
        AgentProviderAssignment? right) =>
        IsUnconfiguredChildRoute(left) && IsUnconfiguredChildRoute(right) ||
        left is not null && right is not null && left.Provider == right.Provider &&
        left.Model == right.Model && left.CredentialSlotId == right.CredentialSlotId;

    private bool ChildModelNeedsSetup(AgentProviderAssignment assignment)
    {
        if (assignment.Provider is "deterministic" or "inherit") return false;
        if (assignment.CredentialSlotId is { } slotId)
            return providerConfiguration?.CredentialSlots?.Any(slot => slot.Id == slotId && slot.Provider == assignment.Provider) != true;
        return providerConfiguration?.Providers.FirstOrDefault(item => item.Provider == assignment.Provider)?.HasCredential != true;
    }

    private static string RoleDisplayName(string role) => role == "planning"
        ? "Planning and work decisions"
        : role == "personal" ? "this agent's decisions"
        : "Routine survival decisions";

    private static string ProviderDisplayName(string provider) => provider switch
    {
        "jev" => "Jev",
        "decisions" or "openaidecisions" => "OpenAI Decisions",
        "openai" => "OpenAI",
        "ollama-cloud" => "Ollama Cloud",
        "anthropic" => "Anthropic",
        "inherit" => "World default",
        _ => "Built-in rules",
    };

    private static string DefaultProviderModel(string provider) => provider switch
    {
        "jev" => "jev-1.13.0",
        "openai" or "decisions" => "gpt-6-luna",
        "ollama-cloud" => "glm-5.3-flash:cloud",
        "anthropic" => "claude-haiku-5-5",
        _ => string.Empty,
    };

    /// <summary>A small caption above a field that hides and shows with it.</summary>
    private static FittedLabel FieldCaption(string text, Control field)
    {
        var caption = new FittedLabel { Text = text, ThemeTypeVariation = "DimLabel", Visible = field.Visible };
        field.VisibilityChanged += () => caption.Visible = field.Visible;
        return caption;
    }

    private void BuildAgentModelPanel()
    {
        // Long key or model names are cut short rather than widening the Profile.
        foreach (var choice in new[] { agentModelProviderChoice, agentModelCredentialChoice, agentModelRoleChoice })
            choice.ClipText = true;
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);

        agentModelTargetChoice.AddItem("World defaults");
        agentModelTargetChoice.ItemSelected += _ =>
        {
            ClearAgentModelSetupCheck();
            agentModelApiKeyInput.Text = string.Empty;
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            RenderProviderConfiguration();
        };
        body.AddChild(agentModelTargetChoice);

        agentModelRoleChoice.AddItem("Routine survival");
        agentModelRoleChoice.AddItem("Planning and work");
        agentModelRoleChoice.TooltipText = "Routine covers everyday choices and planning covers bigger projects; the world's routine helper is set in Settings → World.";
        agentModelRoleChoice.ItemSelected += _ =>
        {
            ClearAgentModelSetupCheck();
            agentModelApiKeyInput.Text = string.Empty;
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            RenderProviderConfiguration();
        };
        var providerRow = new HBoxContainer();
        agentModelRoleChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        agentModelProviderChoice.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        providerRow.AddChild(agentModelRoleChoice);

        PopulateProviderChoices("deterministic");
        agentModelProviderChoice.ItemSelected += _ =>
        {
            ClearAgentModelSetupCheck();
            agentModelApiKeyInput.Text = string.Empty;
            PopulateCredentialChoices();
            RenderProviderConfiguration();
        };
        providerRow.AddChild(agentModelProviderChoice);
        body.AddChild(FieldCaption("Provider", providerRow));
        body.AddChild(providerRow);

        agentModelCredentialChoice.TooltipText = "Pick a saved key for this agent, or add another key for the same provider.";
        agentModelCredentialChoice.ItemSelected += _ =>
        {
            ClearAgentModelSetupCheck();
            agentModelApiKeyInput.Text = string.Empty;
            RenderProviderConfiguration();
        };
        body.AddChild(FieldCaption("API key", agentModelCredentialChoice));
        body.AddChild(agentModelCredentialChoice);

        agentModelCredentialLabelInput.PlaceholderText = "Name this key (for example, Personal account)";
        body.AddChild(agentModelCredentialLabelInput);

        agentModelApiKeyInput.Secret = true;
        agentModelApiKeyInput.PlaceholderText = "Paste API key";
        agentModelApiKeyInput.TextChanged += _ => OnAgentModelKeyEdited();
        body.AddChild(agentModelApiKeyInput);

        agentModelPicker.RetryRequested += () => SyncAgentModelPicker(force: true);
        agentModelPicker.ModelChanged += ClearAgentModelSetupCheck;
        body.AddChild(FieldCaption("Model", agentModelPicker));
        body.AddChild(agentModelPicker);
        PopulateThinkingChoices(agentModelThinkingChoice);
        agentModelThinkingChoice.ItemSelected += _ => ClearAgentModelSetupCheck();
        body.AddChild(FieldCaption("Thinking", agentModelThinkingChoice));
        body.AddChild(agentModelThinkingChoice);
        agentModelSetupCheckButton.Text = "Test model · 1 paid call";
        agentModelSetupCheckButton.TooltipText = "Sends one request with this model and key. It counts toward your model-call limit.";
        StyleButton(agentModelSetupCheckButton);
        agentModelSetupCheckButton.Pressed += () => _ = RunAgentModelSetupCheckAsync();
        body.AddChild(agentModelSetupCheckButton);
        agentModelSetupCheckStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        agentModelSetupCheckStatus.ThemeTypeVariation = "DimLabel";
        body.AddChild(agentModelSetupCheckStatus);

        agentModelCredentialHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        agentModelCredentialHint.ThemeTypeVariation = "DimLabel";
        body.AddChild(agentModelCredentialHint);

        agentModelCredentialHint.TooltipText = "Keys are sent securely and stay on the world server. They are never shown again, logged, or saved in world files.";

        agentModelConfigurationStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(agentModelConfigurationStatus);

        // The buttons wrap rather than push the box wider than an agent's Profile.
        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 6);
        buttons.AddThemeConstantOverride("v_separation", 6);
        saveAgentModelProviderButton.Text = "Apply";
        StyleButton(saveAgentModelProviderButton, primary: true);
        saveAgentModelProviderButton.Pressed += () => _ = SaveProviderConfigurationAsync();
        buttons.AddChild(saveAgentModelProviderButton);
        forgetAgentModelCredentialButton.Text = "Remove key";
        StyleButton(forgetAgentModelCredentialButton);
        forgetAgentModelCredentialButton.Pressed += () => _ = ForgetProviderCredentialAsync();
        buttons.AddChild(forgetAgentModelCredentialButton);
        deleteAgentModelCredentialSlotButton.Text = "Delete this key";
        deleteAgentModelCredentialSlotButton.TooltipText = "Delete a saved key. Move agents using it to another key first; a child whose model was set at birth keeps it and waits for setup.";
        StyleButton(deleteAgentModelCredentialSlotButton);
        deleteAgentModelCredentialSlotButton.Pressed += () => _ = DeleteCredentialSlotAsync();
        buttons.AddChild(deleteAgentModelCredentialSlotButton);
        openModelSettingsButton.Text = "Open model settings";
        openModelSettingsButton.TooltipText = "Opens this agent's model in World Settings, where you can also delete saved keys.";
        StyleButton(openModelSettingsButton);
        openModelSettingsButton.Pressed += () => _ = OpenWorldModelSettingsAsync();
        openModelSettingsButton.Hide();
        buttons.AddChild(openModelSettingsButton);
        refreshAgentModelProviderButton.Text = "Refresh";
        StyleButton(refreshAgentModelProviderButton);
        refreshAgentModelProviderButton.Pressed += () => _ = RefreshProviderConfigurationAsync();
        buttons.AddChild(refreshAgentModelProviderButton);
        body.AddChild(buttons);

        AddPanelContents(agentModelPanel, "Agent model", body);
        agentModelPanel.ThemeTypeVariation = "InsetPanel";
        RenderProviderConfiguration();
    }

    private void OpenAgentModelEditor()
    {
        if (selectedAgentId is null || registration is null || observationSession.Current is not { } current) return;
        PopulateAgentModelTargets();
        for (var index = 1; index < agentModelTargetChoice.ItemCount; index++)
        {
            if (agentModelTargetChoice.GetItemMetadata(index).AsString() != selectedAgentId) continue;
            agentModelTargetChoice.Select(index);
            PopulateProviderChoices(ActiveProviderForSelectedRole());
            PopulateCredentialChoices();
            agentModelTargetChoice.Hide();
            agentModelPanel.Reparent(selectedAgentModelContent, keepGlobalTransform: false);
            selectedAgentOverview.Hide();
            renameRow.Hide();
            selectedAgentModelScroll.Show();
            UpdateAgentProfileCloseButton(living: true);
            RenderProviderConfiguration();
            PositionSelectedAgentCard(current.Baseline.Snapshot);
            _ = RefreshProviderConfigurationAsync();
            return;
        }
        SetStatus("You can't change this agent's model right now.", good: false);
    }

    private readonly MarginContainer selectedAgentModelGap = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

    /// <summary>
    /// An agent's model settings fill the Profile's width and are as tall as
    /// they need, up to the bottom of the screen, scrolling beyond that.
    /// </summary>
    private void BuildAgentModelScroll()
    {
        selectedAgentModelScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        selectedAgentModelContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        selectedAgentModelContent.MinimumSizeChanged += PositionAgentProfile;
        selectedAgentModelGap.AddChild(selectedAgentModelContent);
        selectedAgentModelScroll.AddChild(selectedAgentModelGap);
    }

    private void FitAgentModelScroll()
    {
        var content = selectedAgentModelContent.GetCombinedMinimumSize().Y;
        var rest = agentProfilePanel.GetCombinedMinimumSize().Y - selectedAgentModelScroll.CustomMinimumSize.Y;
        var room = Math.Max(120, UiSize.Y - HudTop - 14 - rest);
        var scrolls = content > room;
        selectedAgentModelGap.AddThemeConstantOverride("margin_right", scrolls ? SettingsScrollGap : 0);
        selectedAgentModelScroll.CustomMinimumSize = new Vector2(0, scrolls ? room : content);
    }

    /// <summary>
    /// From an agent's page, opens Settings on the World page at the Agent
    /// model box with this agent still chosen, where saved keys are managed.
    /// </summary>
    private async Task OpenWorldModelSettingsAsync()
    {
        var generation = observationSession.RequestGeneration;
        CloseAgentModelEditor();
        if (!pauseMenuPanel.Visible) await TogglePauseMenuAsync();
        if (!IsCurrentWorldRequest(generation)) return;
        ShowSettingsSection(worldSpecific: true);
        RenderProviderConfiguration();
        // The page opens at its top; bring the box into view once it is laid out.
        Callable.From(() => settingsScroll.EnsureControlVisible(agentModelPanel)).CallDeferred();
    }

    private void CloseAgentModelEditor()
    {
        if (!selectedAgentModelScroll.Visible) return;
        selectedAgentModelScroll.Hide();
        // Agent model is the World page's last box, so it returns to the end.
        agentModelPanel.Reparent(worldSettingsContent, keepGlobalTransform: false);
        agentModelTargetChoice.Show();
        selectedAgentOverview.Show();
        agentModelApiKeyInput.Text = string.Empty;
        agentModelCredentialLabelInput.Text = string.Empty;
    }

}
