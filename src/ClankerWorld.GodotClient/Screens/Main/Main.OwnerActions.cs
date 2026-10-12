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
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            return false;
        }

        var accepted = false;
        var generation = observationSession.RequestGeneration;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await AwaitCurrentWorldResultAsync(ownerApi.SetPausedAsync(
                ResolveWorldUri(), authority, deviceId, paused, signer, CancellationToken.None));
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
            SetStatus("Wait for the world to load before giving an instruction.", good: false);
            return;
        }

        var selected = current.Baseline.Snapshot.Inhabitants
            .FirstOrDefault(inhabitant => string.Equals(inhabitant.Id, selectedInhabitantId, StringComparison.Ordinal));
        if (selected is null || selected.IsDraft)
        {
            SetStatus("Pick an agent first.", good: false);
            return;
        }
        if (selected.DecisionFactors.Any(factor => factor.Key == "age-band" && factor.Detail == "infant"))
        {
            SetStatus("Infants need an adult to look after them. They can't take work instructions.", good: false);
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
            instructionOrderButton.ButtonPressed ? "must_do" : "suggestive",
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
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None));
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
        var order = PendingOrderToCancel(current.Baseline.Snapshot, selectedInhabitantId);
        if (order is null)
        {
            SetStatus("This agent has no waiting or active order to cancel.", good: false);
            return;
        }

        var action = new OwnerOrderCancelAction(
            $"cancel_order_{OwnerPairingProtocol.CreateRequestId()}",
            order.TargetInhabitantId,
            order.InstructionId,
            current.Baseline.Snapshot.WorldId);
        if (!TryBeginPendingOrderCancel(action, out var pending))
            return;
        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await AwaitCurrentWorldResultAsync(ownerApi.CancelOrderAsync(
                ResolveWorldUri(), authority, deviceId, action, signer, CancellationToken.None));
            completed = true;
            return OrderCancellationResultText(receipt);
        }, retainedSubmission: pending);
        if (completed)
            CompletePendingSubmission(pending);
    }

    private static string InstructionSubmissionResultText(string kind, bool queue) => kind == "must_do"
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
            observationSession.Current?.Baseline.Snapshot.Authoring is not { IsPaused: true })
        {
            SetStatus("paused authoring is disabled until the server reports an atomic paused boundary", good: false);
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
            [operation]);
        if (!TryBeginPendingAuthoring(batch, out var pending))
        {
            return;
        }

        var completed = false;
        await RunOwnerActionAsync(async () =>
        {
            var receipt = await AwaitCurrentWorldResultAsync(ownerApi.SubmitAuthoringAsync(
                ResolveWorldUri(), authority, deviceId, batch, signer, CancellationToken.None));
            completed = true;
            return receipt.Applied
                ? $"applied {operation.Kind} at revision {receipt.Revision}"
                : $"authoring rejected · {receipt.Failure ?? "unknown validation failure"}";
        }, retainedSubmission: pending);
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
        menuQuitToMainButton.Disabled = observationSession.AwaitingFreshBaseline || isOwnerAction;
        var snapshot = observationSession.Current?.Baseline.Snapshot;
        var paused = snapshot?.Authoring?.IsPaused == true;
        var selected = snapshot?.Inhabitants.FirstOrDefault(item =>
            string.Equals(item.Id, selectedInhabitantId, StringComparison.Ordinal));
        var actionDisabled = !paired || isOwnerAction || pendingSubmission is not null ||
            observationSession.AwaitingFreshBaseline;
        buildingManagementApply.Disabled = actionDisabled || buildingManagementChoice.ItemCount == 0;
        buildingManagementChoice.Disabled = actionDisabled;
        buildingRemoveButton.Disabled = actionDisabled;
        saveApiKeyButton.Disabled = actionDisabled;
        apiKeyProviderChoice.Disabled = actionDisabled;
        apiKeyLabelInput.Editable = !actionDisabled;
        apiKeyInput.Editable = !actionDisabled;
        autosaveApplyButton.Disabled = actionDisabled || !paused || !autosaveSettingsLoaded;
        var supportsLifePace = snapshot?.LifePaceRate is not null;
        var supportsRoutineHelper = snapshot?.RoutineHelperProvider is not null &&
            observationSession.Current?.Handshake.ServerCapabilities.Contains("owner-routine-helper.v1", StringComparer.Ordinal) == true;
        routineHelperChoice.Disabled = actionDisabled || !paused || !supportsRoutineHelper;
        routineHelperModelPicker.Editable = !routineHelperChoice.Disabled;
        var helper = SelectedRoutineHelper();
        var helperModel = helper == "off" ? string.Empty : routineHelperModelPicker.Model;
        applyRoutineHelperButton.Disabled = routineHelperChoice.Disabled ||
            helper != "off" && (helperModel.Length is < 1 or > 128 || helperModel.Any(char.IsControl));
        routineHelperKeyChoice.Disabled = routineHelperChoice.Disabled;
        applyLifePaceButton.Disabled = actionDisabled || !paused || !supportsLifePace;
        lifePaceChoice.Disabled = actionDisabled || !paused || !supportsLifePace;
        applyLifePaceButton.TooltipText = !supportsLifePace ? "This host does not support life pacing." :
            !paused ? "Pause the world before changing life pace." : "Apply future aging speed; existing ages are preserved.";
        if (snapshot?.LifePaceRate is { } rate && (lastObservedLifePace != rate || lastLifePaceWorldId != snapshot.WorldId))
        {
            lifePaceChoice.Select(lifePaceChoice.GetItemIndex(rate));
            lastObservedLifePace = rate;
            lastLifePaceWorldId = snapshot.WorldId;
        }
        worldUrlInput.Editable = registration is null && pendingPairing is null && !isPairingOperation && !isOwnerAction && !isRefreshing;
        connectButton.Disabled = registeredEndpointInvalid || pendingPairing is not null || isPairingOperation || isOwnerAction || isRefreshing;
        pairAgainButton.Visible = registration is not null;
        pairAgainButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
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
        submitInstructionButton.TooltipText = deceasedSelected ? "Historical profiles cannot receive instructions." :
            infantSelected ? "Direct care through an adult caregiver." : "Send an instruction to this agent.";
        RenderDeveloperEdits(snapshot, actionDisabled);
        submitWorldEditButton.Disabled = actionDisabled || !paused;
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
        retryPendingSubmissionButton.Disabled = !paired || isOwnerAction || !CanRetryPendingSubmission(pendingSubmission);
        forgetPendingSubmissionButton.Disabled = isPairingOperation || isOwnerAction || isRefreshing;
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
                IsHostedProvider(selectedProvider) && SelectedCredentialChoice() == "default";
        forgetAgentModelCredentialButton.Disabled = actionDisabled || selectedProvider == "deterministic" ||
            selectedProviderStatus?.HasCredential != true;
        deleteAgentModelCredentialSlotButton.Disabled = actionDisabled ||
            providerConfiguration?.Assignments?.Any(item =>
                item.CredentialSlotId == SelectedCredentialChoice() && item.SelectionReason is null) == true;
        // A public key can have only one pending server pairing. Keep the
        // visible comparison value stable until it expires or activates.
        pairButton.Disabled = isPairingOperation || deviceKey is null || pendingPairing is not null || registration is not null;
        forgetRegistrationButton.Disabled = isPairingOperation || registration is null;
    }

}
