using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly PanelContainer pendingSubmissionPanel = new();
    private readonly Button reviewPendingSubmissionButton = new();
    private OwnerPendingSubmission? automaticallyRetriedSubmission;
    private bool pendingRecoveryInProgress;
    private bool unloadedPendingSubmission;

    private void BuildPendingSubmissionRecovery(Control content)
    {
        var body = new VBoxContainer();
        body.AddChild(new Label { Text = "Your last request", ThemeTypeVariation = "SectionLabel" });
        pendingSubmissionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        pendingSubmissionLabel.CustomMinimumSize = new Vector2(420, 0);
        body.AddChild(pendingSubmissionLabel);
        var buttons = new HFlowContainer();
        retryPendingSubmissionButton.Text = "Check and send again";
        retryPendingSubmissionButton.Pressed += () => _ = RetryPendingSubmissionAsync();
        reviewPendingSubmissionButton.Text = "Check and send again";
        reviewPendingSubmissionButton.Pressed += ReviewPendingSubmission;
        forgetPendingSubmissionButton.Text = "Discard it";
        forgetPendingSubmissionButton.Pressed += ForgetPendingSubmission;
        foreach (var button in new[] { retryPendingSubmissionButton, reviewPendingSubmissionButton, forgetPendingSubmissionButton })
        {
            StyleButton(button);
            buttons.AddChild(button);
        }
        body.AddChild(buttons);
        AddPanelContents(pendingSubmissionPanel, body);
        pendingSubmissionPanel.ZIndex = 252;
        content.AddChild(pendingSubmissionPanel);
        body.MinimumSizeChanged += () => Callable.From(ApplyResponsiveLayout).CallDeferred();
        RenderPendingSubmission();
    }

    private async Task RecoverPendingSubmissionAsync()
    {
        var pending = pendingSubmission;
        // Only a confirmed, fully scoped timeline can authorize an unattended retry.
        // One attempt per loaded record avoids resending on every observation pulse.
        if (isOwnerAction || isRefreshing || pendingRecoveryInProgress || pending is null || ReferenceEquals(automaticallyRetriedSubmission, pending) ||
            pending.Binding.Timeline is not { IsValid: true } || !CanRetryPendingSubmission(pending) ||
            !TryCreatePendingSubmissionBinding(out var binding) || !pending.Binding.MatchesRegistration(binding)) return;
        pendingRecoveryInProgress = true;
        try
        {
            if (await RetryPendingSubmissionAsync()) automaticallyRetriedSubmission = pending;
        }
        finally { pendingRecoveryInProgress = false; }
    }

    private void ReviewPendingSubmission()
    {
        var pending = pendingSubmission;
        if (pending is null || isOwnerAction || isRefreshing || observationSession.AwaitingFreshBaseline) return;
        ForgetPendingSubmission();
        if (pendingSubmission is not null) return;
        // Review restores an instruction draft, never the old request's binding or key.
        // A new world must be inspected and addressed independently.
        if (pending.Instruction is { } instruction && observationSession.Current?.Baseline.Snapshot is { } world &&
            instruction.CanRetryIn(world.WorldId) && world.Agents.Any(agent => agent.Id == instruction.TargetAgentId))
        {
            if (selectedAgentId != instruction.TargetAgentId) SelectAgent(instruction.TargetAgentId);
            instructionText.Text = instruction.Text;
            instructionOrderButton.ButtonPressed = instruction.Kind == "order";
            instructionSuggestButton.ButtonPressed = !instructionOrderButton.ButtonPressed;
            instructionQueueToggle.ButtonPressed = instruction.Queue;
            agentProfilePanel.Show();
            SetStatus("Check this agent's Orders before sending again. Your earlier instruction is ready to review.", good: false);
        }
        else
            SetStatus("Check whether your last request took effect before sending a new one.", good: false);
        RefreshControlAvailability();
    }

    private bool TryBeginPendingInstruction(
        OwnerInstructionAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("Connect to your paired server before sending an instruction.", good: false);
            return false;
        }

        pending = OwnerPendingSubmission.ForInstruction(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryBeginPendingAuthoring(
        OwnerAuthoringBatchAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("Connect to your paired server before editing the world.", good: false);
            return false;
        }

        pending = OwnerPendingSubmission.ForAuthoring(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryBeginPendingOrderCancel(
        OwnerOrderCancelAction action,
        out OwnerPendingSubmission pending)
    {
        pending = null!;
        if (!TryCreatePendingSubmissionBinding(out var binding))
        {
            SetStatus("Connect to your paired server before cancelling an order.", good: false);
            return false;
        }
        pending = OwnerPendingSubmission.ForOrderCancel(binding, action);
        return TryRetainPendingSubmission(pending);
    }

    private bool TryRetainPendingSubmission(OwnerPendingSubmission candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (pendingSubmission is not null)
        {
            SetStatus("Your last request is still awaiting confirmation. Check and send again, or discard it first.", good: false);
            return false;
        }

        if (!pendingSubmissionStore.TrySave(candidate))
        {
            unloadedPendingSubmission = pendingSubmissionStore.HasRecord;
            RefreshControlAvailability();
            SetStatus("Your request could not be saved for recovery, so it was not sent. Resolve your last request before trying again.", good: false);
            return false;
        }

        pendingSubmission = candidate;
        unloadedPendingSubmission = false;
        RenderPendingSubmission();
        RefreshControlAvailability();
        return true;
    }

    private void LoadPendingSubmission()
    {
        pendingSubmission = TryCreatePendingSubmissionBinding(out var binding)
            ? pendingSubmissionStore.TryLoadForRegistration(binding)
            : null;
        unloadedPendingSubmission = pendingSubmission is null && pendingSubmissionStore.HasRecord;
        RenderPendingSubmission();
        RefreshControlAvailability();
    }

    private bool TryCreatePendingSubmissionBinding(out OwnerPendingSubmissionBinding binding)
    {
        binding = null!;
        if (registeredEndpointInvalid || registration is null || deviceKey is null)
        {
            return false;
        }

        try
        {
            binding = OwnerPendingSubmissionBinding.Create(
                registration.Authority,
                registration.DeviceId,
                deviceKey.PublicKeyFingerprint,
                ResolveWorldUri(), observationSession.Timeline, observationSession.Current?.Baseline.Snapshot.WorldId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<bool> RetryPendingSubmissionAsync()
    {
        var pending = pendingSubmission;
        if (pending is null)
        {
            SetStatus("There is no saved request to send again.", good: false);
            return false;
        }

        if (!TryGetOwner(out var authority, out var deviceId, out var signer) ||
            !TryCreatePendingSubmissionBinding(out var binding) ||
            !pending.Binding.MatchesRegistration(binding) || !CanRetryPendingSubmission(pending))
        {
            SetStatus("The world or connection has changed. Check your last request, then discard it before sending a new one.", good: false);
            return false;
        }

        if (pending.Instruction is { } retainedInstruction &&
            !retainedInstruction.CanRetryIn(observationSession.Current?.Baseline.Snapshot.WorldId))
        {
            SetStatus("The world has changed. Check your last instruction, then discard it before sending a new one.", good: false);
            return false;
        }
        if (pending.OrderCancel is { } retainedCancellation &&
            !retainedCancellation.CanRetryIn(observationSession.Current?.Baseline.Snapshot.WorldId))
        {
            SetStatus("The world has changed. Check the order before discarding your last cancellation.", good: false);
            return false;
        }

        var completed = false;
        var attempted = false;
        await RunOwnerActionAsync(async () =>
        {
            attempted = true;
            if (pending.Instruction is { } instruction)
            {
                await AwaitCurrentWorldResultAsync(ownerApi.SubmitInstructionAsync(
                    ResolveWorldUri(), authority, deviceId, instruction.ToAction(), signer, OwnerRequestToken));
                completed = true;
                return InstructionSubmissionResultText(instruction.Kind, instruction.Queue);
            }

            if (pending.Authoring is { } authoring)
            {
                var receipt = await AwaitCurrentWorldResultAsync(ownerApi.SubmitAuthoringAsync(
                    ResolveWorldUri(), authority, deviceId, authoring.ToAction(), signer, OwnerRequestToken));
                completed = true;
                return receipt.Applied
                    ? "World edit confirmed."
                    : $"World edit refused · {receipt.Failure ?? "unknown validation failure"}";
            }

            if (pending.OrderCancel is { } cancellation)
            {
                var receipt = await AwaitCurrentWorldResultAsync(ownerApi.CancelOrderAsync(
                    ResolveWorldUri(), authority, deviceId, cancellation.ToAction(), signer, OwnerRequestToken));
                completed = true;
                return OrderCancellationResultText(receipt);
            }

            throw new InvalidOperationException("The retained owner request has no supported payload.");
        }, retainedSubmission: pending);

        if (completed)
        {
            CompletePendingSubmission(pending);
        }
        return attempted;
    }

    private void CompletePendingSubmission(OwnerPendingSubmission completed)
    {
        if (!CanRetryPendingSubmission(completed)) return;
        if (!pendingSubmissionStore.TryClear(completed))
        {
            SetStatus("The server answered, but your saved request could not be cleared. Check the world, then try again or discard it.", good: false);
            return;
        }

        if (ReferenceEquals(pendingSubmission, completed))
        {
            pendingSubmission = null;
        }

        RenderPendingSubmission();
        RefreshControlAvailability();
    }

    private void ForgetPendingSubmission()
    {
        if (!pendingSubmissionStore.TryForget())
        {
            SetStatus("Your saved request could not be discarded. Try again.", good: false);
            return;
        }

        pendingSubmission = null;
        unloadedPendingSubmission = false;
        RenderPendingSubmission();
        RefreshControlAvailability();
        SetStatus("Discarded your saved request. This does not undo anything the server already accepted.", good: false);
    }

    private void RenderPendingSubmission()
    {
        var retryable = CanRetryPendingSubmission(pendingSubmission);
        pendingSubmissionPanel.Visible = pendingSubmission is not null || unloadedPendingSubmission;
        retryPendingSubmissionButton.Visible = retryable;
        reviewPendingSubmissionButton.Visible = pendingSubmission is not null && !retryable;
        pendingSubmissionLabel.Text = pendingSubmission is null
            ? "A saved request could not be loaded for this connection. Discarding it only clears it from this device."
            : isOwnerAction
            ? "Sending your request..."
            : retryable
                ? "The server has not confirmed your last request. Check and send again, or discard it to use other controls."
                : "The world has changed since your last request. Check whether it took effect before sending it again.";
        pendingSubmissionPanel.ResetSize();
        ApplyResponsiveLayout();
    }

    private readonly OwnerActionGate ownerActionGate = new();

    private void SetOwnerRequestBusy(bool busy)
    {
        isOwnerAction = busy;
        if (busy) refreshCancellation?.Cancel();
        RefreshControlAvailability();
    }

    private readonly Dictionary<Control, long> ownerPanelRevisions = [];

    private void BindOwnerRequestPanels()
    {
        foreach (var panel in new Control[] { agentProfilePanel, agentQuickCard, buildingDetailsPanel,
            buildingQuickCard, settingsPanel, agentModelPanel, usageLimitPanel, worldMenuCard,
            mainMenuCard, startupRecoveryCard, pendingSubmissionPanel, manualSaveCard, founderSetupPanel,
            developerPanel, apiKeysPanel, pauseMenuPanel, gameSettingsContent, worldSettingsContent })
        {
            ownerPanelRevisions[panel] = 0;
            panel.VisibilityChanged += () => ownerPanelRevisions[panel]++;
        }
    }

    private Func<bool> CaptureOwnerRequestPanel(Control? sendingPanel = null)
    {
        var panel = sendingPanel ?? GetViewport().GuiGetFocusOwner();
        while (panel is not null && !ownerPanelRevisions.ContainsKey(panel)) panel = panel.GetParent() as Control;
        var revision = panel is null ? 0 : ownerPanelRevisions[panel];
        var agent = selectedAgentId;
        var building = selectedBuildingId;
        return () => panel is null || GodotObject.IsInstanceValid(panel) &&
            ownerPanelRevisions[panel] == revision && selectedAgentId == agent && selectedBuildingId == building;
    }

    private async Task<T> RunOwnerLifecycleAsync<T>(Func<Task<T>> action, Action<bool> setBusy,
        bool bindPanel = true, bool bindWorld = true, Control? sendingPanel = null)
    {
        var generation = observationSession.RequestGeneration;
        var owner = registration;
        var panelIsCurrent = CaptureOwnerRequestPanel(sendingPanel);
        var runner = new OwnerRequest<T>(() => ReferenceEquals(owner, registration) &&
            (!bindWorld || IsCurrentWorldRequest(generation)) && (!bindPanel || panelIsCurrent()), setBusy);
        return await runner.RunAsync(async context =>
        {
            ownerRequestContext.Value = context;
            return await action();
        });
    }

    private bool CanRetryPendingSubmission(OwnerPendingSubmission? pending)
    {
        var worldId = observationSession.Current?.Baseline.Snapshot.WorldId;
        return pending is not null && !observationSession.AwaitingFreshBaseline &&
            pending.Binding.CanRetryIn(worldId, observationSession.Timeline) &&
            (pending.Instruction is not { } instruction || instruction.CanRetryIn(worldId)) &&
            (pending.OrderCancel is not { } cancellation || cancellation.CanRetryIn(worldId));
    }

    // A skipped or obsolete request cannot authorize follow-up UI changes.
    private async Task<bool> RunOwnerActionAsync(Func<Task<string>> action, bool waitForTurn = false,
        string? conflictMessage = null, OwnerPendingSubmission? retainedSubmission = null, bool changesTimeline = false, Control? sendingPanel = null)
    {
        var generation = observationSession.RequestGeneration;
        if (!IsCurrentWorldRequest(generation)) return false;
        var contextRetained = false;
        await ownerActionGate.RunAsync(async () =>
        {
            if (!IsCurrentWorldRequest(generation)) return;
            try
            {
                await RunOwnerLifecycleAsync(async () =>
                {
                    SetStatus("Sending...", good: true);
                    var detail = await action();
                    ownerRequestContext.Value!.EnsureCurrent();
                    SetStatus(detail, good: true);
                    await RefreshAsync();
                    return true;
                }, SetOwnerRequestBusy, bindPanel: !waitForTurn && !changesTimeline, bindWorld: !changesTimeline, sendingPanel: sendingPanel);
            }
            catch (ObsoleteOwnerRequestException) { return; }
            catch (System.Net.Http.HttpRequestException exception)
                when (exception.StatusCode == System.Net.HttpStatusCode.Conflict && conflictMessage is not null)
            {
                SetStatus(conflictMessage, good: false);
            }
            catch (System.Net.Http.HttpRequestException exception) when (exception.StatusCode is not null)
            {
                var refused = (int)exception.StatusCode is >= 400 and < 500 &&
                    exception.StatusCode != System.Net.HttpStatusCode.RequestTimeout;
                SetStatus($"The server did not {(refused ? "accept" : "confirm")} your request · {FriendlyFailure(exception)}", good: false);
                if (retainedSubmission is not null && IsCurrentWorldRequest(generation) && refused)
                    CompletePendingSubmission(retainedSubmission);
            }
            catch (System.Net.Http.HttpRequestException exception)
            {
                ShowHeldState($"could not reach the world host · {FriendlyFailure(exception)}");
            }
            catch (OperationCanceledException exception)
            {
                ShowHeldState($"the world host did not respond · {FriendlyFailure(exception)}");
            }
            catch (Exception exception)
            {
                SetStatus($"Could not complete that action · {FriendlyFailure(exception)}", good: false);
            }
            contextRetained = true;
        }, waitForTurn);
        return contextRetained;
    }
}
