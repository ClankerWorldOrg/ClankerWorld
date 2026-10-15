using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Pairing;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private long renderedObservationGeneration = -1;

    private async Task InitializeAsync()
    {
        try
        {
            deviceKey = OwnerDeviceKey.OpenOrCreate(GameStarter.UsesTestData(OS.GetCmdlineUserArgs())
                ? new OwnerDeviceKeyOptions(OwnerDeviceKey.DefaultKeyName + ".TestData") : null);
            registration = registrationStore.TryLoad(deviceKey.PublicKeyFingerprint);
            if (registration is null)
            {
                RefreshMainMenuAvailability();
                return;
            }

            // Once paired, this device is pinned to the server origin that
            // issued the registration. A command-line URL is useful only for
            // a first pairing; it must never silently retarget an owner key.
            if (!ServerOrigin.TryResolve(registration.WorldUrl, out var storedWorldUri))
            {
                registeredEndpointInvalid = true;
                pairingPanel.Show();
                OpenMenuForSetup();
                pairingInstructionLabel.Text = "This PC's saved connection has no valid world server address. Choose Forget saved connection, then pair again with the right world server.";
                SetStatus("This PC's saved connection is not valid. Pair this device again.", good: false);
                RefreshControlAvailability();
                return;
            }

            serverUrlInput.Text = storedWorldUri.AbsoluteUri;
            LoadPendingSubmission();

            pairingPanel.Hide();
            settingsPanel.Hide();
            ClosePauseMenu();
            RefreshMainMenuAvailability();
            SetStatus("This device is paired. Choose Continue to enter your world.", good: true);
            await CheckStartupRecoveryAsync();
        }
        catch (Exception exception)
        {
            pairingPanel.Show();
            OpenMenuForSetup();
            SetStatus($"Could not open this PC's device key · {FriendlyFailure(exception)}", good: false);
            pairingInstructionLabel.Text = "Pairing needs the key store for your Windows account on this PC. The game never saves the device key as a file that could be copied.";
            pairButton.Disabled = true;
        }
    }

    private async Task PulseAsync()
    {
        ExpireStatusToast(refreshSucceeded: false);
        if (registration is not null && !registeredEndpointInvalid)
            _ = RefreshSaveDiskSpaceAsync();
        else saveDiskWarningPanel.Hide();
        if (pendingPairing is not null)
        {
            await PollPairingAsync();
            return;
        }

        if (isInWorld && registration is not null && !registeredEndpointInvalid)
        {
            await RefreshAsync();
        }
    }

    private async Task StartPairingAsync()
    {
        if (isPairingOperation || pendingPairing is not null || deviceKey is null)
        {
            return;
        }

        isPairingOperation = true;
        RefreshControlAvailability();
        try
        {
            var origin = ResolveWorldUri();
            // The game's own host pairs on its local listener with this
            // launch's secret; any other server keeps the public route.
            var ownServer = localServer is not null && localServer.Serves(origin);
            pendingPairing = ownServer
                ? await localServer!.StartPairingAsync(deviceKey, CancellationToken.None)
                : await ownerApi.StartPairingAsync(origin, deviceKey, CancellationToken.None);
            pendingPairingOrigin = origin;
            if (ownServer && await ApproveLocalPairingAsync(pendingPairing))
            {
                await ActivatePendingPairingAsync();
                return;
            }
            pairingPanel.Show();
            pairingInstructionLabel.Text = "Give the person who runs the world server the pairing ID and comparison code below. They approve this device on the server's own computer. The code is not a password.";
            pairingCodeLabel.Text = pendingPairing.PairingCode;
            pairingIdLabel.Text = pendingPairing.PairingId;
            pairingExpiryLabel.Text = $"Expires {pendingPairing.ExpiresAtUtc.LocalDateTime:yyyy-MM-dd HH:mm:ss}";
            pairButton.Text = "Start fresh pairing";
            SetStatus("Waiting for the person who runs the world server to approve this device.", good: true);
            _ = RevealPairingPanelAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"Could not start pairing · {FriendlyFailure(exception)}", good: false);
        }
        finally
        {
            isPairingOperation = false;
            RefreshControlAvailability();
        }
    }

    // The comparison code sits below the display settings; scroll it into
    // view once the settings page has laid out the newly shown panel.
    private async Task RevealPairingPanelAsync()
    {
        for (var frame = 0; frame < 2; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (pairingPanel.IsVisibleInTree()) settingsScroll.EnsureControlVisible(pairingPanel);
    }

    private async Task PollPairingAsync()
    {
        if (isPairingOperation || pendingPairing is null || deviceKey is null)
        {
            return;
        }

        isPairingOperation = true;
        try
        {
            var status = await ownerApi.GetPairingStatusAsync(
                ResolveWorldUri(),
                pendingPairing.PairingId,
                CancellationToken.None);
            if (!Equals(status.Authority, pendingPairing.Authority) ||
                !string.Equals(status.DeviceId, pendingPairing.DeviceId, StringComparison.Ordinal) ||
                !string.Equals(status.PublicKeyFingerprint, deviceKey.PublicKeyFingerprint, StringComparison.Ordinal))
            {
                pendingPairingOrigin = null;
                pendingPairing = null;
                SetStatus("The pairing reply did not match this device and world server. Start a fresh pairing.", good: false);
                return;
            }

            switch (status.State)
            {
                case OwnerPairingState.Pending:
                    pairingInstructionLabel.Text = "Waiting for approval. Give the person who runs the world server the pairing ID and comparison code exactly as shown.";
                    break;
                case OwnerPairingState.Approved:
                    pairingInstructionLabel.Text = "Approved. Checking this PC's device key...";
                    await ActivatePendingPairingAsync();
                    break;
                case OwnerPairingState.Active:
                    // The process can be interrupted after a successful
                    // server activation but before its non-secret local
                    // registration is flushed. Recover only when the active
                    // record remains bound to this exact Windows key.
                    if (string.Equals(status.DeviceId, pendingPairing.DeviceId, StringComparison.Ordinal) &&
                        string.Equals(status.PublicKeyFingerprint, deviceKey.PublicKeyFingerprint, StringComparison.Ordinal) &&
                        Equals(status.Authority, pendingPairing.Authority))
                    {
                        registration = new OwnerDeviceRegistration(
                            status.Authority,
                            status.DeviceId,
                            deviceKey.PublicKeyFingerprint,
                            ResolveWorldUri().AbsoluteUri);
                        registrationStore.Save(registration);
                        LoadPendingSubmission();
                        pendingPairing = null;
                        pendingPairingOrigin = null;
                        pairingPanel.Hide();
                        settingsPanel.Hide();
                        ClosePauseMenu();
                        ShowMainMenu();
                        SetStatus("Device paired. Choose Continue to enter your world.", good: true);
                        await CheckStartupRecoveryAsync();
                    }
                    else
                    {
                        pairingInstructionLabel.Text = "This pairing is already active for a different device key. Ask the person who runs the world server to remove it, then pair again.";
                        SetStatus("That active pairing belongs to a different device key.", good: false);
                    }

                    break;
                case OwnerPairingState.Expired:
                    pairingInstructionLabel.Text = "The comparison code expired. Start a fresh pairing to get a new short code.";
                    pendingPairing = null;
                    pendingPairingOrigin = null;
                    SetStatus("Pairing expired.", good: false);
                    break;
                default:
                    SetStatus("The world server sent a pairing status this game doesn't know. Check that the game and server are up to date.", good: false);
                    break;
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Could not check the pairing · {FriendlyFailure(exception)}", good: false);
        }
        finally
        {
            isPairingOperation = false;
            RefreshControlAvailability();
        }
    }

    private async Task ActivatePendingPairingAsync()
    {
        if (pendingPairing is null || deviceKey is null)
        {
            return;
        }

        var pairing = pendingPairing;
        try
        {
            var device = await ownerApi.ActivatePairingAsync(
                ResolveWorldUri(),
                pairing,
                deviceKey,
                CancellationToken.None);
            registration = new OwnerDeviceRegistration(
                pairing.Authority,
                device.DeviceId,
                deviceKey.PublicKeyFingerprint,
                ResolveWorldUri().AbsoluteUri);
            registrationStore.Save(registration);
            LoadPendingSubmission();
            pendingPairing = null;
            pendingPairingOrigin = null;
            pairingPanel.Hide();
            settingsPanel.Hide();
            ClosePauseMenu();
            ShowMainMenu();
            SetStatus("Device paired. Choose Continue to enter your world.", good: true);
            await CheckStartupRecoveryAsync();
        }
        catch (Exception exception)
        {
            SetStatus($"Pairing was approved, but this PC could not finish it · {FriendlyFailure(exception)}", good: false);
        }
    }

    private void ForgetLocalRegistration()
    {
        refreshCancellation?.Cancel();
        registrationStore.Forget();
        registration = null;
        registeredEndpointInvalid = false;
        pendingPairing = null;
        pendingPairingOrigin = null;
        pairedDevices = [];
        providerConfiguration = null;
        pairedDeviceList.Clear();
        pendingSubmission = null;
        _ = pendingSubmissionStore.TryForget();
        unloadedPendingSubmission = pendingSubmissionStore.HasRecord;
        RenderPendingSubmission();
        knownEvents.Clear();
        pairingPanel.Show();
        connectionPanel.Show();
        OpenMenuForSetup();
        pairingCodeLabel.Text = "—";
        pairingIdLabel.Text = "—";
        pairingExpiryLabel.Text = string.Empty;
        pairingInstructionLabel.Text = "This PC's saved connection was forgotten. Its device key stays saved on this PC for your Windows account. Pair again only if the person who runs the world server allows this device.";
        SetStatus("Saved connection forgotten.", good: false);
        RefreshControlAvailability();
    }

    private async Task RefreshAsync()
    {
        if (isRefreshing || registeredEndpointInvalid || registration is null || deviceKey is null)
        {
            return;
        }

        isRefreshing = true;
        using var refresh = new CancellationTokenSource();
        refreshCancellation = refresh;
        var confirmed = false;
        try
        {
            var request = observationSession.CaptureRequest();
            var requestedCursor = request.AfterEventId;
            var cachedTerrain = !request.RequiresFullBaseline && observationSession.Current is { } held &&
                held.Handshake.ServerCapabilities.Contains("owner-terrain-delta.v1", StringComparer.Ordinal) &&
                held.Baseline.Snapshot.PackedTerrain is not null &&
                (held.Baseline.Snapshot.MapLayersDigest is null ||
                 held.Baseline.Snapshot.PackedMapLayers is not null)
                ? held.Baseline.Snapshot : null;
            var cachedMapLayersDigest = cachedTerrain is not null &&
                observationSession.Current!.Handshake.ServerCapabilities.Contains(
                    "owner-map-layer-delta.v1", StringComparer.Ordinal)
                ? cachedTerrain.MapLayersDigest : null;
            var reconnect = await ownerApi.ReconnectAsync(
                ResolveWorldUri(),
                registration.Authority,
                registration.DeviceId,
                requestedCursor,
                cachedTerrain?.WorldId,
                cachedTerrain?.MapManifestDigest,
                cachedMapLayersDigest,
                deviceKey,
                refresh.Token);
            // An owner action or shutdown superseded this snapshot.
            if (refresh.IsCancellationRequested) return;
            if (!observationSession.TryAccept(reconnect, request, out var failure, out var timelineChanged))
            {
                if (observationSession.AwaitingFreshBaseline)
                {
                    CancelAutosaveSettingsRead();
                    CancelManualSaveListRead();
                    worldListRequest.Cancel();
                }
                ShowHeldState(failure);
                return;
            }

            var sameContext = !timelineChanged && renderedObservationGeneration == observationSession.RequestGeneration;
            if (!sameContext)
            {
                ResetDisplayedWorldContext();
            }
            else if (reconnect.Baseline.Events.ResetRequired)
            {
                knownEvents.Clear();
            }
            using (var draft = sameContext ? new PanelDraft(this) : null)
                Render(observationSession.Current!.Baseline.Snapshot, reconnect.Baseline.Events.Events);
            renderedObservationGeneration = observationSession.RequestGeneration;
            successfulRefreshCount++;
            if (!isOwnerAction)
            {
                if (usageStatus?.LimitReached == true && reconnect.Baseline.Snapshot.Authoring?.IsPaused == true)
                {
                    SetStatus(UsageLimitPausedMessage, good: false, StatusToastKind.UsageLimit);
                }
                else
                {
                    ExpireStatusToast(refreshSucceeded: true);
                }
            }
            confirmed = true;
        }
        catch (OperationCanceledException) when (refresh.IsCancellationRequested)
        {
            // Superseded refresh is not a connection failure.
        }
        catch (System.Net.Http.HttpRequestException exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            if (!await CheckStartupRecoveryAsync()) ShowHeldState(FriendlyFailure(exception));
        }
        catch (Exception exception)
        {
            ShowHeldState(FriendlyFailure(exception));
        }
        finally
        {
            refreshCancellation = null;
            isRefreshing = false;
            RefreshControlAvailability();
        }
        if (confirmed) await RecoverPendingSubmissionAsync();
    }

    private bool IsCurrentWorldRequest(long generation) =>
        generation == observationSession.RequestGeneration && !observationSession.AwaitingFreshBaseline;

    private readonly AsyncLocal<OwnerRequestContext?> ownerRequestContext = new();
    private CancellationToken OwnerRequestToken => ownerRequestContext.Value?.CancellationToken ?? CancellationToken.None;

    // The context is captured by the runner before anything is sent.
    private Task<T> AwaitCurrentWorldResultAsync<T>(Task<T> request) =>
        (ownerRequestContext.Value ?? CaptureOwnerRequestContext()).AwaitAsync(request);

    private Task AwaitCurrentWorldResultAsync(Task request) =>
        (ownerRequestContext.Value ?? CaptureOwnerRequestContext()).AwaitAsync(request);

    private OwnerRequestContext CaptureOwnerRequestContext()
    {
        var generation = observationSession.RequestGeneration;
        return new OwnerRequestContext(() => IsCurrentWorldRequest(generation), OwnerRequestToken);
    }

    private async Task ConnectUsingCurrentUrlAsync()
    {
        if (registeredEndpointInvalid)
        {
            connectionStatusLabel.Text = "This device's saved connection is not valid. Ask the person who runs the world server before pairing again.";
            connectionStatusLabel.Show();
            SetStatus("This device's saved connection is not valid. Choose Forget saved connection before pairing again.", good: false);
            return;
        }

        try
        {
            _ = ResolveWorldUri();
        }
        catch (Exception exception)
        {
            var failure = FriendlyFailure(exception);
            connectionStatusLabel.Text = $"That world server address can't be used · {failure}";
            connectionStatusLabel.Show();
            SetStatus($"That world server address can't be used · {failure}", good: false);
            return;
        }

        if (registration is not null)
        {
            if (isRefreshing || isOwnerAction)
            {
                connectionStatusLabel.Text = "A connection check is already in progress.";
                connectionStatusLabel.Show();
                return;
            }

            var previousRefreshCount = successfulRefreshCount;
            var previousToastTime = statusToastShownAtMsec;
            connectionStatusLabel.Text = "Checking connection...";
            connectionStatusLabel.Show();
            connectButton.Disabled = true;
            await RefreshAsync();
            connectionStatusLabel.Text = successfulRefreshCount > previousRefreshCount
                ? "Connected. Go back to open your world."
                : statusToastKind == StatusToastKind.Connection && statusToastShownAtMsec != previousToastTime
                    ? statusLabel.Text
                    : "Could not confirm the connection. Try again.";
            return;
        }

        await StartPairingAsync();
    }

    private async Task PairAgainAsync()
    {
        if (isPairingOperation || isOwnerAction || isRefreshing)
        {
            return;
        }

        ForgetLocalRegistration();
        await StartPairingAsync();
    }

    private Uri ResolveWorldUri()
    {
        if (!ServerOrigin.TryResolve(serverUrlInput.Text, out var configuredWorldUri))
        {
            throw new InvalidOperationException("World URL must be an absolute HTTPS origin (or loopback HTTP for local development).");
        }

        if (registration is null)
        {
            if (pendingPairingOrigin is not null)
            {
                if (!ServerOrigin.Same(configuredWorldUri, pendingPairingOrigin))
                {
                    throw new InvalidOperationException("This pending pairing is pinned to the server that created it. Wait for it to expire or forget the local registration before changing servers.");
                }

                return pendingPairingOrigin;
            }

            return configuredWorldUri;
        }

        if (!ServerOrigin.TryResolve(registration.WorldUrl, out var pinnedWorldUri) ||
            !ServerOrigin.Same(configuredWorldUri, pinnedWorldUri))
        {
            throw new InvalidOperationException("This paired device is pinned to its original server origin. Forget the local registration before pairing it with a different server.");
        }

        return pinnedWorldUri;
    }

    private static string ConfiguredServerUrl() => ProjectSettings
        .GetSetting("clankerworld/server_url", "http://127.0.0.1:5188")
        .AsString();

    private static bool TryGetCommandLineServerUrl(out string? serverUrl)
    {
        var argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--server-url=", StringComparison.Ordinal));
        serverUrl = argument is null ? null : argument["--server-url=".Length..];
        return !string.IsNullOrWhiteSpace(serverUrl);
    }

    private enum StatusToastKind
    {
        // Action results and hints stay readable for a few refreshes.
        Message,
        // Instructions for an active map-click mode stay until replaced.
        Sticky,
        // Connection trouble clears on the next successful refresh.
        Connection,
        UsageLimit,
    }

}
