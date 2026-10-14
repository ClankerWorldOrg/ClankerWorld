using System.Net;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyOwnerRequestLifecycleAsync()
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousUrl = serverUrlInput.Text;
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
                            if (isInWorld || mainMenuCard.IsVisibleInTree() || !startupRecoveryCard.IsVisibleInTree() ||
                                startupRecovery?.Autosave?.Id != new string('c', 32) ||
                                !startupRecoverySave.Text.StartsWith("Last usable autosave:", StringComparison.Ordinal) ||
                                statusLabel.Text != "Replacement recovery screen")
                                throw new InvalidOperationException("An obsolete recovery must preserve the replacement menu and feedback.");
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
                            if (agentModelSetupCheckStatus.Text != "Replacement model result" ||
                                statusLabel.Text != "Replacement model screen")
                                throw new InvalidOperationException("A closed/reopened model panel must discard old success and refusal feedback.");
                        });
                });
            }
            await CheckAsync("obsolete key save feedback", async host =>
            {
                ShowSettingsForCheck(worldSpecific: false);
                apiKeysPanel.Show();
                apiKeyLabelInput.Text = "Disposable test key";
                apiKeyInput.Text = "disposable-loopback-key";
                saveApiKeyButton.GrabFocus();
                await HeldAsync(host, OwnerPairingEndpoints.OwnerCredentialSlotCreate, providers, false, SaveApiKeyAsync,
                    () =>
                    {
                        settingsPanel.Hide();
                        settingsPanel.Show();
                        apiKeysStatus.Text = "Replacement key page";
                    },
                    () =>
                    {
                        if (apiKeysStatus.Text != "Replacement key page")
                            throw new InvalidOperationException("Obsolete key saving must not publish a post-run failure into the reopened page.");
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
                        if (host.Requests.Contains(OwnerPairingEndpoints.OwnerDeviceList) ||
                            statusLabel.Text != "Replacement devices screen")
                            throw new InvalidOperationException("An obsolete revoke must not start a fresh registry request from the replacement screen.");
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
                        if (!manualSaveOverlay.Visible || manualSaveName.Text != "Replacement save draft" ||
                            statusLabel.Text != "Replacement saves screen")
                            throw new InvalidOperationException("Closing/reopening Manual Saves must bind the pending request to its original panel revision.");
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
                if (GetViewport().GuiGetFocusOwner() is not null)
                    throw new InvalidOperationException("Mouse map placement must begin after actual focus release.");
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
                        if (!founderSetupPanel.Visible || founderModelSetupCheckStatus.Text != "Replacement founder form" ||
                            statusLabel.Text != "Replacement founder screen" ||
                            host.Requests.Contains(OwnerPairingEndpoints.OwnerProviderStatus))
                            throw new InvalidOperationException("An obsolete map placement must keep its explicit sending form and skip its provider follow-up.");
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
            registration = previousRegistration;
            deviceKey = previousKey;
            serverUrlInput.Text = previousUrl;
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
            registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
            deviceKey = signer;
            serverUrlInput.Text = host.Address;
            observationSession.ResetAfterLoad();
            if (!observationSession.TryAccept(baseline, 0, out var failure))
                throw new InvalidOperationException($"Lifecycle checks need a valid baseline: {failure}");
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
            host.ReleasePause.TrySetResult();
            try
            {
                await check(host);
                if (isOwnerAction || isRefreshing || worldMenuBusy || startupRecoveryBusy)
                    throw new InvalidOperationException("Every completed lifecycle must restore its busy controls.");
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
}
