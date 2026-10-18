using ClankerWorld.Contracts;
using ClankerWorld.GodotClient.Networking;
using Godot;

namespace ClankerWorld.GodotClient.Launcher;

public partial class Launcher
{
    private readonly VBoxContainer hostingPage = new();
    private readonly Label hostingAddress = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label hostingState = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label hostingData = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer pendingDevices = new();
    private readonly Button startHosting = new() { Text = "Start hosting" };
    private readonly Button stopHosting = new() { Text = "Stop hosting" };
    private readonly Button hostingBack = new() { Text = "Back" };
    private readonly CheckButton resumeHosting = new() { Text = "Start hosting again when I sign in to Windows" };
    private readonly CheckButton stopWithGame = new() { Text = "Stop hosting when the game quits" };
    private readonly CancellationTokenSource hostingLifetime = new();
    private LocalServer? hostedServer;
    private Uri? hostedOrigin;
    private TailscaleHost? hostingNetwork;
    private bool hostingRunning;
    private bool refreshingHost;

    private void BuildHostPage()
    {
        hostingPage.AddThemeConstantOverride("separation", 8);
        hostingPage.AddChild(SectionLabel("Host a world for other devices"));
        hostingPage.AddChild(hostingAddress);
        hostingPage.AddChild(hostingState);
        hostingPage.AddChild(hostingData);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 100),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        pendingDevices.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pendingDevices.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(pendingDevices);
        hostingPage.AddChild(scroll);
        resumeHosting.SetPressedNoSignal(settings.ResumeHostingAtSignIn);
        resumeHosting.Toggled += enabled =>
        {
            try
            {
                HostingStartup.SetEnabled(OS.GetExecutablePath(), settings.HostingEnabled && enabled);
                settings = settings with { ResumeHostingAtSignIn = enabled };
                SaveSettings();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                resumeHosting.SetPressedNoSignal(settings.ResumeHostingAtSignIn);
                SetStatus("Windows could not save the sign-in setting. Hosting is unchanged; try again.");
            }
        };
        hostingPage.AddChild(resumeHosting);
        stopWithGame.SetPressedNoSignal(settings.StopHostingWhenGameQuits);
        stopWithGame.TooltipText = "Stop hosting before changing this option.";
        stopWithGame.Toggled += enabled =>
        {
            settings = settings with { StopHostingWhenGameQuits = enabled };
            SaveSettings();
        };
        hostingPage.AddChild(stopWithGame);
        StyleButton(startHosting, primary: true);
        startHosting.Pressed += () => _ = StartHostingAsync();
        StyleButton(stopHosting);
        stopHosting.Pressed += () => _ = StopHostingAsync();
        StyleButton(hostingBack);
        hostingBack.Pressed += () => { if (!hostingRunning && !busy) ShowPage(playPage); };
        var controls = new HBoxContainer();
        foreach (var button in new[] { startHosting, stopHosting, hostingBack })
        {
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            controls.AddChild(button);
        }
        hostingPage.AddChild(controls);
        var refresh = new Godot.Timer { WaitTime = 2, Autostart = true };
        refresh.Timeout += () =>
        {
            if (hostingRunning && !busy && !refreshingHost && GetWindow().Mode != Window.ModeEnum.Minimized)
                _ = RefreshHostAsync();
        };
        AddChild(refresh);
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += () =>
        {
            if (hostingRunning || busy && hostingPage.Visible) GetWindow().Mode = Window.ModeEnum.Minimized;
            else GetTree().Quit();
        };
        GetWindow().FocusExited += () => { if (hostingRunning) Engine.MaxFps = 1; };
        GetWindow().FocusEntered += () =>
        {
            Engine.MaxFps = 0;
            if (hostingRunning && !refreshingHost && !busy) _ = RefreshHostAsync();
        };
        RefreshHostControls();
    }

    private void OpenHostPage()
    {
        if (busy || joining) return;
        ShowPage(hostingPage);
        if (!hostingRunning)
        {
            hostingAddress.Text = "Use Tailscale on both computers and enable HTTPS on the hosting computer.";
            hostingState.Text = "Choose a game version on Play first. Hosting uses this PC's worlds, saved keys and model-call count.";
        }
        RefreshHostControls();
    }

    private void RefreshHostControls()
    {
        startHosting.Disabled = busy || hostingRunning || Chosen() is null;
        stopHosting.Visible = hostingRunning;
        stopHosting.Disabled = busy;
        stopWithGame.Disabled = busy || hostingRunning;
        resumeHosting.Disabled = busy;
        hostingBack.Disabled = busy || hostingRunning;
        navigationTabs.Visible = !hostingRunning;
    }

    private async Task ResumeHostingAsync()
    {
        if (!settings.HostingEnabled || !settings.ResumeHostingAtSignIn) return;
        requestedVersion = settings.HostedVersion;
        RefreshInstalled();
        OpenHostPage();
        await StartHostingAsync(restoring: true);
        if (hostingRunning && hostedOrigin is not null && IsInsideTree()) GetWindow().Mode = Window.ModeEnum.Minimized;
    }

    private async Task StartHostingAsync(bool restoring = false)
    {
        if (busy || hostingRunning || Chosen() is not { } chosen) return;
        var version = installed.FirstOrDefault(item => item.Version == chosen);
        if (version is null)
        {
            var release = releases.Games.FirstOrDefault(item => item.Version == chosen);
            if (release is null) { SetStatus("Install a game version on Versions before hosting."); return; }
            version = await InstallAsync(release, playAfter: false);
            if (version is null) return;
        }
        var startedHere = false;
        SetBusy(true);
        RefreshHostControls();
        SetStatus("Starting your hosted world…");
        try
        {
            var dataRoot = layout.HostingDataDirectory(settings, restoring);
            var useTestData = dataRoot == layout.TestDataDirectory;
            var hostLayout = await Task.Run(() => LocalServerLayout.ForHosting(version, new LauncherLayout(dataRoot), settings.StopHostingWhenGameQuits), hostingLifetime.Token);
            var host = new LocalServer(hostLayout, http);
            hostedServer = host;
            var result = await host.StartAsync(version.Version.ToString(), version.SourceRevision!, hostingLifetime.Token);
            if (!result.IsReady) throw new GameInstallException(result.PlayerMessage);
            startedHere = result.Outcome == LocalServerStartOutcome.Started;
            hostingRunning = true;
            var tailscale = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe");
            hostingNetwork = new TailscaleHost(new HostCommandRunner(), File.Exists(tailscale) ? tailscale : "tailscale");
            var address = await hostingNetwork.ConnectAsync(host.Origin, hostingLifetime.Token);
            hostedOrigin = address;
            if (!IsInsideTree()) return;
            settings = settings with { HostingEnabled = true, HostedVersion = version.Version.ToString(), HostedUseTestData = useTestData };
            settings.Save(layout);
            var startupSaved = true;
            try { HostingStartup.SetEnabled(OS.GetExecutablePath(), settings.ResumeHostingAtSignIn); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                settings = settings with { ResumeHostingAtSignIn = false };
                resumeHosting.SetPressedNoSignal(false);
                settings.Save(layout);
                startupSaved = false;
            }
            hostingAddress.Text = "Address for the joining computer: " + address.AbsoluteUri;
            hostingData.Text = useTestData ? "Using your test data folder." : "Using this PC's existing worlds, saved keys and model-call count.";
            hostingState.Text = "Waiting for a game. The world saves and pauses when the last game leaves.";
            ShowPage(hostingPage);
            SetStatus(startupSaved ? "Hosting is running. Closing this window keeps it in the background; use Stop hosting to end it."
                : "Hosting is running. Windows could not save automatic restart; turn it on again to retry.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (startedHere && hostedServer is { } host)
            {
                if (await host.StopAsync(CancellationToken.None))
                {
                    await TurnOffHostingAddressAsync(host);
                    hostedOrigin = null;
                    hostedServer = null;
                    hostingRunning = false;
                }
            }
            if (IsInsideTree()) SetStatus(exception is GameInstallException ? exception.Message :
                "Hosting could not start. Check Tailscale and the game files, then try again. Your world is kept.");
        }
        finally
        {
            if (IsInsideTree())
            {
                SetBusy(false);
                RefreshHostControls();
            }
        }
    }

    private async Task StopHostingAsync()
    {
        if (busy || hostedServer is not { } host) return;
        SetBusy(true);
        RefreshHostControls();
        try
        {
            if (!await host.StopAsync(hostingLifetime.Token))
            {
                SetStatus("The world could not be saved. Hosting is still running and paused; try Stop hosting again.");
                return;
            }
            var networkStopped = await TurnOffHostingAddressAsync(host);
            hostedServer = null;
            hostedOrigin = null;
            hostingRunning = false;
            settings = settings with { HostingEnabled = false };
            var startupSaved = true;
            try
            {
                settings.Save(layout);
                HostingStartup.SetEnabled(OS.GetExecutablePath(), false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { startupSaved = false; }
            ClearHostRequests();
            hostingState.Text = "Hosting stopped. Your world and approved devices are kept.";
            hostingAddress.Text = string.Empty;
            SetStatus(!networkStopped ? "The world stopped, but Tailscale could not turn off its address. Check Tailscale's hosting settings." : startupSaved ? "Hosting stopped." :
                "Hosting stopped, but Windows could not save the restart setting. Turn off Start hosting again to retry.");
            Engine.MaxFps = 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { if (IsInsideTree()) SetStatus("Hosting could not be stopped cleanly. Your world is kept; try again."); }
        finally { if (IsInsideTree()) { SetBusy(false); RefreshHostControls(); } }
    }

    private async Task RefreshHostAsync()
    {
        if (hostedServer is not { } host || refreshingHost || busy) return;
        refreshingHost = true;
        try
        {
            if (host.StartedProcessHasExited)
            {
                await host.StopAsync(hostingLifetime.Token);
                if (!IsInsideTree() || hostedServer != host) return;
                var networkStopped = await TurnOffHostingAddressAsync(host);
                hostedServer = null;
                hostedOrigin = null;
                hostingRunning = false;
                ClearHostRequests();
                hostingState.Text = networkStopped ? "Hosting stopped. Your world is kept; choose Start hosting to open it again."
                    : "The world stopped, but Tailscale could not turn off its address. Check Tailscale's hosting settings.";
                RefreshHostControls();
                return;
            }
            var state = await host.HostStatusAsync(hostingLifetime.Token);
            var requests = await host.PendingPairingsAsync(hostingLifetime.Token);
            if (!IsInsideTree() || hostedServer != host || !hostingRunning) return;
            hostingState.Text = state.RecoveryPending ? "The saved world needs recovery in the connected game." : state.SaveFailed
                ? "The world is paused but could not be saved. Free disk space and try Stop hosting again."
                : state.ConnectedDevices == 1 ? "Hosting · one device connected." : state.ConnectedDevices > 1 ? $"Hosting · {state.ConnectedDevices} devices connected."
                : "Waiting for a game. The world is paused and no models are running.";
            ClearHostRequests();
            foreach (var request in requests) AddHostRequest(request);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (IsInsideTree() && hostedServer == host)
            {
                ClearHostRequests();
                hostingState.Text = "The host is not answering. If it stopped when the game quit, choose Stop hosting, then Start hosting to open it again.";
            }
        }
        finally { refreshingHost = false; }
    }

    private void ClearHostRequests()
    {
        foreach (var child in pendingDevices.GetChildren()) { pendingDevices.RemoveChild(child); child.QueueFree(); }
    }

    private async Task<bool> TurnOffHostingAddressAsync(LocalServer host)
    {
        if (hostingNetwork is null || hostedOrigin is null) return true;
        try
        {
            await hostingNetwork.DisconnectAsync(host.Origin, hostedOrigin, hostingLifetime.Token);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return false; }
    }

    private void AddHostRequest(HostDeviceRequest request)
    {
        var row = new VBoxContainer();
        row.AddChild(new Label
        {
            Text = request.PairingCode is { } code ? "Comparison code: " + code : "Code unavailable after restart. Decline this request and start joining again.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        row.AddChild(new Label { Text = "Approve only if this code matches the joining computer.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var actions = new HBoxContainer();
        var approve = new Button { Text = "Approve", Disabled = request.PairingCode is null, TooltipText = "Approve only if this code matches the joining computer." };
        var decline = new Button { Text = "Decline" };
        StyleButton(approve); StyleButton(decline);
        approve.Pressed += () => _ = AnswerHostRequestAsync(request, approve: true);
        decline.Pressed += () => _ = AnswerHostRequestAsync(request, approve: false);
        actions.AddChild(approve); actions.AddChild(decline);
        row.AddChild(actions);
        pendingDevices.AddChild(row);
    }

    private async Task AnswerHostRequestAsync(HostDeviceRequest request, bool approve)
    {
        if (busy || hostedServer is not { } host || approve && request.PairingCode is null) return;
        SetBusy(true);
        foreach (var button in pendingDevices.FindChildren("*", "Button", recursive: true, owned: false).OfType<Button>()) button.Disabled = true;
        try
        {
            var accepted = approve && request.PairingCode is { } code
                ? await host.ApprovePairingAsync(request.PairingId, code, hostingLifetime.Token)
                : await host.DeclinePairingAsync(request.PairingId, hostingLifetime.Token);
            if (IsInsideTree()) SetStatus(accepted ? approve ? "Device approved. Continue on the joining computer." : "Device declined." :
                "This request changed or expired. Start joining again.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { if (IsInsideTree()) SetStatus("The request could not be answered. Check the host and try again."); }
        finally
        {
            if (IsInsideTree()) { SetBusy(false); RefreshHostControls(); await RefreshHostAsync(); }
        }
    }
}
