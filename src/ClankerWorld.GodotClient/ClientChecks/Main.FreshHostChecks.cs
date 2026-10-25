using System.Text.Json;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.Launcher;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyPairingRecoveryMenuAsync()
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousUrl = serverUrlInput.Text;
        var previousObservation = observationSession.Current;
        var previousJoin = localJoin;
        var previousInWorld = isInWorld;
        var previousResume = resumeWorldOnContinue;
        var previousLocalStarting = localServerStarting;
        var previousRequestedWorld = requestedWorldToSelect;
        var registrationPath = GameUserPath((localServer is null ? "" : "local-") + "owner-device-registration.json");
        var previousRegistrationFile = File.Exists(registrationPath) ? File.ReadAllBytes(registrationPath) : null;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        var expiry = DateTimeOffset.UtcNow.AddMinutes(1);
        host.PairingStart = new(host.Authority, "menu-pairing", "smoke-device", "fixture-code",
            signer.PublicKeyFingerprint, expiry, OwnerPairingProtocol.CreatePairingActivationCanonicalProof(
                host.Authority, "menu-pairing", "smoke-device", signer.PublicKeyFingerprint));
        var approved = new OwnerPairingStatus(host.Authority, "menu-pairing", "smoke-device",
            signer.PublicKeyFingerprint, OwnerPairingState.Approved, expiry);
        host.Reconnect = new(new(new(1, 1),
            ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
             "owner-control.request.v1", "paused-authoring.request.v1"], []),
            new(new("paired-world", 0, "paired-map", [new(0, 0, "meadow")], [], [], null, 0), new(0, 0, [])));
        int Reconnects() => host.Requests.Count(request => request == OwnerPairingEndpoints.OwnerReconnect);
        void AssertWaiting()
        {
            if (!mainMenuContinueButton.Disabled || !mainMenuNewButton.Disabled || !mainMenuLoadButton.Disabled ||
                mainMenuStatus.Text != "Starting your world server…")
                throw new InvalidOperationException("World choices must wait for the local server and device setup.");
        }
        try
        {
            deviceKey = signer;
            serverUrlInput.Text = host.Address;
            resumeWorldOnContinue = false;
            foreach (var loseReply in new[] { true, false })
            {
                registration = null;
                registrationStore.Forget();
                localJoin = new DeviceJoinSession(ownerApi, signer, registrationStore);
                await localJoin.StartAsync(new Uri(host.Address), CancellationToken.None);
                observationSession.ResetAfterLoad();
                host.PairingStatus = approved;
                host.LoseActivationReply = loseReply;
                ShowMainMenu();
                localServerStarting = true;
                RefreshMainMenuAvailability();
                var reconnects = Reconnects();
                host.ActivationReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                host.ReleaseActivation = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var activating = FinishLocalDeviceSetupAsync();
                await host.ActivationReceived.Task.WaitAsync(TimeSpan.FromSeconds(3));
                AssertWaiting();
                if (activating.IsCompleted || registration is not null)
                    throw new InvalidOperationException("Local startup must wait through the actual signed activation reply.");
                host.ReleaseActivation.TrySetResult();
                await activating;
                if (loseReply)
                {
                    if (registration is not null || localJoin.Pending is null)
                        throw new InvalidOperationException("A lost activation reply must remain available for recovery.");
                    await FinishLocalDeviceSetupAsync();
                }
                CompleteLocalServerStartup();
                if (registration is null || registrationStore.TryLoad(signer.PublicKeyFingerprint) != registration ||
                    localJoin.Pending is not null || localJoin.PendingOrigin is not null || isInWorld ||
                    !mainMenuOverlay.Visible || !mainMenuContinueButton.IsVisibleInTree() || mainMenuContinueButton.Disabled ||
                    !mainMenuSettingsButton.IsVisibleInTree() || !quitGameButton.IsVisibleInTree() || mainMenuStatus.Visible ||
                    Reconnects() != reconnects)
                    throw new InvalidOperationException("Completed local setup must restore usable Main Menu controls without entering the world.");
                localServerStarting = true;
                RefreshMainMenuAvailability();
                AssertWaiting();
                CompleteLocalServerStartup();
                requestedWorldToSelect = "0123456789abcdef0123456789abcdef";
                await EnterWorldAsync();
                if (!isInWorld || mainMenuOverlay.Visible || requestedWorldToSelect is not null ||
                    observationSession.Current?.Baseline.Snapshot.WorldId != "paired-world" || Reconnects() != reconnects + 1)
                    throw new InvalidOperationException("Continue after local setup must enter the recovered world through signed HTTP.");
            }
            if (FindChildren("*", nameof(Button), true, false).OfType<Button>().Any(button =>
                button.IsVisibleInTree() && button.Text is "Connect to a world server" or "Start pairing" or "Pair again"))
                throw new InvalidOperationException("The game must leave joining and pairing in the launcher.");
        }
        finally
        {
            host.ReleaseActivation?.TrySetResult();
            localJoin = previousJoin;
            localServerStarting = previousLocalStarting;
            registration = previousRegistration;
            if (previousRegistrationFile is null) registrationStore.Forget();
            else File.WriteAllBytes(registrationPath, previousRegistrationFile);
            deviceKey = previousKey;
            serverUrlInput.Text = previousUrl;
            observationSession.ResetAfterLoad();
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            ClosePauseMenu();
            ShowMainMenu();
            isInWorld = previousInWorld;
            requestedWorldToSelect = previousRequestedWorld;
            resumeWorldOnContinue = previousResume;
            System.Environment.SetEnvironmentVariable("CI", previousCi);
            RefreshControlAvailability();
            statusToast.Hide();
        }
    }

    /// <summary>Entering an untouched host starts the normal creation flow without exposing or resuming its bootstrap world.</summary>
    private async Task VerifyFreshHostEntryAsync()
    {
        var previousConnection = CaptureOwnerCheckConnection();
        var previousObservation = observationSession.Current;
        var previousInWorld = isInWorld;
        var previousResume = resumeWorldOnContinue;
        var previousMenuVisible = mainMenuOverlay.Visible;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        var handshake = new OwnerWorldHandshake(new(1, 1),
            ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
             "owner-control.request.v1", "paused-authoring.request.v1"], []);
        var bootstrap = new OwnerWorldSnapshot("bootstrap", 0, "entry-map", [new(0, 0, "meadow")],
            [new("cooking", "cooking", new(0, 0))], [], null, 0)
        {
            FounderSetup = new(4, 0, false) { RequiresWorldCreation = true },
            Authoring = new(true, 0, 0, 0, "entry-map", "entry-map", "clear", "spring", []),
        };
        var generated = bootstrap with
        {
            WorldId = "generated",
            Objects = [],
            FounderSetup = new(4, 0, false) { CanChooseTownSite = true },
        };
        OwnerWorldReconnect Reply(OwnerWorldSnapshot snapshot) => new(handshake, new(snapshot, new(0, snapshot.WorldTick, [])));
        int Requests(string path) => host.Requests.Count(request => request == path);
        async Task WaitForPreviewAsync()
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (worldMenuBusy || previewedWorldResult is null)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                    throw new InvalidOperationException("Entering New World must finish its normal map preview.");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        try
        {
            ConnectOwnerCheck(host, signer);
            host.SupportedActionPayloads = [OwnerWorldActionPayload.WorldCreationPayloadDomain, OwnerWorldActionPayload.WorldControlPayloadDomain];
            host.Preview = new(new(1, 1, "terrain-kind-v1", "AA=="), new(0, 0), "created-manifest")
            {
                MapLayersDigest = "created-layers",
                Coverage = new(2, 1, 0, 0, 0, 0, 0, 0, 0, 0, false, false, true, true),
            };
            host.ReleasePause.TrySetResult();
            host.ReleaseSelect.TrySetResult();
            host.Catalog = new("bootstrap-entry", [new("bootstrap-entry", "Earlier host", bootstrap.WorldId, "bootstrap-seed",
                DateTimeOffset.UnixEpoch, [], null, "compatible")]);
            foreach (var directFromMenu in new[] { true, false })
            {
                host.Reconnect = Reply(bootstrap);
                observationSession.ResetAfterLoad();
                await worldListRequest.RefreshAsync(token =>
                {
                    worldListRequest.Cancel();
                    return Task.FromCanceled<WorldCatalogSnapshot>(token);
                });
                ShowMainMenu();
                worldMenuOverlay.Hide();
                resumeWorldOnContinue = !directFromMenu;

                if (directFromMenu)
                {
                    if (observationSession.Current is not null || worldListRequest.Catalog is not null)
                        throw new InvalidOperationException("Direct New World must start with neither an observation nor a catalog.");
                    OpenWorldMenu(create: true);
                }
                else await EnterWorldAsync();
                await WaitForPreviewAsync();
                if (isInWorld || resumeWorldOnContinue || !mainMenuOverlay.Visible || !worldMenuOverlay.Visible ||
                    worldMenuHeading.Text != "New World" || !worldMenuColumns.Visible || !worldPreview.Visible ||
                    worldCreateButton.Disabled || mainMenuContinueButton.Disabled || mainMenuNewButton.Disabled ||
                    mainMenuLoadButton.Disabled || Requests(OwnerPairingEndpoints.OwnerResume) != 0)
                    throw new InvalidOperationException("Continue on an untouched host must keep the bootstrap world behind the menus and offer New World without resuming.");

                // Creation still submits the candidate the player actually previewed, then enters founder setup.
                host.CreatedWorld = new("created-entry", "New World", generated.WorldId, worldSeedInput.Text,
                    DateTimeOffset.UnixEpoch, [], null, "compatible");
                host.CreatedObservation = Reply(generated);
                var catalogReads = host.CatalogReadCount;
                await CreateSelectedWorldAsync();
                if (!isInWorld || mainMenuOverlay.Visible || worldMenuOverlay.Visible || resumeWorldOnContinue ||
                    observationSession.Current?.Baseline.Snapshot.WorldId != generated.WorldId ||
                    host.WorldCreations.Count != (directFromMenu ? 1 : 2) ||
                    host.WorldCreations.LastOrDefault() is not { } creation || creation.CandidateAttempt != 2 ||
                    creation.ExpectedManifestDigest != "created-manifest" || creation.ExpectedMapLayersDigest != "created-layers" ||
                    host.CatalogReadCount != catalogReads + 1 || host.Controls.LastOrDefault()?.WorldId != bootstrap.WorldId ||
                    Requests(OwnerPairingEndpoints.OwnerResume) != 0)
                    throw new InvalidOperationException("The fresh-host route must preserve preview-bound creation and enter the generated founder setup without resuming it.");
                GD.Print($"Menu targeting smoke: {(directFromMenu ? "direct New World without observation or catalog" : "New World after Continue")} passed.");
            }

            // Selecting the still-untouched bootstrap from Load World uses the same entry boundary.
            ShowMainMenu();
            ShowWorldActionSmokeMenu();
            var selectedWorld = new CatalogWorld("bootstrap-entry", "Earlier host", bootstrap.WorldId, "bootstrap-seed",
                DateTimeOffset.UnixEpoch, [], null, "compatible");
            host.SelectedWorld = selectedWorld;
            host.Catalog = new(selectedWorld.Id, [selectedWorld, host.CreatedWorld!]);
            host.Reconnect = Reply(bootstrap);
            await RefreshWorldListAsync();
            if (observationSession.Current?.Baseline.Snapshot.WorldId != generated.WorldId ||
                worldListRequest.Catalog?.ActiveId != selectedWorld.Id)
                throw new InvalidOperationException("Load World must retain the earlier observation beside the newer active catalog.");
            ChooseWorldActionSmokeRow(0);
            await SelectListedWorldAsync();
            await WaitForPreviewAsync();
            if (isInWorld || !mainMenuOverlay.Visible || !worldMenuOverlay.Visible ||
                worldMenuHeading.Text != "New World" || resumeWorldOnContinue ||
                host.Controls.LastOrDefault()?.WorldId != bootstrap.WorldId ||
                Requests(OwnerPairingEndpoints.OwnerResume) != 0)
                throw new InvalidOperationException("Load World must route an untouched bootstrap to New World too.");
            GD.Print("Menu targeting smoke: Load World uses the current catalog over a retained observation passed.");

            // A failed read must not act on the bootstrap flag in the previously held observation.
            worldMenuOverlay.Hide();
            var previewRequests = Requests(OwnerPairingEndpoints.OwnerWorldPreview);
            var refreshes = successfulRefreshCount;
            host.Reconnect = null;
            resumeWorldOnContinue = true;
            await EnterWorldAsync();
            if (isInWorld || worldMenuOverlay.Visible || !mainMenuOverlay.Visible || !resumeWorldOnContinue ||
                mainMenuContinueButton.Disabled || successfulRefreshCount != refreshes ||
                Requests(OwnerPairingEndpoints.OwnerWorldPreview) != previewRequests ||
                Requests(OwnerPairingEndpoints.OwnerResume) != 0 ||
                !mainMenuStatus.Text.StartsWith("Could not reach your world.", StringComparison.Ordinal))
                throw new InvalidOperationException("A failed Continue refresh must retain the menu and retry state without opening New World or resuming.");

            // Older hosts omit the new property; existing/progressed worlds also keep Continue's resume behavior.
            var olderSetup = JsonSerializer.Deserialize<OwnerFounderSetup>(
                "{\"required\":4,\"placed\":0,\"started\":false}", CompatibilitySmokeJsonOptions)!;
            foreach (var setup in new[] { olderSetup, new OwnerFounderSetup(4, 4, true) })
            {
                ShowMainMenu();
                observationSession.ResetAfterLoad();
                host.Reconnect = Reply(generated with { FounderSetup = setup });
                resumeWorldOnContinue = setup.Started;
                var resumes = Requests(OwnerPairingEndpoints.OwnerResume);
                await EnterWorldAsync();
                if (!isInWorld || mainMenuOverlay.Visible || worldMenuOverlay.Visible || resumeWorldOnContinue ||
                    Requests(OwnerPairingEndpoints.OwnerWorldPreview) != previewRequests ||
                    Requests(OwnerPairingEndpoints.OwnerResume) != resumes + (setup.Started ? 1 : 0))
                    throw new InvalidOperationException("A host without the new flag and an existing world must continue normally.");
            }
        }
        finally
        {
            worldListRequest.Cancel();
            worldMenuOverlay.Hide();
            InvalidateWorldPreview(refresh: false);
            listedWorlds = [];
            listedActiveWorldId = null;
            worldSelectionList.Clear();
            RestoreOwnerCheckConnection(previousConnection);
            observationSession.ResetAfterLoad();
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            isInWorld = previousInWorld;
            resumeWorldOnContinue = previousResume;
            mainMenuOverlay.Visible = previousMenuVisible;
            System.Environment.SetEnvironmentVariable("CI", previousCi);
            RefreshControlAvailability();
            RefreshMainMenuAvailability();
            statusToast.Hide();
        }
    }

    /// <summary>Late Continue replies cannot resume a world after menu navigation.</summary>
    private async Task VerifyContinueSettingsNavigationAsync()
    {
        var previousConnection = CaptureOwnerCheckConnection();
        var previousObservation = observationSession.Current;
        var previousInWorld = isInWorld;
        var previousResume = resumeWorldOnContinue;
        var previousMenuVisible = mainMenuOverlay.Visible;
        var previousReturnToMainMenu = returnToMainMenu;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        host.ReleasePause.TrySetResult();
        var handshake = new OwnerWorldHandshake(new(1, 1),
            ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
             "owner-control.request.v1", "paused-authoring.request.v1"], []);
        var snapshot = new OwnerWorldSnapshot("continue-probe", 0, "continue-map", [new(0, 0, "meadow")],
            [], [], null, 0)
        {
            FounderSetup = new(4, 4, true),
            Authoring = new(true, 0, 0, 0, "continue-map", "continue-map", "clear", "spring", []),
        };
        host.Reconnect = new(handshake, new(snapshot, new(0, snapshot.WorldTick, [])));
        int Requests(string path) => host.Requests.Count(p => p == path);
        void Log(string stage) => GD.Print($"Continue navigation smoke {stage}: title={mainMenuOverlay.Visible && mainMenuCard.Visible}; settings={pauseMenuPanel.Visible}; inWorld={isInWorld}; returnToMainMenu={returnToMainMenu}; hostPaused={host.HostPaused}; pauses={host.PauseCount}; resumes={Requests(OwnerPairingEndpoints.OwnerResume)}");
        try
        {
            ConnectOwnerCheck(host, signer);
            observationSession.ResetAfterLoad();
            ShowMainMenu();
            worldMenuOverlay.Hide();
            resumeWorldOnContinue = true;
            await SetPausedAsync(paused: true);
            mainMenuSettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            menuCloseButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Log("settings-only-control");
            if (!mainMenuCard.Visible || isInWorld || !host.HostPaused)
                throw new InvalidOperationException("Settings-only control failed.");

            host.ReleaseReconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
            host.ReconnectReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var enter = EnterWorldAsync();
            await host.ReconnectReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Log($"waiting ContinueDisabled={mainMenuContinueButton.Disabled} SettingsDisabled={mainMenuSettingsButton.Disabled}");
            host.ReleaseReconnect.TrySetResult();
            await enter;
            Log("continue-only-control");
            if (!isInWorld || host.HostPaused || mainMenuOverlay.Visible)
                throw new InvalidOperationException("Ordinary Continue control failed.");

            await SetPausedAsync(paused: true);
            ShowMainMenu();
            resumeWorldOnContinue = true;
            host.ReleaseReconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
            host.ReconnectReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            enter = EnterWorldAsync();
            await host.ReconnectReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            mainMenuSettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Log("settings-during-continue");
            host.ReleaseReconnect.TrySetResult();
            await enter;
            Log("late-continue");
            if (isInWorld || !pauseMenuPanel.Visible || !host.HostPaused ||
                Requests(OwnerPairingEndpoints.OwnerResume) != 1)
                throw new InvalidOperationException("A Continue completed behind Settings must retain the confirmed pause.");
            var pausesBeforeBack = host.PauseCount;
            menuCloseButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Log($"settings-back additionalPauses={host.PauseCount - pausesBeforeBack}");
            if (isInWorld || !mainMenuCard.Visible || !mainMenuOverlay.Visible || !host.HostPaused ||
                mainMenuContinueButton.Disabled || Requests(OwnerPairingEndpoints.OwnerResume) != 1)
                throw new InvalidOperationException("Settings Back must return to a usable paused Main Menu.");

            // Leaving Settings before the reply arrives must expire the old Continue too.
            host.ReleaseReconnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
            host.ReconnectReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            enter = EnterWorldAsync();
            await host.ReconnectReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            mainMenuSettingsButton.EmitSignal(BaseButton.SignalName.Pressed);
            menuCloseButton.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            host.ReleaseReconnect.TrySetResult();
            await enter;
            if (isInWorld || !mainMenuOverlay.Visible || !mainMenuCard.Visible || !host.HostPaused ||
                Requests(OwnerPairingEndpoints.OwnerResume) != 1)
                throw new InvalidOperationException("Settings Back cannot reactivate a Continue from before navigation.");

            // A fresh Continue after Back still enters and resumes exactly once.
            await EnterWorldAsync();
            if (!isInWorld || mainMenuOverlay.Visible || host.HostPaused ||
                Requests(OwnerPairingEndpoints.OwnerResume) != 2)
                throw new InvalidOperationException("A fresh Continue after Settings Back must resume normally.");
        }
        finally
        {
            host.ReleaseReconnect?.TrySetResult();
            ClosePauseMenu();
            RestoreOwnerCheckConnection(previousConnection);
            observationSession.ResetAfterLoad();
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            isInWorld = previousInWorld;
            resumeWorldOnContinue = previousResume;
            returnToMainMenu = previousReturnToMainMenu;
            mainMenuOverlay.Visible = previousMenuVisible;
            System.Environment.SetEnvironmentVariable("CI", previousCi);
            RefreshControlAvailability();
            RefreshMainMenuAvailability();
            statusToast.Hide();
        }
    }
}
