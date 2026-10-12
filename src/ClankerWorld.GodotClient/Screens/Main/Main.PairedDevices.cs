using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// Paired devices in Developer tools: approve a device waiting to pair, list
/// the paired devices and revoke one.
/// </summary>
public partial class Main
{
    private async Task ApprovePairingAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        var pairingId = pairingApprovalId.Text.Trim();
        var pairingCode = pairingApprovalCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(pairingId) || string.IsNullOrWhiteSpace(pairingCode))
        {
            SetStatus("enter the pending pairing ID and comparison code", good: false);
            return;
        }

        var action = new OwnerPairingApprovalAction(pairingId, pairingCode);
        await RunOwnerActionAsync(async () =>
        {
            var approval = await AwaitCurrentWorldResultAsync(ownerApi.ApprovePairingAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None));
            pairingApprovalCode.Text = string.Empty;
            return $"approved pending device {approval.DeviceId}; it must still activate its own Windows key";
        });
    }

    private async Task RevokeDeviceAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        var targetDeviceId = revokeDeviceId.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            SetStatus("enter a paired device ID to revoke it", good: false);
            return;
        }

        if (string.Equals(targetDeviceId, deviceId, StringComparison.Ordinal))
        {
            SetStatus("this client will not revoke its own active key; use host-local recovery if that is intentional", good: false);
            return;
        }

        var action = new OwnerDeviceManagementAction(targetDeviceId);
        await RunOwnerActionAsync(async () =>
        {
            var revoked = await AwaitCurrentWorldResultAsync(ownerApi.RevokeDeviceAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None));
            revokeDeviceId.Text = string.Empty;
            return $"revoked device {revoked.DeviceId}";
        });
        await RefreshDeviceRegistryAsync();
    }

    private async Task RefreshDeviceRegistryAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return;
        }

        await RunOwnerActionAsync(async () =>
        {
            pairedDevices = await AwaitCurrentWorldResultAsync(ownerApi.ListDevicesAsync(
                ResolveWorldUri(), authority, deviceId, signer, CancellationToken.None));
            RenderPairedDevices();
            return $"loaded {pairedDevices.Length} paired device record(s)";
        });
    }

    private void RenderPairedDevices()
    {
        pairedDeviceList.Clear();
        foreach (var device in pairedDevices
            .OrderBy(device => device.State == OwnerDeviceState.Active ? 0 : 1)
            .ThenBy(device => device.DeviceId, StringComparer.Ordinal))
        {
            var self = string.Equals(device.DeviceId, registration?.DeviceId, StringComparison.Ordinal)
                ? " · this Windows device"
                : string.Empty;
            pairedDeviceList.AddItem($"{device.State.ToString().ToLowerInvariant()} · {device.DeviceId}{self}");
            pairedDeviceList.SetItemMetadata(pairedDeviceList.ItemCount - 1, device.DeviceId);
        }
    }

    private void BuildDeveloperDevices()
    {
        var deviceManagementBody = new VBoxContainer();
        pairingApprovalId.PlaceholderText = "Pending pairing ID from the new device";
        deviceManagementBody.AddChild(pairingApprovalId);
        pairingApprovalCode.PlaceholderText = "Six-digit comparison code";
        pairingApprovalCode.Secret = true;
        deviceManagementBody.AddChild(pairingApprovalCode);
        approvePairingButton.Text = "Approve paired device";
        approvePairingButton.Pressed += () => _ = ApprovePairingAsync();
        deviceManagementBody.AddChild(approvePairingButton);
        refreshDevicesButton.Text = "Refresh signed device list";
        refreshDevicesButton.Pressed += () => _ = RefreshDeviceRegistryAsync();
        deviceManagementBody.AddChild(refreshDevicesButton);
        pairedDeviceList.CustomMinimumSize = new Vector2(0, 104);
        pairedDeviceList.ItemSelected += index =>
        {
            var deviceId = pairedDeviceList.GetItemMetadata(checked((int)index)).AsString();
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                revokeDeviceId.Text = deviceId;
            }
        };
        deviceManagementBody.AddChild(pairedDeviceList);
        revokeDeviceId.PlaceholderText = "Device ID to revoke";
        deviceManagementBody.AddChild(revokeDeviceId);
        revokeDeviceButton.Text = "Revoke other device";
        revokeDeviceButton.Pressed += () => _ = RevokeDeviceAsync();
        deviceManagementBody.AddChild(revokeDeviceButton);
        developerBody.AddChild(NewPanel("Paired-device management · signed server requests", deviceManagementBody));
    }
}
