using System.Net;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyOwnerRequestLifecycleAsync()
    {
        var previousConnection = CaptureOwnerCheckConnection();
        var previousObservation = observationSession.Current;
        var previousProvider = providerConfiguration;
        var previousSelection = selectedAgentId;
        var previousRenderedGeneration = renderedObservationGeneration;
        var previousInWorld = isInWorld;
        var previousPanelVisibility = ownerPanelRevisions.Keys
            .Concat(new Control[] { mainMenuOverlay, worldMenuOverlay, manualSaveOverlay })
            .Distinct().ToDictionary(panel => panel, panel => panel.Visible);
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        var failures = new List<string>();
        var passed = 0;
        const string founderId = "lifecycle-founder";
        var position = new OwnerWorldPosition(1, 1);
        var snapshot = new OwnerWorldSnapshot("lifecycle-world", 0, "lifecycle-map", [], [], [], null, 0)
        {
            PackedTerrain = new(3, 3, "terrain-kind-v1", Convert.ToBase64String(new byte[9])),
            Agents = [new(founderId, "Rowan Lake", "active", position, 8_000, [], [],
                new("idle", null, null, [], string.Empty), new(position, [position], [position]), false)],
            FounderSetup = new(4, 1, false) { LastFounderId = founderId },
            Authoring = new(true, 0, 0, 0, "lifecycle-map", "lifecycle-map", "clear", "spring", []),
        };
        var baseline = new OwnerWorldReconnect(new(new(1, 1),
            ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                "owner-control.request.v1", "paused-authoring.request.v1"], []), new(snapshot, new(0, 0, [])));
        var providers = new OwnerProviderConfigurationStatus("openai", "openai", 0,
            [new("openai", "lifecycle-model", true)]);
        try
        {
            foreach (var refuse in new[] { false, true })
            {
                await CheckAsync($"obsolete recovery / refusal={refuse}", async host =>
                {
                    var save = new ManualWorldSave(new string('a', 32), "Autosave", DateTimeOffset.UnixEpoch, 0, true);
                    host.StartupRecovery = new(true, snapshot.WorldId, save);
                    await CheckStartupRecoveryAsync();
                    await HeldAsync(host, "/api/v1/owner/recovery/restore", new StartupRecoveryReceipt(save.Id, 0), refuse,
                        RecoverStartupAutosaveAsync,
                        () =>
                        {
                            registration = registration! with { DeviceId = "replacement-device" };
                            ShowStartupRecovery(new(true, "replacement-world",
                                save with { Id = new string('c', 32), Name = "Replacement autosave" }));
                            SetStatus("Replacement recovery screen", good: true);
                        },
                        () =>
                        {
                            FailUiCheckIf(isInWorld || mainMenuCard.IsVisibleInTree() || !startupRecoveryCard.IsVisibleInTree() ||
                                startupRecovery?.Autosave?.Id != new string('c', 32) ||
                                !startupRecoverySave.Text.StartsWith("Last usable autosave:", StringComparison.Ordinal) ||
                                statusLabel.Text != "Replacement recovery screen", "An obsolete recovery must preserve the replacement menu and feedback.");
                        });
                });
                await CheckAsync($"obsolete model check / refusal={refuse}", async host =>
                {
                    ShowSettingsForCheck(worldSpecific: true);
                    agentModelPanel.Show();
                    agentModelSetupCheckButton.Disabled = false;
                    agentModelSetupCheckButton.GrabFocus();
                    await HeldAsync(host, OwnerPairingEndpoints.OwnerProviderSetupCheck,
                        new OwnerProviderSetupCheckResult("ready", "Old check succeeded", true), refuse,
                        () => RunModelSetupCheckAsync(agentModelSetupCheckButton, agentModelSetupCheckStatus,
                            "openai", "lifecycle-model", new("openai", "lifecycle-model"), () => 0),
                        () =>
                        {
                            agentModelPanel.Hide();
                            agentModelPanel.Show();
                            agentModelSetupCheckStatus.Text = "Replacement model result";
                            SetStatus("Replacement model screen", good: true);
                        },
                        () =>
                        {
                            FailUiCheckIf(agentModelSetupCheckStatus.Text != "Replacement model result" ||
                                statusLabel.Text != "Replacement model screen", "A closed/reopened model panel must discard old success and refusal feedback.");
                        });
                });
            }
            foreach (var helper in new[] { "jev", "decisions" })
            {
                foreach (var refuse in new[] { false, true })
                    await CheckAsync($"replaced helper check / {helper} / refusal={refuse}", async host =>
                    {
                        ShowSettingsForCheck(worldSpecific: true);
                        host.SupportedActionPayloads = [OwnerWorldActionPayload.HelperSetupCheckPayloadDomain];
                        routineHelperChoice.Select(helper == "jev" ? 1 : 2);
                        routineHelperModelPicker.SetModel("helper-lifecycle-model");
                        routineHelperSetupCheckButton.Show();
                        routineHelperSetupCheckButton.Disabled = false;
                        routineHelperSetupCheckButton.GrabFocus();
                        routineHelperModelPicker.SetModel("   ");
                        await RunRoutineHelperSetupCheckAsync();
                        FailUiCheckIf(host.Requests.Contains(OwnerPairingEndpoints.OwnerProviderSetupCheck), "An empty helper model must never send a paid test.");
                        routineHelperModelPicker.SetModel("helper-lifecycle-model");
                        routineHelperSetupCheckButton.Disabled = false;
                        FailUiCheckIf(routineHelperSetupCheckButton.GetParent() != routineHelperChoice.GetParent() ||
                            routineHelperSetupCheckButton.Text != "Test helper · 1 paid call" ||
                            !routineHelperSetupCheckButton.TooltipText.Contains("model-call limit", StringComparison.Ordinal), "The helper check must sit beside its settings and disclose the paid call.");
                        await HeldAsync(host, OwnerPairingEndpoints.OwnerProviderSetupCheck,
                            new OwnerProviderSetupCheckResult("ready", "Old helper succeeded", true), refuse,
                            RunRoutineHelperSetupCheckAsync,
                            () =>
                            {
                                routineHelperChoice.Select(0);
                                routineHelperChoice.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                                FailUiCheckIf(routineHelperSetupCheckStatus.Text.Length != 0, "Changing helper must clear its old check result.");
                                routineHelperSetupCheckStatus.Text = "Replacement helper result";
                                SetStatus("Replacement helper choice", good: true);
                            },
                            () =>
                            {
                                FailUiCheckIf(routineHelperSetupCheckStatus.Text != "Replacement helper result" ||
                                    statusLabel.Text != "Replacement helper choice", "A replaced helper check must discard both old success and refusal feedback.");
                            });
                    });
                await CheckAsync($"older host helper check / {helper}", async host =>
                {
                    ShowSettingsForCheck(worldSpecific: true);
                    routineHelperChoice.Select(helper == "jev" ? 1 : 2);
                    routineHelperModelPicker.SetModel("helper-lifecycle-model");
                    routineHelperSetupCheckButton.Show();
                    routineHelperSetupCheckButton.Disabled = false;
                    routineHelperSetupCheckButton.GrabFocus();
                    await RunRoutineHelperSetupCheckAsync();
                    FailUiCheckIf(!routineHelperSetupCheckStatus.Text.Contains("Nothing was sent", StringComparison.Ordinal) ||
                        host.Requests.Contains(OwnerPairingEndpoints.OwnerProviderSetupCheck), "An older host must explain the update without sending a paid helper request.");
                });
            }
            await CheckAsync("model settings refresh retains provider controls", async host =>
            {
                ShowSettingsForCheck(worldSpecific: true);
                if (agentModelPanel.GetParent() != worldSettingsContent)
                    agentModelPanel.Reparent(worldSettingsContent, keepGlobalTransform: false);
                agentModelTargetChoice.Show();
                agentModelPanel.Show();
                selectedAgentModelScroll.Hide();
                settingsScroll.Show();
                var saved = new OwnerProviderConfigurationStatus("deterministic", "deterministic", 0,
                    [new("openai", "lifecycle-model", true)]);
                providerConfiguration = saved;
                host.ActionHandler = (path, _, _) => Task.FromResult<object?>(
                    path == OwnerPairingEndpoints.OwnerProviderStatus ? saved : null);
                PopulateAgentModelTargets();
                agentModelTargetChoice.Select(0);
                agentModelRoleChoice.Select(1);
                PopulateProviderChoices("openai");
                PopulateCredentialChoices();
                RenderProviderConfiguration();
                agentModelApiKeyInput.Text = "disposable-loopback-draft";
                if (!agentModelProviderChoice.IsVisibleInTree() || !agentModelApiKeyInput.IsVisibleInTree())
                    throw new InvalidOperationException($"The refresh regression needs visible planning-provider controls: provider={SelectedProviderId()}, role={SelectedRoleId()}, providerVisible={agentModelProviderChoice.IsVisibleInTree()}, keyVisible={agentModelApiKeyInput.IsVisibleInTree()}.");
                refreshAgentModelProviderButton.GrabFocus();
                await RefreshProviderConfigurationAsync();
                if (SelectedProviderId() != "openai" || !agentModelPicker.Visible ||
                    !agentModelApiKeyInput.Visible || agentModelApiKeyInput.Text != "disposable-loopback-draft" ||
                    !host.Requests.Contains(OwnerPairingEndpoints.OwnerProviderStatus))
                    throw new InvalidOperationException($"Refreshing model settings must keep the provider draft and render its model/key controls: provider={SelectedProviderId()}, picker={agentModelPicker.Visible}, keyVisible={agentModelApiKeyInput.Visible}, draftKept={agentModelApiKeyInput.Text == "disposable-loopback-draft"}, read={host.Requests.Contains(OwnerPairingEndpoints.OwnerProviderStatus)}, status={statusLabel.Text}.");
            });
            await CheckAsync("TypeSafe key save and delete", async host =>
            {
                ShowSettingsForCheck(worldSpecific: false);
                apiKeysPanel.Show();
                var index = Enumerable.Range(0, apiKeyProviderChoice.ItemCount)
                    .Single(item => apiKeyProviderChoice.GetItemMetadata(item).AsString() == "jev");
                FailUiCheckIf(apiKeyProviderChoice.GetItemText(index) != "TypeSafe (Jev)" ||
                    DefaultProviderModel("jev") != "jev-1.14.0", "API keys must offer TypeSafe and Jev must start on 1.14.0.");
                apiKeyProviderChoice.Select(index);
                apiKeyLabelInput.Text = "TypeSafe account";
                apiKeyInput.Text = "synthetic-typesafe-key";
                string? slotId = null;
                host.ActionHandler = (path, envelope, _) =>
                {
                    if (path == OwnerPairingEndpoints.OwnerCredentialSlotCreate)
                    {
                        var action = envelope.GetProperty("action");
                        FailUiCheckIf(action.GetProperty("provider").GetString() != "jev" ||
                            action.GetProperty("apiKey").GetString() != "synthetic-typesafe-key", "Save must submit the TypeSafe provider and pasted key.");
                        slotId = action.GetProperty("credentialSlotId").GetString();
                        return Task.FromResult<object?>(providers with { CredentialSlots = [new(slotId!, "jev", "TypeSafe account")] });
                    }
                    return Task.FromResult<object?>(path == OwnerPairingEndpoints.OwnerCredentialSlotDelete
                        ? providers with { CredentialSlots = [] } : null);
                };
                await SaveApiKeyAsync();
                FailUiCheckIf(slotId is null || apiKeyInput.Text.Length != 0 || !savedApiKeys.Text.Contains("TypeSafe · TypeSafe account", StringComparison.Ordinal), "Saving a TypeSafe key must clear its secret and list its name.");
                routineHelperChoice.Select(1);
                RefreshRoutineHelperKeys(slotId);
                RequestRoutineHelperModels();
                FailUiCheckIf(!routineHelperKeyChoice.Visible || SelectedRoutineHelperKey() != slotId ||
                    routineHelperKeyChoice.ItemCount != 2, "Jev must offer its saved TypeSafe key without OpenAI keys.");
                routineHelperChoice.Select(2);
                RefreshRoutineHelperKeys();
                FailUiCheckIf(SelectedRoutineHelperKey() is not null || routineHelperKeyChoice.ItemCount != 1, "Switching to Decisions must release the TypeSafe key selection.");
                await DeleteApiKeyAsync();
                FailUiCheckIf(!host.Requests.Contains(OwnerPairingEndpoints.OwnerCredentialSlotDelete) ||
                    providerConfiguration?.CredentialSlots?.Count != 0 || savedApiKeyChoice.Visible || deleteApiKeyButton.Visible, "An unused TypeSafe key must be deletable from API keys.");
            });
            foreach (var deletingKey in new[] { false, true })
                await CheckAsync($"obsolete key feedback / delete={deletingKey}", async host =>
            {
                ShowSettingsForCheck(worldSpecific: false);
                apiKeysPanel.Show();
                apiKeyLabelInput.Text = "Disposable test key";
                apiKeyInput.Text = "disposable-loopback-key";
                providerConfiguration = providers with { CredentialSlots = [new("1234567890abcdef1234567890abcdef", "jev", "TypeSafe account")] };
                RenderSavedApiKeys();
                (deletingKey ? deleteApiKeyButton : saveApiKeyButton).GrabFocus();
                await HeldAsync(host, deletingKey ? OwnerPairingEndpoints.OwnerCredentialSlotDelete : OwnerPairingEndpoints.OwnerCredentialSlotCreate,
                    providers, false, deletingKey ? DeleteApiKeyAsync : SaveApiKeyAsync,
                    () =>
                    {
                        settingsPanel.Hide();
                        settingsPanel.Show();
                        apiKeysStatus.Text = "Replacement key page";
                    },
                    () =>
                    {
                        FailUiCheckIf(apiKeysStatus.Text != "Replacement key page", "Obsolete key saving must not publish a post-run failure into the reopened page.");
                    });
            });
            await CheckAsync("obsolete revoke follow-up", async host =>
            {
                ShowSettingsForCheck(worldSpecific: false);
                developerPanel.Show();
                revokeDeviceId.Text = "other-device";
                revokeDeviceButton.GrabFocus();
                await HeldAsync(host, OwnerPairingEndpoints.OwnerDeviceRevoke,
                    new OwnerDevice("other-device", signer.PublicKeySpkiBase64, signer.PublicKeyFingerprint,
                        OwnerDeviceState.Revoked, DateTimeOffset.UnixEpoch, null), false, RevokeDeviceAsync,
                    () =>
                    {
                        developerPanel.Hide();
                        settingsPanel.Hide();
                        settingsPanel.Show();
                        developerPanel.Show();
                        SetStatus("Replacement devices screen", good: true);
                    },
                    () =>
                    {
                        FailUiCheckIf(host.Requests.Contains(OwnerPairingEndpoints.OwnerDeviceList) ||
                            statusLabel.Text != "Replacement devices screen", "An obsolete revoke must not start a fresh registry request from the replacement screen.");
                    });
            });
            await CheckAsync("obsolete manual save panel", async host =>
            {
                manualSaveOverlay.Show();
                manualSaveName.Text = "Old save draft";
                manualSaveCreateButton.Disabled = false;
                manualSaveCreateButton.GrabFocus();
                await HeldAsync(host, OwnerPairingEndpoints.OwnerSaveCreate,
                    new ManualWorldSave(new string('b', 32), "Old save draft", DateTimeOffset.UnixEpoch, 0), false,
                    CreateManualSaveAsync,
                    () =>
                    {
                        manualSaveOverlay.Hide();
                        manualSaveOverlay.Show();
                        manualSaveName.Text = "Replacement save draft";
                        SetStatus("Replacement saves screen", good: true);
                    },
                    () =>
                    {
                        FailUiCheckIf(!manualSaveOverlay.Visible || manualSaveName.Text != "Replacement save draft" ||
                            statusLabel.Text != "Replacement saves screen", "Closing/reopening Manual Saves must bind the pending request to its original panel revision.");
                    });
            });
            await CheckAsync("obsolete founder map placement", async host =>
            {
                pauseMenuPanel.Hide();
                providerConfiguration = providers;
                PopulateFounderCredentials();
                founderModelPicker.SetModel("lifecycle-model", isNewAgent: true);
                founderSetupPanel.Show();
                GetViewport().GuiReleaseFocus();
                FailUiCheckIf(GetViewport().GuiGetFocusOwner() is not null, "Mouse map placement must begin after actual focus release.");
                await HeldAsync(host, OwnerPairingEndpoints.OwnerFounderPlace,
                    new OwnerFounderPlacementReceipt("placed-founder", "lifecycle-household", 2, 4), false,
                    // This is the async handler dispatched by ActivateMapTile after mouse focus release.
                    () => PlaceFounderAtAsync(new(0, 0)),
                    () =>
                    {
                        founderSetupPanel.Hide();
                        founderSetupPanel.Show();
                        founderModelSetupCheckStatus.Text = "Replacement founder form";
                        SetStatus("Replacement founder screen", good: true);
                    },
                    () =>
                    {
                        FailUiCheckIf(!founderSetupPanel.Visible || founderModelSetupCheckStatus.Text != "Replacement founder form" ||
                            statusLabel.Text != "Replacement founder screen" ||
                            host.Requests.Contains(OwnerPairingEndpoints.OwnerProviderStatus), "An obsolete map placement must keep its explicit sending form and skip its provider follow-up.");
                    });
            });
            foreach (var selected in new[] { false, true })
                await CheckAsync($"founder undo / selected={selected}", async host =>
                {
                    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    host.ActionHandler = (path, _, _) =>
                    {
                        if (path != OwnerPairingEndpoints.OwnerFounderUndo) return Task.FromResult<object?>(null);
                        host.Reconnect = baseline with
                        {
                            Baseline = baseline.Baseline with
                            {
                                Snapshot = snapshot with { Agents = [], FounderSetup = new(4, 0, false) },
                            }
                        };
                        entered.TrySetResult();
                        return Task.FromResult<object?>(new OwnerFounderUndoReceipt(founderId, 0, 4));
                    };
                    providerConfiguration = null;
                    if (selected) SelectAgent(founderId);
                    undoFounderButton.GrabFocus();
                    undoFounderButton.EmitSignal(BaseButton.SignalName.Pressed);
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await WaitForWorldPreviewSmokeAsync(() => !isOwnerAction && !isRefreshing, "founder undo lifecycle cleanup");
                    if (host.Requests.Count(path => path == OwnerPairingEndpoints.OwnerFounderUndo) != 1 ||
                        host.Requests.Count(path => path == OwnerPairingEndpoints.OwnerProviderStatus) != 1 ||
                        observationSession.Current?.Baseline.Snapshot.FounderSetup?.Placed != 0 ||
                        selectedAgentId is not null || providerConfiguration is null ||
                        !statusLabel.Text.StartsWith("Last founder removed", StringComparison.Ordinal))
                        throw new InvalidOperationException($"Founder Undo needs roster/status cleanup: undo={host.Requests.Count(path => path == OwnerPairingEndpoints.OwnerFounderUndo)}, provider={host.Requests.Count(path => path == OwnerPairingEndpoints.OwnerProviderStatus)}, placed={observationSession.Current?.Baseline.Snapshot.FounderSetup?.Placed}, selected={selectedAgentId}, providerLoaded={providerConfiguration is not null}, status={statusLabel.Text}, current={observationSession.RequestGeneration}, panel={CaptureOwnerRequestPanel()()}");
                });
        }
        finally
        {
            RestoreOwnerCheckConnection(previousConnection);
            providerConfiguration = previousProvider;
            observationSession.ResetAfterLoad();
            if (previousObservation is not null &&
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _))
                Render(previousObservation.Baseline.Snapshot, []);
            selectedAgentId = previousSelection;
            renderedObservationGeneration = previousRenderedGeneration;
            startupRecovery = null;
            startupRecoveryCard.Hide();
            manualSaveOverlay.Hide();
            developerPanel.Hide();
            settingsPanel.Hide();
            agentModelPanel.Hide();
            apiKeyInput.Text = string.Empty;
            ShowMainMenu();
            isInWorld = previousInWorld;
            foreach (var (panel, visible) in previousPanelVisibility) panel.Visible = visible;
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
        GD.Print($"Owner lifecycle checks: {passed} passed, {failures.Count} failed.");
        if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));

        void ShowSettingsForCheck(bool worldSpecific)
        {
            pauseMenuPanel.Show();
            settingsPanel.Show();
            worldSettingsContent.Visible = worldSpecific;
            gameSettingsContent.Visible = !worldSpecific;
        }

        async Task CheckAsync(string name, Func<WorldActionSmokeHost, Task> check)
        {
            using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64)
            {
                Reconnect = baseline,
                SupportedActionPayloads = [OwnerWorldActionPayload.ModelThinkingPayloadDomain],
            };
            ConnectOwnerCheck(host, signer);
            observationSession.ResetAfterLoad();
            // Fixture setup initializes both serializers in this process; actions keep the normal read deadline.
            var initialAction = new OwnerReconnectAction(0, null, null, null);
            var initial = await new OwnerPairingClient(httpClient).SendSignedActionAsync<OwnerReconnectAction, OwnerWorldReconnect>(
                new Uri(host.Address), host.Authority, "smoke-device", OwnerPairingEndpoints.OwnerReconnect,
                OwnerPairingProtocol.CreateRequestId(), OwnerWorldActionPayload.Reconnect(initialAction),
                initialAction, signer, CancellationToken.None);
            if (!observationSession.TryAccept(initial, 0, out var failure))
                throw new InvalidOperationException($"Lifecycle checks need their signed initial baseline: {failure}");
            selectedAgentId = null;
            selectedBuildingId = null;
            Render(snapshot, []);
            renderedObservationGeneration = observationSession.RequestGeneration;
            ShowMainMenu();
            mainMenuOverlay.Hide();
            settingsPanel.Hide();
            agentModelPanel.Hide();
            developerPanel.Hide();
            founderSetupPanel.Hide();
            agentQuickCard.Hide();
            agentProfilePanel.Hide();
            manualSaveOverlay.Hide();
            isInWorld = true;
            host.Requests.Clear();
            host.ReleasePause.TrySetResult();
            try
            {
                await check(host);
                FailUiCheckIf(isOwnerAction || isRefreshing || worldMenuBusy || startupRecoveryBusy, "Every completed lifecycle must restore its busy controls.");
                passed++;
                GD.Print($"Owner lifecycle PASS: {name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
                GD.Print($"Owner lifecycle FAIL: {name}: {exception.Message}");
            }
        }

        async Task HeldAsync(WorldActionSmokeHost host, string endpoint, object response, bool refuse,
            Func<Task> start, Action replace, Action verify)
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.ActionHandler = async (path, _, httpResponse) =>
            {
                if (path == OwnerPairingEndpoints.OwnerDeviceList) return Array.Empty<OwnerDevice>();
                if (path != endpoint) return null;
                entered.TrySetResult();
                await release.Task.ConfigureAwait(false);
                if (!refuse) return response;
                httpResponse.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                return new { error = "Controlled obsolete request refusal." };
            };
            var operation = start();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                if (!isOwnerAction) throw new InvalidOperationException("The real signed request must be held inside its busy lifecycle.");
                replace();
                release.TrySetResult();
                await operation.WaitAsync(TimeSpan.FromSeconds(10));
                verify();
            }
            finally
            {
                release.TrySetResult();
                await operation.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    // The checks own key/host lifetimes; this snapshot only borrows the UI binding.
    private readonly record struct OwnerCheckConnection(
        OwnerDeviceRegistration? Registration, OwnerDeviceKey? Key, string Url);

    private OwnerCheckConnection CaptureOwnerCheckConnection() =>
        new(registration, deviceKey, serverUrlInput.Text);

    private void ConnectOwnerCheck(WorldActionSmokeHost host, OwnerDeviceKey signer)
    {
        registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
        deviceKey = signer;
        serverUrlInput.Text = host.Address;
    }

    private void RestoreOwnerCheckConnection(OwnerCheckConnection previous)
    {
        registration = previous.Registration;
        deviceKey = previous.Key;
        serverUrlInput.Text = previous.Url;
    }
}
