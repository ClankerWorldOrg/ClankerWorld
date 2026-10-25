using ClankerWorld.Simulation;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// Signed owner requests: pause, instructions, order cancellation and world
/// edits, each guarded by the pending-submission store, and which controls
/// are available while one is in flight.
/// </summary>
public partial class Main
{
    private async Task<bool> SetPausedAsync(bool paused)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current?.Baseline.Snapshot is not { } snapshot)
        {
            return false;
        }

        var accepted = false;
        var generation = observationSession.RequestGeneration;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await AwaitCurrentWorldResultAsync(ownerApi.SetPausedAsync(
                ResolveWorldUri(), authority, deviceId, snapshot.WorldId, paused, signer, OwnerRequestToken));
            accepted = true;
            return receipt.Changed
                ? paused ? "World paused" : "World resumed"
                : $"The world was already {(paused ? "paused" : "running")}";
        }, waitForTurn: true);
        return accepted && IsCurrentWorldRequest(generation);
    }

    private async Task SubmitInstructionAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current is not { } current)
        {
            SetStatus("Wait for the world to load before sending a message.", good: false);
            return;
        }

        var selected = current.Baseline.Snapshot.Agents
            .FirstOrDefault(agent => string.Equals(agent.Id, selectedAgentId, StringComparison.Ordinal));
        if (selected is null || selected.IsDraft)
        {
            SetStatus("Pick an agent first.", good: false);
            return;
        }
        if (selected.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant"))
        {
            SetStatus("Infants need an adult to look after them. You can't send them messages.", good: false);
            return;
        }

        var text = instructionText.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Write something to say first.", good: false);
            return;
        }

        var action = new OwnerInstructionAction(
            $"instruction_{OwnerPairingProtocol.CreateRequestId()}",
            selected.Id,
            instructionOrderButton.ButtonPressed ? "order" : "suggestion",
            text,
            current.Baseline.Snapshot.WorldId,
            instructionOrderButton.ButtonPressed && instructionQueueToggle.ButtonPressed);
        if (!TryBeginPendingInstruction(action, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            await AwaitCurrentWorldResultAsync(ownerApi.SubmitInstructionAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, OwnerRequestToken));
            completed = true;
            instructionText.Text = string.Empty;
            return InstructionSubmissionResultText(action.Kind, action.Queue);
        }, retainedSubmission: pending);
        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private async Task CancelSelectedOrderAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current is not { } current)
        {
            SetStatus("Wait for the world to load before cancelling an order.", good: false);
            return;
        }
        var order = PendingOrderToCancel(current.Baseline.Snapshot, selectedAgentId);
        if (order is null)
        {
            SetStatus("This agent has no waiting or active order to cancel.", good: false);
            return;
        }

        var action = new OwnerOrderCancelAction(
            $"cancel_order_{OwnerPairingProtocol.CreateRequestId()}",
            order.TargetAgentId,
            order.InstructionId,
            current.Baseline.Snapshot.WorldId);
        if (!TryBeginPendingOrderCancel(action, out var pending))
            return;
        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await AwaitCurrentWorldResultAsync(ownerApi.CancelOrderAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, OwnerRequestToken));
            completed = true;
            return OrderCancellationResultText(receipt);
        }, retainedSubmission: pending);
        if (completed)
            CompletePendingSubmission(pending);
    }

    private static string InstructionSubmissionResultText(string kind, bool queue) => kind == "order"
        ? queue ? "Order added to the queue." : "Order sent."
        : "Suggestion sent.";

    private static string OrderCancellationResultText(OwnerOrderControlReceipt receipt)
    {
        if (receipt.Changed)
            return "Order cancelled.";

        return receipt.Status switch
        {
            "cancelled" => "That order was already cancelled.",
            "finished" => "That order had already finished.",
            "not_understood" => "The agent could not follow that order.",
            _ => "That order is no longer waiting or active.",
        };
    }

    private async Task SubmitWorldEditAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            observationSession.Current?.Baseline.Snapshot is not { Authoring: { IsPaused: true } authoring } snapshot)
        {
            SetStatus("World editing works only once the world server shows the world as paused. Pause it, then try again.", good: false);
            return;
        }

        var operation = new OwnerAuthoringOperationAction(
            SelectedWorldEditKind(),
            EmptyToNull(worldEditId.Text),
            EmptyToNull(worldEditValue.Text),
            EmptyToNull(worldEditSecondaryValue.Text),
            checked((int)worldEditX.Value),
            checked((int)worldEditY.Value),
            worldEditRenewable.ButtonPressed);
        var batch = new OwnerAuthoringBatchAction(
            $"authoring_{OwnerPairingProtocol.CreateRequestId()}",
            worldEditAdvanced.ButtonPressed ? [operation] :
                PickedWorldEditTool is { } tool && PickedWorldEditKind is { } kind
                ? worldEditTiles.OrderBy(tile => tile.Y).ThenBy(tile => tile.X).Select(tile =>
                    new OwnerAuthoringOperationAction(tool.Operation, null, kind.Value, null, tile.X, tile.Y, kind.IsRenewable)).ToArray()
                : [], snapshot.WorldId, authoring.Revision, authoring.RunEpoch, observationSession.Timeline);
        if (batch.Operations.Count == 0) return;
        if (!TryBeginPendingAuthoring(batch, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await AwaitCurrentWorldResultAsync(ownerApi.SubmitAuthoringAsync(
                ResolveWorldUri(), authority, deviceId, batch, signer, OwnerRequestToken));
            if (!receipt.Applied)
                throw new OwnerWorldEditRefusedException(receipt.Failure ?? "The server did not say why. Refresh the world, then try again.");
            completed = true;
            ClearWorldEditPreview();
            return "World edit saved. See Developer edit in the Event Log.";
        }, retainedSubmission: pending, sendingPanel: developerPanel);
        if (completed)
        {
            CompletePendingSubmission(pending);
        }
    }

    private bool TryGetOwner(
        out OwnerAuthorityIdentity authority,
        out string deviceId,
        out IOwnerDeviceSigner signer)
    {
        if (!observationSession.AwaitingFreshBaseline)
            return TryGetRegisteredOwner(out authority, out deviceId, out signer);

        authority = null!;
        deviceId = string.Empty;
        signer = null!;
        return false;
    }

    private bool TryGetRegisteredOwner(
        out OwnerAuthorityIdentity authority,
        out string deviceId,
        out IOwnerDeviceSigner signer)
    {
        if (!registeredEndpointInvalid &&
            registration is not null && deviceKey is not null)
        {
            authority = registration.Authority;
            deviceId = registration.DeviceId;
            signer = deviceKey;
            return true;
        }

        authority = null!;
        deviceId = string.Empty;
        signer = null!;
        return false;
    }

    private void RefreshControlAvailability()
    {
        RefreshManualSaveAvailability();
        RefreshWorldMenuAvailability();
        if (!IsCurrentAutosaveSettingsContext()) CancelAutosaveSettingsRead();
        var paired = !registeredEndpointInvalid && registration is not null && deviceKey is not null;
        worldSettingsCategoryButton.Disabled = !paired || !isInWorld || returnToMainMenu ||
            observationSession.AwaitingFreshBaseline;
        menuResumeButton.Disabled = observationSession.AwaitingFreshBaseline;
        menuSaveWorldButton.Disabled = observationSession.AwaitingFreshBaseline || isOwnerAction;
        menuLoadSaveButton.Disabled = menuSaveWorldButton.Disabled;
        menuQuitToMainButton.Disabled = observationSession.AwaitingFreshBaseline || isOwnerAction;
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var paused = snapshot?.Authoring?.IsPaused == true;
        var selected = snapshot?.Agents.FirstOrDefault(item =>
            string.Equals(item.Id, selectedAgentId, StringComparison.Ordinal));
        var actionDisabled = !paired || isOwnerAction || pendingSubmission is not null ||
            observationSession.AwaitingFreshBaseline;
        buildingManagementApply.Disabled = actionDisabled || buildingManagementChoice.ItemCount == 0;
        buildingManagementChoice.Disabled = actionDisabled;
        buildingRemoveButton.Disabled = actionDisabled;
        saveApiKeyButton.Disabled = actionDisabled;
        deleteApiKeyButton.Disabled = actionDisabled;
        savedApiKeyChoice.Disabled = actionDisabled;
        apiKeyProviderChoice.Disabled = actionDisabled;
        apiKeyLabelInput.Editable = !actionDisabled;
        apiKeyInput.Editable = !actionDisabled;
        autosaveApplyButton.Disabled = actionDisabled || !paused || !autosaveSettingsLoaded;
        var supportsAgingOverride = snapshot?.AgingOverrideRate is not null;
        var supportsRoutineHelper = snapshot?.RoutineHelperProvider is not null &&
            observationSession.Current?.Handshake.ServerCapabilities.Contains("owner-routine-helper.v1", StringComparer.Ordinal) == true;
        routineHelperChoice.Disabled = actionDisabled || !paused || !supportsRoutineHelper;
        routineHelperModelPicker.Editable = !routineHelperChoice.Disabled;
        var helper = SelectedRoutineHelper();
        var helperModel = helper == "off" ? string.Empty : routineHelperModelPicker.Model;
        applyRoutineHelperButton.Disabled = routineHelperChoice.Disabled ||
            helper != "off" && (!BoundedText.IsValid(helperModel, 128, requireTrimmed: true));
        routineHelperKeyChoice.Disabled = routineHelperChoice.Disabled;
        routineHelperSetupCheckButton.Disabled = actionDisabled || !supportsRoutineHelper || helper == "off" ||
            !BoundedText.IsValid(helperModel, 128, requireTrimmed: true);
        applyAgingOverrideButton.Disabled = actionDisabled || !paused || !supportsAgingOverride;
        agingOverrideChoice.Disabled = actionDisabled || !paused || !supportsAgingOverride;
        applyAgingOverrideButton.TooltipText = !supportsAgingOverride ? "This world server can't use the aging override." :
            !paused ? "Pause the world before changing the aging override." : "Change how fast agents age from now on; current ages stay the same.";
        if (snapshot?.AgingOverrideRate is { } rate && (lastObservedAgingOverride != rate || lastAgingOverrideWorldId != snapshot.WorldId))
        {
            agingOverrideChoice.Select(agingOverrideChoice.GetItemIndex(rate));
            lastObservedAgingOverride = rate;
            lastAgingOverrideWorldId = snapshot.WorldId;
        }
        pauseButton.Disabled = actionDisabled || snapshot is null;
        pauseButton.Visible = snapshot?.FounderSetup is not { Started: false };
        founderSetupButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { Started: false };
        townSiteButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { CanChooseTownSite: true };
        moveFounderButton.Disabled = actionDisabled || movingFounderId is null &&
            (snapshot?.FounderSetup is not { Started: false, Placed: > 0 } ||
             selected is null || !selected.Id.StartsWith("founder:", StringComparison.Ordinal));
        undoFounderButton.Disabled = actionDisabled || snapshot?.FounderSetup is not
        { Started: false, Placed: > 0, LastFounderId: not null };
        if (snapshot?.FounderSetup is { CanChooseTownSite: true, HasAcceptedTownSite: false })
            founderSetupButton.Disabled = true;
        addAgentButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { Started: true };
        renameAgentButton.Disabled = actionDisabled || selected is null || selected.IsDraft;
        renameAgentInput.Editable = !actionDisabled && selected is { IsDraft: false };
        renameToggleButton.Disabled = !renameAgentInput.Editable;
        startWorldButton.Disabled = actionDisabled || snapshot?.FounderSetup is not { Started: false, Placed: 4 };
        founderProviderChoice.Disabled = actionDisabled;
        founderCredentialChoice.Disabled = actionDisabled;
        founderModelPicker.Editable = !actionDisabled;
        founderThinkingChoice.Disabled = actionDisabled;
        founderModelSetupCheckButton.Disabled = actionDisabled;
        founderApiKeyInput.Editable = !actionDisabled;
        founderKeyLabelInput.Editable = !actionDisabled;
        var infantSelected = selected?.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant") == true;
        var deceasedSelected = selected?.Lifecycle == "dead";
        submitInstructionButton.Disabled = actionDisabled || selected is null || selected.IsDraft || infantSelected || deceasedSelected;
        submitInstructionButton.TooltipText = deceasedSelected ? "You can't send messages to someone who has died." :
            infantSelected ? "Send messages to an adult who cares for them instead." : "Send this message to the agent.";
        RenderDeveloperEdits(snapshot, actionDisabled);
        submitWorldEditButton.Disabled = actionDisabled || !paused ||
            !worldEditAdvanced.ButtonPressed && (worldEditTiles.Count == 0 || PickedWorldEditKind is null);
        worldEditTool.Disabled = actionDisabled || !paused || worldEditTools.Count == 0;
        worldEditPick.Disabled = worldEditPaint.Disabled = actionDisabled || !paused || PickedWorldEditKind is null;
        worldEditAdvanced.Disabled = actionDisabled || !paused;
        worldEditClear.Disabled = isOwnerAction || worldEditTiles.Count == 0;
        worldEditKind.Disabled = actionDisabled || !paused;
        worldEditId.Editable = !actionDisabled && paused;
        worldEditValue.Editable = !actionDisabled && paused;
        worldEditSecondaryValue.Editable = !actionDisabled && paused;
        worldEditX.Editable = !actionDisabled && paused;
        worldEditY.Editable = !actionDisabled && paused;
        worldEditRenewable.Disabled = actionDisabled || !paused;
        instructionSuggestButton.Disabled = actionDisabled || deceasedSelected;
        instructionOrderButton.Disabled = actionDisabled || deceasedSelected;
        instructionQueueToggle.Disabled = actionDisabled || deceasedSelected || !instructionOrderButton.ButtonPressed;
        instructionCancelButton.Disabled = actionDisabled || deceasedSelected;
        instructionText.Editable = !actionDisabled && !deceasedSelected;
        RenderPendingSubmission();
        retryPendingSubmissionButton.Disabled = !paired || isOwnerAction || isRefreshing || !CanRetryPendingSubmission(pendingSubmission);
        reviewPendingSubmissionButton.Disabled = !paired || isOwnerAction || isRefreshing ||
            observationSession.AwaitingFreshBaseline || snapshot is null;
        forgetPendingSubmissionButton.Disabled = isOwnerAction || isRefreshing;
        pairingApprovalId.Editable = !actionDisabled;
        pairingApprovalCode.Editable = !actionDisabled;
        approvePairingButton.Disabled = actionDisabled;
        refreshDevicesButton.Disabled = actionDisabled;
        revokeDeviceId.Editable = !actionDisabled;
        revokeDeviceButton.Disabled = actionDisabled;
        agentModelRoleChoice.Disabled = actionDisabled;
        agentModelProviderChoice.Disabled = actionDisabled;
        agentModelCredentialChoice.Disabled = actionDisabled;
        agentModelPicker.Editable = !actionDisabled && SelectedProviderId() != "deterministic";
        agentModelThinkingChoice.Disabled = actionDisabled;
        agentModelSetupCheckButton.Disabled = actionDisabled;
        agentModelApiKeyInput.Editable = !actionDisabled && SelectedProviderId() != "deterministic";
        agentModelCredentialLabelInput.Editable = !actionDisabled;
        refreshAgentModelProviderButton.Disabled = actionDisabled;
        applyUsageLimitButton.Disabled = actionDisabled || usageStatus?.AccountingError is not null;
        grantUsageCallsButton.Disabled = actionDisabled || usageStatus?.LimitReached != true;
        refreshUsageButton.Disabled = actionDisabled;
        usageAttemptLimitInput.Editable = !actionDisabled && usageStatus?.AccountingError is null;
        var selectedProvider = SelectedProviderId();
        var selectedProviderStatus = providerConfiguration?.Providers.FirstOrDefault(item =>
            string.Equals(item.Provider, selectedProvider, StringComparison.Ordinal));
        saveAgentModelProviderButton.Disabled = actionDisabled ||
            SelectedAgentModelTarget() is not null && selectedProvider == "jev" ||
            SelectedAgentModelTarget() is not null && selectedProviderStatus?.HasCredential != true &&
                IsModelProvider(selectedProvider) && SelectedCredentialChoice() == "default";
        forgetAgentModelCredentialButton.Disabled = actionDisabled || selectedProvider == "deterministic" ||
            selectedProviderStatus?.HasCredential != true;
        deleteAgentModelCredentialSlotButton.Disabled = actionDisabled ||
            providerConfiguration?.Assignments?.Any(item =>
                item.CredentialSlotId == SelectedCredentialChoice() && item.SelectionReason is null) == true;
    }

}
