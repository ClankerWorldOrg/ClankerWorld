using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using System.Text.Json;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyLostPendingSubmissionsAsync()
    {
        var previousRegistration = registration;
        var previousKey = deviceKey;
        var previousUrl = serverUrlInput.Text;
        var previousObservation = observationSession.Current;
        var previousPending = pendingSubmission;
        var previousUnloadedPending = unloadedPendingSubmission;
        var previousSelection = selectedAgentId;
        var previousText = instructionText.Text;
        var previousOrder = instructionOrderButton.ButtonPressed;
        var previousQueue = instructionQueueToggle.ButtonPressed;
        var previousProfile = agentProfilePanel.Visible;
        var previousProfileRequested = agentProfileRequested;
        var previousInWorld = isInWorld;
        var previousMenus = (Main: mainMenuOverlay.Visible, Card: mainMenuCard.Visible,
            Pause: pauseMenuPanel.Visible, Shade: menuShade.Visible);
        var previousDeveloperMode = developerMode;
        var previousCi = System.Environment.GetEnvironmentVariable("CI");
        System.Environment.SetEnvironmentVariable("CI", "true");
        using var signer = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        using var host = new WorldActionSmokeHost(signer.PublicKeySpkiBase64);
        const string worldId = "lost-request-world";
        const string agentId = "lost-request-agent";
        var position = new OwnerWorldPosition(1, 1);
        host.Reconnect = new(new(new(1, 1), ["owner-observation.read.v1", "owner-event-presentation.v2", "agent-inspection.read.v1",
            "spatial-knowledge.read.v1", "owner-control.request.v1", "paused-authoring.request.v1",
            "owner-observation-timeline.v1"], []), new(
            new(worldId, 0, "lost-request-map", [], [], [], null, 0)
            {
                PackedTerrain = new(3, 3, "terrain-kind-v1", Convert.ToBase64String(new byte[9])),
                Agents = [new(agentId, "Rowan Lake", "active", position, 8_000, [], [],
                    new("idle", null, null, [], string.Empty), new(position, [position], [position]), false)],
            }, new(0, 0, []), new("lost-request-host", 1)));
        host.PendingSubmissionReceipt = (path, action) => path switch
        {
            OwnerPairingEndpoints.OwnerInstructions => new OwnerInstructionReceipt("accepted-order",
                action.GetProperty("idempotencyKey").GetString()!, 0, 0, 1),
            OwnerPairingEndpoints.OwnerOrderCancel => new OwnerOrderControlReceipt("accepted-order", "cancelled", true, 0, 0),
            _ => new OwnerAuthoringBatchReceipt(action.GetProperty("batchId").GetString()!, true, null, 1, 1, "lost-request-map"),
        };
        try
        {
            await WaitForWorldPreviewSmokeAsync(() => !isRefreshing && !isOwnerAction, "pending recovery fixture setup");
            developerMode = false;
            registration = new(host.Authority, "smoke-device", signer.PublicKeyFingerprint, host.Address);
            deviceKey = signer;
            serverUrlInput.Text = host.Address;
            observationSession.ReplaceRegistration(registration);
            pendingSubmission = null;
            if (!observationSession.TryAccept(host.Reconnect, 0, out var failure))
                throw new InvalidOperationException($"The recovery fixture needs a confirmed baseline: {failure}");
            Render(host.Reconnect.Baseline.Snapshot, []);
            if (!TryCreatePendingSubmissionBinding(out var binding))
                throw new InvalidOperationException("The recovery fixture needs its real paired binding.");
            OwnerPendingSubmission[] requests =
            [
                OwnerPendingSubmission.ForInstruction(binding, new("lost-instruction", agentId, "order", "Rest.", worldId, true)),
                OwnerPendingSubmission.ForOrderCancel(binding, new("lost-cancel", agentId, "accepted-order", worldId)),
                OwnerPendingSubmission.ForAuthoring(binding, new("lost-authoring", [new("weather", null, "clear", null, 0, 0, false)])),
            ];
            foreach (var request in requests)
            {
                host.PendingSubmissionActions.Clear();
                host.LoseNextSubmissionReply = true;
                if (!TryRetainPendingSubmission(request)) throw new InvalidOperationException("Could not retain the recovery request.");
                await RetryPendingSubmissionAsync();
                if (!ReferenceEquals(pendingSubmission, request) || !saveApiKeyButton.Disabled || !buildingRemoveButton.Disabled ||
                    JsonSerializer.Serialize(pendingSubmissionStore.TryLoad(binding)) != JsonSerializer.Serialize(request))
                    throw new InvalidOperationException("A lost receipt must keep the exact request on disk and block conflicting controls.");
                if (ReferenceEquals(request, requests[0]))
                {
                    var window = GetWindow();
                    var oldSize = window.Size;
                    var oldScaleSize = window.ContentScaleSize;
                    try
                    {
                        foreach (var size in new[] { new Godot.Vector2I(1920, 1080), new Godot.Vector2I(1280, 720) })
                        {
                            window.Size = size;
                            window.ContentScaleSize = size;
                            ApplyResponsiveLayout();
                            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
                            if (!GetViewportRect().Grow(1).Encloses(pendingSubmissionPanel.GetGlobalRect()) ||
                                !pendingSubmissionPanel.GetGlobalRect().Encloses(retryPendingSubmissionButton.GetGlobalRect()) ||
                                !pendingSubmissionPanel.GetGlobalRect().Encloses(forgetPendingSubmissionButton.GetGlobalRect()))
                                throw new InvalidOperationException($"Normal recovery controls must fit the screen at {size}.");
                        }
                    }
                    finally { window.Size = oldSize; window.ContentScaleSize = oldScaleSize; ApplyResponsiveLayout(); }
                }
                LoadPendingSubmission();
                await RefreshAsync();
                var sent = host.PendingSubmissionActions.ToArray();
                if (sent.Length != 2 || sent[0] != sent[1] || pendingSubmission is not null ||
                    pendingSubmissionStore.TryLoad(binding) is not null || saveApiKeyButton.Disabled || buildingRemoveButton.Disabled)
                    throw new InvalidOperationException("#1812: reconnect must retry the identical request and restore normal owner controls without Developer mode.");
                await RefreshAsync();
                if (host.PendingSubmissionActions.Count != 2)
                    throw new InvalidOperationException("A confirmed receipt must end recovery without sending another request.");
            }
            foreach (var onMainMenu in new[] { false, true })
                foreach (var discard in new[] { false, true })
                {
                    pauseMenuPanel.Hide();
                    menuShade.Hide();
                    mainMenuOverlay.Visible = onMainMenu;
                    if (onMainMenu) mainMenuCard.Show();
                    isInWorld = !onMainMenu;
                    selectedAgentId = agentId;
                    agentProfileRequested = !onMainMenu;
                    RenderSelectedAgentCard(host.Reconnect.Baseline.Snapshot);
                    agentProfilePanel.Visible = !onMainMenu;
                    var scope = onMainMenu ? mainMenuOverlay : agentProfilePanel;
                    var opener = KeyboardControls(scope)[0];
                    opener.GrabFocus();
                    var ambiguous = OwnerPendingSubmission.ForInstruction(binding,
                        new("ambiguous-twice", agentId, "order", "Rest.", worldId));
                    host.PendingSubmissionActions.Clear();
                    if (!TryRetainPendingSubmission(ambiguous)) throw new InvalidOperationException("Could not retain the ambiguous recovery request.");
                    host.LoseNextSubmissionReply = true;
                    await RetryPendingSubmissionAsync();
                    host.LoseNextSubmissionReply = true;
                    await RefreshAsync();
                    await RefreshAsync();
                    if (host.PendingSubmissionActions.Count != 2 || !ReferenceEquals(pendingSubmission, ambiguous) ||
                        !pendingSubmissionPanel.IsVisibleInTree() || retryPendingSubmissionButton.Disabled || forgetPendingSubmissionButton.Disabled)
                        throw new InvalidOperationException("A second lost reply must offer normal recovery without automatically retrying on every pulse.");
                    opener.GrabFocus();
                    await KeyboardKeyAsync(Godot.Key.Tab);
                    if (GetViewport().GuiGetFocusOwner() != retryPendingSubmissionButton)
                        throw new InvalidOperationException($"Tab must reach request recovery from {(onMainMenu ? "Main Menu" : "Profile")}.");
                    await KeyboardKeyAsync(Godot.Key.Tab, shift: true);
                    if (GetViewport().GuiGetFocusOwner() != forgetPendingSubmissionButton ||
                        retryPendingSubmissionButton.GetThemeStylebox("focus") is Godot.StyleBoxEmpty ||
                        forgetPendingSubmissionButton.GetThemeStylebox("focus") is Godot.StyleBoxEmpty)
                        throw new InvalidOperationException("Shift+Tab must reach Discard it with a visible focus style.");
                    await KeyboardActivateAsync(pendingSubmissionPanel, discard ? forgetPendingSubmissionButton : retryPendingSubmissionButton);
                    await WaitForWorldPreviewSmokeAsync(() => pendingSubmission is null && !isOwnerAction, "keyboard recovery after a second lost reply");
                    for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
                    var manualReplies = host.PendingSubmissionActions.ToArray();
                    if (manualReplies.Length != (discard ? 2 : 3) || manualReplies.Any(action => action != manualReplies[0]) ||
                        pendingSubmissionStore.HasRecord || pendingSubmissionPanel.Visible)
                        throw new InvalidOperationException("Keyboard recovery must clear the retained request; retry keeps its exact payload and discard sends nothing.");
                    var focus = GetViewport().GuiGetFocusOwner();
                    if (focus is null || !IsKeyboardFocusable(focus) || !scope.IsAncestorOf(focus))
                        throw new InvalidOperationException("Recovery must return focus to a usable control on the original screen.");
                    await KeyboardKeyAsync(Godot.Key.Tab);
                    if (GetViewport().GuiGetFocusOwner() is not { } nextFocus || !scope.IsAncestorOf(nextFocus))
                        throw new InvalidOperationException("Normal screen focus must stay within its menu or Profile after recovery.");
                }

            var originalReconnect = host.Reconnect;
            foreach (var changed in new[] { "load", "restart", "switch" })
                foreach (var review in new[] { false, true })
                    foreach (var request in requests)
                    {
                        host.Reconnect = originalReconnect;
                        observationSession.ReplaceRegistration(registration);
                        await RefreshAsync();
                        host.PendingSubmissionActions.Clear();
                        if (!TryRetainPendingSubmission(request)) throw new InvalidOperationException("Could not retain the stale recovery request.");
                        LoadPendingSubmission();
                        var saved = JsonSerializer.Serialize(pendingSubmission);
                        host.Reconnect = originalReconnect with
                        {
                            Baseline = originalReconnect.Baseline with
                            {
                                Timeline = new(changed == "restart" ? "restarted-host" : "lost-request-host", 2),
                                Snapshot = originalReconnect.Baseline.Snapshot with
                                {
                                    WorldId = changed == "switch" ? "another-world" : worldId,
                                },
                            },
                        };
                        await RefreshAsync();
                        await RefreshAsync();
                        if (!host.PendingSubmissionActions.IsEmpty || !pendingSubmissionPanel.IsVisibleInTree() ||
                            developerPanel.IsAncestorOf(pendingSubmissionPanel) || DeveloperMode ||
                            retryPendingSubmissionButton.Visible || !retryPendingSubmissionButton.Disabled ||
                            !reviewPendingSubmissionButton.IsVisibleInTree() || reviewPendingSubmissionButton.Disabled ||
                            forgetPendingSubmissionButton.Disabled || JsonSerializer.Serialize(pendingSubmissionStore.TryLoadForRegistration(binding)) != saved)
                            throw new InvalidOperationException("Changed worlds must keep the exact disk record without resending and offer ordinary review/discard controls.");
                        if (review) reviewPendingSubmissionButton.EmitSignal(Godot.BaseButton.SignalName.Pressed);
                        else forgetPendingSubmissionButton.EmitSignal(Godot.BaseButton.SignalName.Pressed);
                        if (pendingSubmission is not null || pendingSubmissionStore.TryLoadForRegistration(binding) is not null ||
                            pendingSubmissionPanel.Visible || saveApiKeyButton.Disabled || buildingRemoveButton.Disabled ||
                            !host.PendingSubmissionActions.IsEmpty)
                            throw new InvalidOperationException("Both ordinary recovery choices must unlock controls without sending a stale request.");
                        if (review && changed != "switch" && request.Instruction is { } instruction)
                        {
                            if (instructionText.Text != instruction.Text || !instructionOrderButton.ButtonPressed || !instructionQueueToggle.ButtonPressed)
                                throw new InvalidOperationException("Review must preserve the original instruction text, order kind and queue choice.");
                            await SubmitInstructionAsync();
                            using var sent = JsonDocument.Parse(host.PendingSubmissionActions.Single());
                            if (sent.RootElement.GetProperty("idempotencyKey").GetString() == instruction.IdempotencyKey ||
                                sent.RootElement.GetProperty("text").GetString() != instruction.Text || pendingSubmission is not null)
                                throw new InvalidOperationException("Only the player's new Send may submit the reviewed text, with a fresh request key.");
                        }
                    }
            // A legacy observation can support manual retries but cannot confirm a timeline for unattended recovery.
            host.Reconnect = originalReconnect with
            {
                Handshake = originalReconnect.Handshake with
                {
                    ServerCapabilities = originalReconnect.Handshake.ServerCapabilities.Where(capability => capability != "owner-observation-timeline.v1").ToArray(),
                },
                Baseline = originalReconnect.Baseline with { Timeline = null },
            };
            observationSession.ReplaceRegistration(registration);
            await RefreshAsync();
            foreach (var request in requests)
            {
                host.PendingSubmissionActions.Clear();
                var legacy = request with { Binding = binding with { Timeline = null } };
                if (!TryRetainPendingSubmission(legacy)) throw new InvalidOperationException("Could not retain the legacy recovery request.");
                await RefreshAsync();
                if (!host.PendingSubmissionActions.IsEmpty || !ReferenceEquals(pendingSubmission, legacy) ||
                    retryPendingSubmissionButton.Disabled || !pendingSubmissionPanel.IsVisibleInTree())
                    throw new InvalidOperationException("An unconfirmed legacy timeline must require an explicit player retry.");
                retryPendingSubmissionButton.EmitSignal(Godot.BaseButton.SignalName.Pressed);
                await WaitForWorldPreviewSmokeAsync(() => pendingSubmission is null && !isOwnerAction, "explicit legacy recovery");
                if (host.PendingSubmissionActions.Count != 1)
                    throw new InvalidOperationException("The player may explicitly retry one exact legacy request.");
            }
            var foreign = requests[0] with { Binding = binding with { DeviceId = "another-device" } };
            if (!pendingSubmissionStore.TrySave(foreign)) throw new InvalidOperationException("Could not save the mismatched recovery record.");
            host.PendingSubmissionActions.Clear();
            LoadPendingSubmission();
            await RefreshAsync();
            if (pendingSubmission is not null || !pendingSubmissionStore.HasRecord || !pendingSubmissionPanel.IsVisibleInTree() ||
                retryPendingSubmissionButton.Visible || reviewPendingSubmissionButton.Visible || forgetPendingSubmissionButton.Disabled ||
                !host.PendingSubmissionActions.IsEmpty || pendingSubmissionStore.TryLoad(foreign.Binding) is null)
                throw new InvalidOperationException("An unloadable record must stay on disk and offer ordinary discard without exposing or sending it.");
            forgetPendingSubmissionButton.EmitSignal(Godot.BaseButton.SignalName.Pressed);
            if (pendingSubmissionStore.HasRecord || pendingSubmissionPanel.Visible || !host.PendingSubmissionActions.IsEmpty)
                throw new InvalidOperationException("Ordinary discard must also release an unloadable saved record.");
        }
        finally
        {
            pendingSubmissionStore.TryForget();
            registration = previousRegistration;
            deviceKey = previousKey;
            serverUrlInput.Text = previousUrl;
            observationSession.ReplaceRegistration(previousRegistration);
            if (previousObservation is not null)
                observationSession.TryAccept(previousObservation, previousObservation.Baseline.Events.AfterEventId, out _);
            pendingSubmission = previousPending;
            unloadedPendingSubmission = previousUnloadedPending;
            selectedAgentId = previousSelection;
            instructionText.Text = previousText;
            instructionOrderButton.ButtonPressed = previousOrder;
            instructionSuggestButton.ButtonPressed = !previousOrder;
            instructionQueueToggle.ButtonPressed = previousQueue;
            agentProfilePanel.Visible = previousProfile;
            agentProfileRequested = previousProfileRequested;
            isInWorld = previousInWorld;
            mainMenuOverlay.Visible = previousMenus.Main;
            mainMenuCard.Visible = previousMenus.Card;
            pauseMenuPanel.Visible = previousMenus.Pause;
            menuShade.Visible = previousMenus.Shade;
            developerMode = previousDeveloperMode;
            RefreshControlAvailability();
            System.Environment.SetEnvironmentVariable("CI", previousCi);
        }
    }
}
