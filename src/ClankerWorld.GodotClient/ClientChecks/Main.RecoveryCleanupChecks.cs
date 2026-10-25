using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyRecoveryCleanupAsync()
    {
        var previousConnection = CaptureOwnerCheckConnection();
        var previousObservation = observationSession.Current;
        var previousKeyboard = keyboardNavigation;
        var previousKeyboardKey = keyboardNavigationKey;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        try
        {
            ConnectOwnerCheck(host, signer);
            observationSession.ReplaceRegistration(registration);
            var snapshot = new OwnerWorldSnapshot("cleanup-world", 0, "cleanup-map", [new(0, 0, "meadow")], [], [], null, 0)
            { Authoring = new(true, 0, 0, 0, "cleanup-map", "cleanup-map", "clear", "spring", []) };
            var handshake = new OwnerWorldHandshake(new(1, 1),
                ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1", "spatial-knowledge.read.v1",
                 "owner-control.request.v1", "paused-authoring.request.v1"], []);
            var reconnect = new OwnerWorldReconnect(handshake, new(snapshot, new(0, 0, [])));
            if (!observationSession.TryAccept(reconnect, 0, out var failure))
                throw new InvalidOperationException("Cleanup fixture refused: " + failure);
            host.Reconnect = reconnect;
            ManualWorldSave old = new("older-copy", "Before overwriting: First", DateTimeOffset.UnixEpoch, 1);
            ManualWorldSave keep = new("named-save", "First", DateTimeOffset.UnixEpoch, 2);
            host.ManualSaves = [old, keep];
            host.RecoveryPreview = new("cleanup-world", 3, new string('a', 64), [old], [keep]);
            await RefreshAsync();
            await OpenManualSavesAsync(false);
            FailUiCheckIf(!host.RecoveryCleanupRequests.IsEmpty || recoveryCleanupButton.Disabled, "Opening Save World must offer cleanup without running it.");
            recoveryCleanupButton.EmitSignal(BaseButton.SignalName.Pressed);
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            while (!recoveryCleanupConfirmation.Visible && System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            FailUiCheckIf(!recoveryCleanupConfirmation.Visible || pendingRecoveryCleanup?.Digest != host.RecoveryPreview.Digest ||
                !recoveryCleanupSummary.Text.Contains(old.Id, StringComparison.Ordinal) ||
                !recoveryCleanupSummary.Text.Contains(keep.Id, StringComparison.Ordinal), "Cleanup must preview exact removed and kept copies before confirmation.");
            FailUiCheckIf(isOwnerAction && !recoveryCleanupConfirmation.GetOkButton().Disabled, "Cleanup confirmation must wait until its preview action finishes.");
            var previewText = recoveryCleanupSummary.Text;
            recoveryCleanupSummary.Text += "\n" + string.Join('\n', Enumerable.Range(0, 120).Select(index => $"Recovery copy {index}"));
            var previewScroll = recoveryCleanupConfirmation.GetChildren().OfType<ScrollContainer>().Single();
            try { await VerifyKeyboardScrollAsync(previewScroll, window: recoveryCleanupConfirmation); }
            finally
            {
                recoveryCleanupSummary.Text = previewText;
                previewScroll.ScrollVertical = 0;
            }
            recoveryCleanupConfirmation.EmitSignal(ConfirmationDialog.SignalName.Canceled);
            recoveryCleanupConfirmation.Hide();
            await CleanConfirmedRecoveryHistoryAsync();
            FailUiCheckIf(pendingRecoveryCleanup is not null || host.RecoveryCleanupRequests.Any(action => action.Operation == "apply"), "Cancelling the preview must never send cleanup.");
            started = System.Diagnostics.Stopwatch.GetTimestamp();
            while (isOwnerAction && System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await PreviewRecoveryCleanupAsync();
            if (recoveryCleanupConfirmation.GetOkButton().Disabled || pendingRecoveryCleanup is null)
                throw new InvalidOperationException($"A completed preview must enable explicit confirmation: " +
                    $"pending={pendingRecoveryCleanup?.Digest}, busy={isOwnerAction}, panel={manualSaveOverlay.Visible}.");
            recoveryCleanupConfirmation.Hide();
            await CleanConfirmedRecoveryHistoryAsync();
            var applied = host.RecoveryCleanupRequests.Single(action => action.Operation == "apply");
            FailUiCheckIf(applied.ExpectedDigest != host.RecoveryPreview.Digest || applied.WorldId != "cleanup-world" ||
                applied.KeepCount != 3 || listedManualSaves.Any(save => save.Id == old.Id), "Confirmed cleanup must bind the exact preview and refresh remaining saves.");
            ShowRecoveryCleanupPreview(host.RecoveryPreview with { Remove = [] });
            FailUiCheckIf(!recoveryCleanupConfirmation.GetOkButton().Disabled || pendingRecoveryCleanup is not null, "An empty preview must never offer deletion.");
        }
        finally
        {
            keyboardNavigation = previousKeyboard;
            keyboardNavigationKey = previousKeyboardKey;
            recoveryCleanupConfirmation.GuiReleaseFocus();
            GetViewport().GuiReleaseFocus();
            manualSaveOverlay.Hide();
            recoveryCleanupConfirmation.Hide();
            pendingRecoveryCleanup = null;
            RestoreOwnerCheckConnection(previousConnection);
            observationSession.ResetAfterLoad();
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
    }
}
