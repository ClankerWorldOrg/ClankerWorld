using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Pairing;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private long renderedObservationGeneration = -1;
    private DeviceJoinSession? localJoin;
    private bool settingUpLocalDevice;

    private async Task InitializeAsync()
    {
        try
        {
            deviceKey = OwnerDeviceKey.OpenOrCreate(GameStarter.UsesTestData(OS.GetCmdlineUserArgs())
                ? new OwnerDeviceKeyOptions(OwnerDeviceKey.DefaultKeyName + ".TestData") : null);
            registration = registrationStore.TryLoad(deviceKey.PublicKeyFingerprint);
            if (registration is not null)
            {
                if (!ServerOrigin.TryResolve(registration.WorldUrl, out var stored) ||
                    (TryGetCommandLineServerUrl(out var requested) &&
                     (!ServerOrigin.TryResolve(requested, out var target) || !ServerOrigin.Same(stored, target))))
                {
                    registeredEndpointInvalid = true;
                    SetMainMenuStatus("Open the launcher and join the intended host again.");
                    return;
                }
                serverUrlInput.Text = stored.AbsoluteUri;
                LoadPendingSubmission();
            }
            RefreshMainMenuAvailability();
            if (registration is not null) await CheckStartupRecoveryAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetMainMenuStatus("This PC's device key could not be opened. Start ClankerWorld through the launcher and try again.");
        }
    }

    private async Task PulseAsync()
    {
        ExpireStatusToast(refreshSucceeded: false);
        if (registration is not null && !registeredEndpointInvalid) _ = RefreshSaveDiskSpaceAsync();
        else saveDiskWarningPanel.Hide();
        if (localJoin?.Pending is not null)
        {
            await FinishLocalDeviceSetupAsync();
            return;
        }
        if (isInWorld && registration is not null && !registeredEndpointInvalid) await RefreshAsync();
    }

    private async Task StartPairingAsync()
    {
        if (localServer is null || deviceKey is null || registration is not null || settingUpLocalDevice) return;
        localJoin ??= new DeviceJoinSession(ownerApi, deviceKey, registrationStore);
        try
        {
            if (localJoin.Pending is null)
                await localJoin.StartAsync(localServer.Origin, OwnerRequestToken,
                    () => localServer.StartPairingAsync(deviceKey, OwnerRequestToken));
            if (localJoin.Pending is { } pending && await ApproveLocalPairingAsync(pending))
                await FinishLocalDeviceSetupAsync();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { SetMainMenuStatus("This PC could not set up its world. Close the game and try again."); }
    }

    private async Task FinishLocalDeviceSetupAsync()
    {
        if (localJoin is null || settingUpLocalDevice) return;
        settingUpLocalDevice = true;
        try
        {
            if (await localJoin.PollAsync(OwnerRequestToken))
            {
                registration = localJoin.Registration;
                LoadPendingSubmission();
                RefreshMainMenuAvailability();
                await CheckStartupRecoveryAsync();
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { SetMainMenuStatus("This PC could not finish setting up its world. Close the game and try again."); }
        finally { settingUpLocalDevice = false; }
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
                eventLogRefusals.Clear();
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

    private Uri ResolveWorldUri()
    {
        if (!ServerOrigin.TryResolve(serverUrlInput.Text, out var configured))
            throw new InvalidOperationException("The host address is invalid. Join it through the launcher.");
        if (registration is not null && (!ServerOrigin.TryResolve(registration.WorldUrl, out var pinned) ||
            !ServerOrigin.Same(configured, pinned)))
            throw new InvalidOperationException("This device is approved for another host. Join through the launcher.");
        return configured;
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
