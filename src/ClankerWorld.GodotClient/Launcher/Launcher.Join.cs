using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Networking;
using ClankerWorld.GodotClient.Pairing;
using Godot;

namespace ClankerWorld.GodotClient.Launcher;

public partial class Launcher
{
    private readonly VBoxContainer joinPage = new();
    private readonly LineEdit hostAddress = new() { PlaceholderText = "https://your-private-host" };
    private readonly Label comparison = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button joinHost = new() { Text = "Join" };
    private readonly Button checkApproval = new() { Text = "Check approval" };
    private readonly Button forgetHost = new() { Text = "Forget this connection" };
    private readonly CancellationTokenSource joinLifetime = new();
    private OwnerDeviceKey? joinKey;
    private DeviceJoinSession? joinSession;
    private bool joining;
    private string? joinRegistrationPath;

    private void BuildJoinPage()
    {
        joinPage.AddThemeConstantOverride("separation", 8);
        joinPage.AddChild(SectionLabel("Join a world on another computer"));
        joinPage.AddChild(hostAddress);
        joinPage.AddChild(comparison);
        StyleButton(joinHost, primary: true);
        joinHost.Pressed += () => _ = JoinHostAsync();
        joinPage.AddChild(joinHost);
        StyleButton(checkApproval);
        checkApproval.Pressed += () => _ = CheckApprovalAsync();
        joinPage.AddChild(checkApproval);
        StyleButton(forgetHost);
        forgetHost.Pressed += () =>
        {
            if (joining) return;
            try
            {
                EnsureJoinSession();
                joinSession!.Forget();
                settings = settings with { JoinRemoteWorld = false };
                SaveSettings();
                RefreshJoinPage();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            { SetStatus("The saved connection could not be removed. Try again."); }
        };
        joinPage.AddChild(forgetHost);
        var back = new Button { Text = "Back" };
        StyleButton(back);
        back.Pressed += () => { if (!joining) ShowPage(playPage); };
        joinPage.AddChild(back);
        var timer = new Godot.Timer { WaitTime = 2, Autostart = true };
        timer.Timeout += () =>
        {
            if (joinSession?.Pending is not null && !joining) _ = CheckApprovalAsync();
        };
        AddChild(timer);
    }

    private void EnsureJoinSession()
    {
        var path = settings.DeveloperMode && settings.UseTestData
            ? Path.Combine(layout.TestDataDirectory, "client", "owner-device-registration.json")
            : ProjectSettings.GlobalizePath("user://owner-device-registration.json");
        if (joinSession is not null && joinRegistrationPath == path) return;
        joinSession = null;
        joinKey?.Dispose();
        joinKey = OwnerDeviceKey.OpenOrCreate(settings.DeveloperMode && settings.UseTestData
            ? new OwnerDeviceKeyOptions(OwnerDeviceKey.DefaultKeyName + ".TestData") : null);
        joinSession = new DeviceJoinSession(new OwnerWorldApi(http), joinKey, new OwnerDeviceRegistrationStore(path));
        joinRegistrationPath = path;
    }

    private void OpenJoinPage()
    {
        if (busy || joining) return;
        try { EnsureJoinSession(); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetStatus("Joining needs the device key store for your Windows account. Try again on your Windows PC.");
            return;
        }
        ShowPage(joinPage);
        RefreshJoinPage();
    }

    private void RefreshJoinPage()
    {
        var pending = joinSession?.Pending;
        var registration = joinSession?.Registration;
        hostAddress.Editable = !joining && pending is null && registration is null;
        joinHost.Disabled = joining || pending is not null || registration is not null;
        checkApproval.Visible = pending is not null;
        checkApproval.Disabled = joining;
        forgetHost.Disabled = joining;
        if (pending is not null)
            comparison.Text = $"Comparison code: {pending.PairingCode}\nPairing ID: {pending.PairingId}\nAsk the host to approve only when both codes match. Expires {pending.ExpiresAtUtc.LocalDateTime:t}.";
        else if (registration is not null)
        {
            hostAddress.Text = registration.WorldUrl;
            comparison.Text = "This device is approved. Choose Play to reconnect to this world.";
        }
        else comparison.Text = "Enter the address shown on the hosting computer. Both devices must be on its private network.";
    }

    private async Task JoinHostAsync()
    {
        if (joining || busy || joinSession is null) return;
        if (!ServerOrigin.TryResolve(hostAddress.Text, out var origin))
        {
            SetStatus("Enter the host's HTTPS address, without a path.");
            return;
        }
        joining = true;
        SetBusy(true);
        RefreshJoinPage();
        try
        {
            await joinSession.StartAsync(origin, joinLifetime.Token);
            SetStatus("Waiting for approval on the hosting computer.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { SetStatus(joinSession.FailureMessage(exception)); }
        finally { joining = false; SetBusy(false); RefreshJoinPage(); }
    }

    private async Task CheckApprovalAsync()
    {
        if (joining || busy || joinSession?.Pending is null) return;
        joining = true;
        SetBusy(true);
        RefreshJoinPage();
        try
        {
            if (await joinSession.PollAsync(joinLifetime.Token))
            {
                settings = settings with { JoinRemoteWorld = true };
                SaveSettings();
                SetStatus("Device approved. Choose Play to open the world.");
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { SetStatus(joinSession.FailureMessage(exception)); }
        finally { joining = false; SetBusy(false); RefreshJoinPage(); }
    }
}
