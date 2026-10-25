using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifySameWorldTimelineUiRecoveryAsync()
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousUrl = serverUrlInput.Text;
        var previousObservation = observationSession.Current;
        var previousSelection = selectedAgentId;
        var previousPendingSubmission = pendingSubmission;
        var previousProviderConfiguration = providerConfiguration;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        var previousMenuVisible = pauseMenuPanel.Visible;
        var previousSettingsVisible = settingsPanel.Visible;
        var previousWorldSettingsVisible = worldSettingsContent.Visible;
        var previousGameSettingsVisible = gameSettingsContent.Visible;
        var previousInWorld = isInWorld;
        var previousMainMenuVisible = mainMenuOverlay.Visible;
        var previousWorldMenuVisible = worldMenuOverlay.Visible;
        var previousResumeOnContinue = resumeWorldOnContinue;
        var previousMenuPausedWorld = menuPausedWorld;
        var previousTownSiteMode = choosingFirstTownSite;
        var previousModel = agentModelPicker.Model;
        var previousModelTyping = agentModelPicker.IsTyping;
        var previousRole = agentModelRoleChoice.Selected;
        var previousModelKey = agentModelApiKeyInput.Text;
        var previousDeveloperKind = developerEditKind.Selected;
        var previousDeveloperValue = developerEditValue.Selected;
        var previousDeveloperAmount = developerEditAmount.Value;
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        const string worldId = "timeline-ui-world";
        const string agentId = "timeline-ui-agent";
        const string listenerId = "timeline-ui-listener";
        const string turnId = "timeline-ui-turn";
        const string retainedKey = "timeline-ui-instruction-key";
        const string retainedText = "Remember the orchard.";
        OwnerWorldReconnect World(long generation, long tick, int eventCount, bool paused = false)
        {
            var position = new OwnerWorldPosition(1, 1);
            OwnerWorldAgent Agent(string id, string name) => new(id, name, "active", position, 8_000, [], [],
                new("idle", null, null, [], string.Empty), new(position, [position], [position]), false);
            var snapshot = new OwnerWorldSnapshot(worldId, tick, "timeline-ui-map",
                Enumerable.Range(0, 9).Select(index => new OwnerWorldTile(index % 3, index / 3, "meadow")).ToArray(),
                [], [], null, eventCount)
            {
                PackedTerrain = new(3, 3, "terrain-kind-v1", Convert.ToBase64String(new byte[9])),
                Agents = [Agent(agentId, "Rowan Lake"), Agent(listenerId, "Aster Vale")],
                Handcarts = [new("timeline-cart", agentId, "Rowan Lake", new(tick == 19 ? 0 : 1, 1),
                    32, 100, null, null, [])],
                Conversations = [new("timeline-ui-conversation", agentId, "Rowan Lake", listenerId, "Aster Vale",
                    "completed", null, null, 0, tick,
                    [new(turnId, agentId, "Rowan Lake", $"Timeline {generation} conversation.", tick, [listenerId], false)])],
                Authoring = new(paused, 0, 0, 0, "timeline-ui-map", "timeline-ui-map", "clear", "spring", []),
                FounderSetup = new(4, 0, false) { CanChooseTownSite = true },
            };
            var events = Enumerable.Range(1, eventCount)
                .Select(id => new OwnerWorldEvent(id, tick, "timeline_smoke", $"generation:{generation}") { Text = $"generation:{generation}" })
                .ToArray();
            return new(new(new(1, 1),
                    ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                     "owner-control.request.v1", "paused-authoring.request.v1", "owner-observation-timeline.v1"], []),
                new(snapshot, new(tick, 0, events), new("timeline-ui-host", generation)));
        }

        try
        {
            registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
            deviceKey = signer;
            serverUrlInput.Text = host.Address;
            foreach (var lateFailure in new[] { false, true })
            {
                observationSession.ReplaceRegistration(registration);
                ResetDisplayedWorldContext();
                host.Reconnect = World(1, 19, 0);
                await RefreshAsync();
                host.Reconnect = World(1, 20, 2);
                await RefreshAsync();
                FailUiCheckIf(observationSession.Timeline?.Generation != 1 || observationSession.Current?.Baseline.Snapshot.WorldTick != 20, "The timeline UI check must first render its signed initial baseline.");
                var heldCart = mapObjectVisuals["handcart:timeline-cart"];
                void CheckCartFacing(int facing)
                {
                    using var pixels = mapObjectVisuals["handcart:timeline-cart"].GetNode<TextureRect>("HandcartSprite").Texture.GetImage();
                    FailUiCheckIf(HandcartPixelDigest(pixels) != ApprovedHandcartDigest(facing, loaded: false, pulled: false), "A handcart must keep its held heading until a fresh timeline is accepted, then start facing south.");
                }
                CheckCartFacing(6);
                selectedAgentId = agentId;
                var retained = OwnerPendingSubmission.ForInstruction(
                    OwnerPendingSubmissionBinding.Create(host.Authority, "smoke-device", signer.PublicKeyFingerprint,
                        new Uri(host.Address), observationSession.Timeline, worldId),
                    new(retainedKey, agentId, "guidance", retainedText, worldId));
                pendingSubmission = retained;
                RenderPendingSubmission();
                RefreshControlAvailability();
                FailUiCheckIf(retryPendingSubmissionButton.Disabled, "A retained instruction must initially be retryable on its original timeline.");
                lastSeenEventId = 20;
                newEventsAfter = 19;
                unreadEvents = 4;
                conversationReadWorldId = worldId;
                locallyReadConversationTurns.Add(ConversationTurnKey(worldId, turnId));
                void VerifyRetainedInstruction()
                {
                    FailUiCheckIf(!retryPendingSubmissionButton.Disabled || !ReferenceEquals(pendingSubmission, retained) ||
                        pendingSubmission?.Instruction?.IdempotencyKey != retainedKey ||
                        pendingSubmission?.Instruction?.Text != retainedText || pendingSubmission?.Binding != retained.Binding, "A previous-timeline instruction must remain unchanged with Retry disabled.");
                }
                var heldTerrain = terrainMap;
                pauseMenuPanel.Show();
                settingsPanel.Show();
                gameSettingsContent.Hide();
                worldSettingsContent.Show();

                // Leave both panels open and their reads pending. The world ID,
                // registration and local request identities stay the same.
                var oldSettings = new TaskCompletionSource<WorldAutosaveSettings>();
                var oldSaves = new TaskCompletionSource<ManualWorldSave[]>();
                var settingsRead = RefreshAutosaveSettingsAsync(_ => oldSettings.Task);
                var savesRead = OpenManualSavesAsync(true, _ => oldSaves.Task);
                choosingFirstTownSite = true;
                var generation = observationSession.RequestGeneration;
                host.Reconnect = World(2, 5, 1);
                await RefreshAsync();
                VerifyRetainedInstruction();
                FailUiCheckIf(!observationSession.AwaitingFreshBaseline || observationSession.RequestGeneration == generation ||
                    observationSession.Current?.Baseline.Snapshot.WorldId != worldId ||
                    observationSession.Current?.Baseline.Snapshot.WorldTick != 20 || selectedAgentId != agentId ||
                    !knownEvents.ContainsKey(2) || !ReferenceEquals(terrainMap, heldTerrain) || !choosingFirstTownSite, "A same-world rewind must hold the old display while invalidating its requests.");
                FailUiCheckIf(!ReferenceEquals(mapObjectVisuals["handcart:timeline-cart"], heldCart), "Discovering a timeline change must hold the currently displayed cart.");
                CheckCartFacing(6);
                var heldSettingsStatus = autosaveSettingsStatus.Text;
                var heldSavesStatus = manualSaveStatus.Text;
                var heldSavePlaceholder = manualSaveList.Placeholder;
                if (lateFailure)
                {
                    oldSettings.SetException(new IOException("Controlled previous-timeline settings failure."));
                    oldSaves.SetException(new IOException("Controlled previous-timeline save-list failure."));
                }
                else
                {
                    oldSettings.SetResult(new(worldId, true, 1, 3, DateTimeOffset.UnixEpoch, -1));
                    oldSaves.SetResult([new("obsolete-save", "Previous timeline", DateTimeOffset.UnixEpoch, 20)]);
                }
                await Task.WhenAll(settingsRead, savesRead);
                FailUiCheckIf(autosaveSettingsLoaded || !autosaveApplyButton.Disabled || manualSaveList.ItemCount != 0 ||
                    autosaveSettingsStatus.Text != heldSettingsStatus || manualSaveStatus.Text != heldSavesStatus ||
                    manualSaveList.Placeholder != heldSavePlaceholder || !manualSaveLoadButton.Disabled, "A held response from the previous timeline must not publish settings, saves or failure labels.");

                // The next real reconnect supplies the complete replacement;
                // its unchanged world/map/agent IDs must not preserve old UI caches.
                await RefreshAsync();
                VerifyRetainedInstruction();
                FailUiCheckIf(observationSession.AwaitingFreshBaseline || observationSession.Timeline?.Generation != 2 ||
                    observationSession.Current?.Baseline.Snapshot.WorldTick != 5 || selectedAgentId is not null ||
                    knownEvents.ContainsKey(2) || knownEvents.Count != 1 || knownEvents[1].Text != "generation:2" ||
                    ReferenceEquals(terrainMap, heldTerrain) || manualSaveOverlay.Visible || choosingFirstTownSite ||
                    observationSession.Current?.Baseline.Snapshot.FounderSetup?.CanChooseTownSite != true, "Accepting a fresh same-world baseline must clear old events, selection and terrain caches.");
                FailUiCheckIf(ReferenceEquals(mapObjectVisuals["handcart:timeline-cart"], heldCart), "Accepting a fresh timeline must replace the previous cart marker.");
                CheckCartFacing(0);
                FailUiCheckIf(lastSeenEventId > 1 || newEventsAfter != long.MaxValue || unreadEvents != 0 ||
                    locallyReadConversationTurns.Contains(ConversationTurnKey(worldId, turnId)), "A fresh timeline must reset event unread state and reused conversation-turn read markers.");

                await RefreshAutosaveSettingsAsync(_ => Task.FromResult(
                    new WorldAutosaveSettings(worldId, true, 10, 5, DateTimeOffset.UnixEpoch, -1)));
                await OpenManualSavesAsync(true, _ => Task.FromResult<ManualWorldSave[]>(
                    [new("current-save", "Current timeline", DateTimeOffset.UnixEpoch, 5)]));
                FailUiCheckIf(!autosaveSettingsLoaded || autosaveIntervalChoice.GetSelectedId() != 10 ||
                    autosaveRotationChoice.GetSelectedId() != 5 || manualSaveList.ItemCount != 1 ||
                    listedManualSaves[0].Id != "current-save" || manualSaveList.GetItemTitle(0) != "Current timeline", "New-generation settings and save-list reads must still populate their controls.");
                manualSaveOverlay.Hide();
            }

            // Loading owns its timeline transition. A normal pulse can observe
            // that transition before the signed load receipt reaches the client.
            pendingSubmission = null;
            RefreshControlAvailability();
            isInWorld = true;
            mainMenuOverlay.Show();
            worldMenuOverlay.Show();
            resumeWorldOnContinue = true;
            menuPausedWorld = true;
            await OpenManualSavesAsync(true, _ => Task.FromResult<ManualWorldSave[]>(
                [new("race-save", "Earlier world", DateTimeOffset.UnixEpoch, 2)]));
            manualSaveList.Select(0);
            host.ReleasePause.TrySetResult();
            host.LoadReceipt = new("race-save", "before-race-load", 2);
            var releaseLoad = host.ReleaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var loading = LoadSelectedManualSaveAsync();
            try
            {
                await Task.WhenAny(host.LoadReceived.Task, loading).WaitAsync(TimeSpan.FromSeconds(5));
                FailUiCheckIf(!host.LoadReceived.Task.IsCompleted || await host.LoadReceived.Task != "race-save" ||
                    !isOwnerAction || !observationSession.AwaitingFreshBaseline || loading.IsCompleted, "The manual-load race must hold a signed load receipt after starting its timeline transition.");
                var beforePulse = observationSession.RequestGeneration;
                host.Reconnect = World(3, 2, 0);
                await PulseAsync();
                FailUiCheckIf(!isOwnerAction || loading.IsCompleted || observationSession.AwaitingFreshBaseline ||
                    observationSession.RequestGeneration == beforePulse || observationSession.Timeline?.Generation != 3 ||
                    observationSession.Current?.Baseline.Snapshot.WorldTick != 2 ||
                    !mainMenuOverlay.Visible || !worldMenuOverlay.Visible, "A normal pulse must accept the loaded baseline while its load receipt is still held.");
                releaseLoad.SetResult();
                await loading.WaitAsync(TimeSpan.FromSeconds(5));
                FailUiCheckIf(!isInWorld || isOwnerAction || mainMenuOverlay.Visible || worldMenuOverlay.Visible || manualSaveOverlay.Visible ||
                    resumeWorldOnContinue || menuPausedWorld || observationSession.AwaitingFreshBaseline ||
                    observationSession.Timeline?.Generation != 3 || observationSession.Current?.Baseline.Snapshot.WorldTick != 2 ||
                    !statusLabel.Text.StartsWith("Loaded Earlier world.", StringComparison.Ordinal), "A delayed receipt for our own load must finish closing its menus and keep the accepted loaded world.");
            }
            finally
            {
                releaseLoad.TrySetResult();
                await loading.WaitAsync(TimeSpan.FromSeconds(5));
            }

            foreach (var lateFailure in new[] { false, true })
            {
                observationSession.ReplaceRegistration(registration);
                ResetDisplayedWorldContext();
                host.Reconnect = World(10, 20, 2);
                await RefreshAsync();
                pauseMenuPanel.Show();
                settingsPanel.Show();
                gameSettingsContent.Hide();
                worldSettingsContent.Show();
                agentModelApiKeyInput.Text = string.Empty;
                var models = new OwnerProviderConfigurationStatus("deterministic", "openai", 0,
                    [new("openai", "timeline-selected-model", true)]);
                void ShowSameModelContext()
                {
                    providerConfiguration = models;
                    PopulateAgentModelTargets();
                    agentModelTargetChoice.Select(0);
                    agentModelRoleChoice.Select(1);
                    PopulateProviderChoices("openai");
                    PopulateCredentialChoices();
                    RenderProviderConfiguration();
                }
                bool ListsModel(string model) => Enumerable.Range(0, agentModelPicker.Choice.ItemCount)
                    .Any(index => agentModelPicker.Choice.GetItemText(index) == model);
                host.FailModels = false;
                host.ReleaseModels = null;
                host.Models = new("openai", [new("timeline-selected-model", true), new("timeline-initial-model", true)],
                    "timeline-selected-model", null);
                ShowSameModelContext();
                await WaitForTimelineSmokeAsync(() => ListsModel("timeline-initial-model"),
                    "The model lookup must first populate through its ordinary context cache and signed host endpoint.");

                host.ModelsReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseModels = host.ReleaseModels = new(TaskCreationOptions.RunContinuationsAsynchronously);
                host.Models = new("openai", [new("timeline-obsolete-model", true)], "timeline-obsolete-model", null);
                host.FailModels = lateFailure;
                // A retry for the same provider and key still uses the existing
                // context cache, but the held read below has its own Task.
                var modelRead = LoadModelListAsync(agentModelPicker, "openai", null, null);
                try
                {
                    await Task.WhenAny(host.ModelsReceived.Task, modelRead).WaitAsync(TimeSpan.FromSeconds(5));
                    FailUiCheckIf(!host.ModelsReceived.Task.IsCompleted || modelRead.IsCompleted ||
                        await host.ModelsReceived.Task != new OwnerProviderModelListAction("openai", null, null, true), "The old model read must reach the signed endpoint with the same provider and key before recovery.");
                    host.Reconnect = World(11, 5, 1);
                    await RefreshAsync();
                    await RefreshAsync();
                    FailUiCheckIf(observationSession.AwaitingFreshBaseline || observationSession.Timeline?.Generation != 11, "The model-list check must accept a replacement timeline before releasing its old reply.");
                    releaseModels.SetResult();
                    await modelRead.WaitAsync(TimeSpan.FromSeconds(5));
                    FailUiCheckIf(ListsModel("timeline-obsolete-model") || agentModelPicker.Problem.Length != 0, "A late old-timeline model success or failure must not publish into the replacement picker.");

                    host.ModelsReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    host.ReleaseModels = null;
                    host.FailModels = false;
                    host.Models = new("openai", [new("timeline-selected-model", true), new("timeline-current-model", true)],
                        "timeline-selected-model", null);
                    ShowSameModelContext();
                    await host.ModelsReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    await WaitForTimelineSmokeAsync(() => ListsModel("timeline-current-model"),
                        "Reopening the unchanged model context after recovery must issue a new lookup and populate its current list.");
                    FailUiCheckIf(ListsModel("timeline-obsolete-model") || agentModelPicker.Problem.Length != 0, "The recovered model picker must keep only the current model list.");
                }
                finally
                {
                    releaseModels.TrySetResult();
                    await modelRead.WaitAsync(TimeSpan.FromSeconds(5));
                    host.ReleaseModels = null;
                    host.FailModels = false;
                }
            }

            foreach (var lateFailure in new[] { false, true })
            {
                observationSession.ReplaceRegistration(registration);
                ResetDisplayedWorldContext();
                host.Reconnect = World(20, 20, 2, paused: true);
                await RefreshAsync();
                selectedAgentId = agentId;
                developerEditKind.Select(0);
                ConfigureDeveloperEdit();
                developerEditAmount.Value = 50;
                host.DeveloperEditReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseEdit = host.ReleaseDeveloperEdit = new(TaskCreationOptions.RunContinuationsAsynchronously);
                host.DeveloperEditReceipt = new("developer-edit", true, true, 20, 0, 0);
                host.FailDeveloperEdit = lateFailure;
                var editing = ApplyDeveloperEditAsync();
                try
                {
                    await Task.WhenAny(host.DeveloperEditReceived.Task, editing).WaitAsync(TimeSpan.FromSeconds(5));
                    FailUiCheckIf(!host.DeveloperEditReceived.Task.IsCompleted || editing.IsCompleted || !isOwnerAction ||
                        await host.DeveloperEditReceived.Task != new OwnerDeveloperEditAction(worldId, 2, agentId, "set_need", "fullness", 50), "The developer-edit check must hold the actual signed selected-agent request at the host.");
                    host.Reconnect = World(21, 5, 1, paused: true);
                    await RefreshAsync();
                    await RefreshAsync();
                    FailUiCheckIf(observationSession.AwaitingFreshBaseline || observationSession.Timeline?.Generation != 21, "The developer-edit check must accept its replacement baseline before releasing the old reply.");
                    SetStatus("Replacement world confirmed.", good: true);
                    var replacementStatus = statusLabel.Text;
                    releaseEdit.SetResult();
                    await editing.WaitAsync(TimeSpan.FromSeconds(5));
                    FailUiCheckIf(statusLabel.Text != replacementStatus || isOwnerAction || selectedAgentId is not null ||
                        observationSession.Timeline?.Generation != 21 || observationSession.Current?.Baseline.Snapshot.WorldTick != 5, "A late developer-edit success or failure must not replace the new timeline's status, selection or baseline.");
                }
                finally
                {
                    releaseEdit.TrySetResult();
                    await editing.WaitAsync(TimeSpan.FromSeconds(5));
                    host.ReleaseDeveloperEdit = null;
                    host.FailDeveloperEdit = false;
                }
            }
        }
        finally
        {
            CancelAutosaveSettingsRead();
            CancelManualSaveListRead();
            manualSaveOverlay.Hide();
            allListedManualSaves = [];
            listedManualSaves = [];
            manualSaveList.Clear();
            ResetDisplayedWorldContext();
            registration = previousRegistration;
            deviceKey = previousKey;
            serverUrlInput.Text = previousUrl;
            observationSession.ReplaceRegistration(previousRegistration);
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            selectedAgentId = previousSelection;
            pendingSubmission = previousPendingSubmission;
            providerConfiguration = previousProviderConfiguration;
            RenderPendingSubmission();
            pauseMenuPanel.Visible = previousMenuVisible;
            settingsPanel.Visible = previousSettingsVisible;
            worldSettingsContent.Visible = previousWorldSettingsVisible;
            gameSettingsContent.Visible = previousGameSettingsVisible;
            isInWorld = previousInWorld;
            mainMenuOverlay.Visible = previousMainMenuVisible;
            worldMenuOverlay.Visible = previousWorldMenuVisible;
            resumeWorldOnContinue = previousResumeOnContinue;
            menuPausedWorld = previousMenuPausedWorld;
            choosingFirstTownSite = previousTownSiteMode;
            agentModelApiKeyInput.Text = previousModelKey;
            agentModelRoleChoice.Select(previousRole);
            if (previousModelTyping) agentModelPicker.ShowTypedOnly(previousModel);
            else agentModelPicker.SetModel(previousModel);
            developerEditKind.Select(previousDeveloperKind);
            ConfigureDeveloperEdit();
            developerEditValue.Select(previousDeveloperValue);
            developerEditAmount.Value = previousDeveloperAmount;
            System.Environment.SetEnvironmentVariable("CI", previousCi);
            RefreshControlAvailability();
            statusToast.Hide();
        }
    }

    private async Task WaitForTimelineSmokeAsync(Func<bool> ready, string failure)
    {
        var deadline = System.Environment.TickCount64 + 5_000;
        while (!ready())
        {
            if (System.Environment.TickCount64 >= deadline) throw new InvalidOperationException(failure);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
