using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.GodotClient.UI;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private readonly Button founderModelSetupCheckButton = new();
    private readonly Label founderModelSetupCheckStatus = new();
    private readonly Button agentModelSetupCheckButton = new();
    private readonly Label agentModelSetupCheckStatus = new();
    private string? agentModelSetupCheckContext;
    private int founderModelSetupCheckRevision;
    private int agentModelSetupCheckRevision;

    private async Task RunFounderModelSetupCheckAsync()
    {
        var provider = SelectedFounderProvider();
        var model = EmptyToNull(founderModelPicker.Model);
        var credentialChoice = SelectedFounderCredential();
        var apiKey = credentialChoice == "new" ? EmptyToNull(founderApiKeyInput.Text) : null;
        if (credentialChoice == "new" && apiKey is null)
        {
            founderModelSetupCheckStatus.Text = "Add the new key first. Nothing was sent.";
            founderModelSetupCheckStatus.Show();
            return;
        }
        if (model is null)
        {
            founderModelSetupCheckStatus.Text = "Choose a model before testing it.";
            founderModelSetupCheckStatus.Show();
            return;
        }

        var slotId = credentialChoice is "default" or "new" ? null : credentialChoice;
        var action = new OwnerProviderSetupCheckAction(provider, model, slotId, apiKey, SelectedThinking(founderThinkingChoice));
        await RunModelSetupCheckAsync(
            founderModelSetupCheckButton,
            founderModelSetupCheckStatus,
            provider,
            model,
            action,
            () => founderModelSetupCheckRevision);
    }

    private async Task RunAgentModelSetupCheckAsync()
    {
        ResetAgentModelSetupCheckForCurrentChoice();
        var provider = SelectedProviderId();
        var model = EmptyToNull(agentModelPicker.Model);
        var credentialChoice = SelectedCredentialChoice();
        var apiKey = credentialChoice == "new" ? EmptyToNull(agentModelApiKeyInput.Text) : null;
        if (credentialChoice == "new" && apiKey is null)
        {
            agentModelSetupCheckStatus.Text = "Add the new key first. Nothing was sent.";
            agentModelSetupCheckStatus.Show();
            return;
        }
        if (model is null)
        {
            agentModelSetupCheckStatus.Text = "Choose a model before testing it.";
            agentModelSetupCheckStatus.Show();
            return;
        }

        var slotId = credentialChoice is "default" or "new" ? null : credentialChoice;
        var action = new OwnerProviderSetupCheckAction(provider, model, slotId, apiKey, SelectedThinking(agentModelThinkingChoice));
        await RunModelSetupCheckAsync(
            agentModelSetupCheckButton,
            agentModelSetupCheckStatus,
            provider,
            model,
            action,
            () => agentModelSetupCheckRevision);
    }

    private async Task RunModelSetupCheckAsync(
        Button button,
        Label resultLabel,
        string provider,
        string model,
        OwnerProviderSetupCheckAction action,
        Func<int> currentRevision)
    {
        if (button.Disabled)
            return;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            resultLabel.Text = "Connect this device before testing a model. Nothing was sent.";
            resultLabel.Show();
            return;
        }

        var testedRevision = currentRevision();
        var generation = observationSession.RequestGeneration;
        button.Disabled = true;
        resultLabel.Show();
        resultLabel.Text = $"Checking {provider} / {model}. This sends one paid call.";
        try
        {
            await RunOwnerActionAsync(async () =>
            {
                try
                {
                    var result = await AwaitCurrentWorldResultAsync(ownerApi.CheckProviderSetupAsync(
                        ResolveWorldUri(), authority, deviceId, action, signer, OwnerRequestToken));
                    if (currentRevision() != testedRevision) throw new ObsoleteOwnerRequestException();
                    resultLabel.Text = $"{ProviderDisplayName(provider)} / {model}: {result.Message}";
                    return $"{ProviderDisplayName(provider)} / {model}: {result.Message}";
                }
                catch (OwnerActionCompatibilityException exception)
                {
                    var message = $"{FriendlyFailure(exception)}. Nothing was sent.";
                    if (currentRevision() != testedRevision) throw new ObsoleteOwnerRequestException();
                    resultLabel.Text = $"{ProviderDisplayName(provider)} / {model}: {message}";
                    return message;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException &&
                    exception is not ObsoleteOwnerRequestException)
                {
                    if (currentRevision() != testedRevision) throw new ObsoleteOwnerRequestException();
                    resultLabel.Text = $"{ProviderDisplayName(provider)} / {model}: No result arrived. Check your model-call count before trying again.";
                    return "The test result didn't arrive. Check your model-call count before trying again.";
                }
            });
        }
        finally
        {
            if (IsCurrentWorldRequest(generation)) button.Disabled = false;
            RefreshControlAvailability();
        }
    }

    private void ResetAgentModelSetupCheckForCurrentChoice()
    {
        var context = $"{SelectedAgentModelTarget()}|{SelectedProviderId()}|{agentModelPicker.Model}|{SelectedCredentialChoice()}|{SelectedThinking(agentModelThinkingChoice)}";
        if (context == agentModelSetupCheckContext)
            return;
        agentModelSetupCheckContext = context;
        ClearAgentModelSetupCheck();
    }

    private void ClearFounderModelSetupCheck()
    {
        ++founderModelSetupCheckRevision;
        founderModelSetupCheckStatus.Text = string.Empty;
        founderModelSetupCheckStatus.Hide();
    }

    private void ClearAgentModelSetupCheck()
    {
        ++agentModelSetupCheckRevision;
        agentModelSetupCheckStatus.Text = string.Empty;
        agentModelSetupCheckStatus.Hide();
    }

    private void VerifyModelSetupCheckControls()
    {
        if (founderModelSetupCheckButton.GetParent() != founderModelPicker.GetParent() ||
            agentModelSetupCheckButton.GetParent() != agentModelPicker.GetParent() ||
            founderModelSetupCheckButton.Text != "Test model · 1 paid call" ||
            agentModelSetupCheckButton.Text != "Test model · 1 paid call" ||
            !founderModelSetupCheckButton.TooltipText.Contains("counts toward your model-call limit", StringComparison.OrdinalIgnoreCase) ||
            !agentModelSetupCheckButton.TooltipText.Contains("counts toward your model-call limit", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Add Agent and an agent's Model panel must expose the same explicit one-paid-call setup check beside the model picker.");

        try
        {
            VerifySetupCheckInvalidation(founderModelPicker, founderApiKeyInput, founderModelSetupCheckStatus,
                founderCredentialChoice, () => founderModelSetupCheckRevision, isNewAgent: true);
            VerifySetupCheckInvalidation(agentModelPicker, agentModelApiKeyInput, agentModelSetupCheckStatus,
                agentModelCredentialChoice, () => agentModelSetupCheckRevision);
        }
        finally
        {
            // The smoke edits must not start delayed model-list lookups.
            ++founderKeyEdits;
            ++agentModelKeyEdits;
        }
    }

    private static void VerifySetupCheckInvalidation(ModelPicker picker, LineEdit keyInput, Label status,
        OptionButton credentialChoice, Func<int> currentRevision, bool isNewAgent = false)
    {
        var originalModel = picker.Model;
        var originalTyping = picker.IsTyping;
        var originalKey = keyInput.Text;
        try
        {
            VerifyCleared("typed model", () => EditText(picker.TypedInput, "setup-check-smoke-model"));
            VerifyCleared("chosen model", () => picker.Choice.EmitSignal(OptionButton.SignalName.ItemSelected, picker.Choice.ItemCount - 1));
            VerifyCleared("pasted key", () => EditText(keyInput, "setup-check-smoke-key"));
            VerifyCleared("chosen key", () => credentialChoice.EmitSignal(OptionButton.SignalName.ItemSelected, credentialChoice.Selected));
        }
        finally
        {
            keyInput.Text = originalKey;
            if (originalTyping) picker.ShowTypedOnly(originalModel);
            else picker.SetModel(originalModel, isNewAgent);
            status.Text = string.Empty;
            status.Hide();
        }

        void VerifyCleared(string choice, Action edit)
        {
            status.Text = "The previous model was ready.";
            status.Show();
            var testedRevision = currentRevision();
            edit();
            if (status.Text.Length > 0 || status.Visible || currentRevision() == testedRevision)
                throw new InvalidOperationException($"Changing the {choice} must clear the previous setup-check result and invalidate a reply still in flight.");
        }

        static void EditText(LineEdit input, string text)
        {
            input.Text = text;
            // Setting Text does not emit the signal Godot sends for a user edit.
            input.EmitSignal(LineEdit.SignalName.TextChanged, text);
        }
    }
}
