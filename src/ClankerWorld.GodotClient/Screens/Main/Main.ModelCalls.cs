using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.Protocol;
using Godot;
using System.Globalization;

namespace ClankerWorld.GodotClient;

/// <summary>
/// The model-call limit: how many model calls the world may make, the calls
/// made so far, and the pause when the limit is reached.
/// </summary>
public partial class Main
{
    private readonly Label usageScopeHint = new();
    private int usageReads;
    private bool usageLimitEdited;

    /// <summary>
    /// Reads the installation's model-call count for Game Settings without a
    /// status message, so opening Settings from the Main Menu stays quiet.
    /// </summary>
    private async Task RefreshUsageAsync()
    {
        var read = ++usageReads;
        var owner = registration;
        if (!TryGetOwner(out var authority, out var deviceId, out var signer))
        {
            usageMeterStatus.Text = "Connect this device to see model-call usage.";
            return;
        }
        try
        {
            var status = await ownerApi.GetUsageStatusAsync(ResolveWorldUri(), authority, deviceId,
                signer, CancellationToken.None);
            if (read != usageReads || !ReferenceEquals(owner, registration)) return;
            usageStatus = status;
            RenderUsageStatus();
        }
        catch (Exception exception)
        {
            if (read == usageReads && ReferenceEquals(owner, registration))
                usageMeterStatus.Text = "Could not load model calls: " + FriendlyFailure(exception);
        }
    }

    private async Task ObserveUsagePauseAsync()
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        var read = ++usageReads;
        var owner = registration;
        try
        {
            var status = await AwaitCurrentWorldResultAsync(ownerApi.GetUsageStatusAsync(ResolveWorldUri(), authority, deviceId,
                signer, CancellationToken.None));
            if (read != usageReads || !ReferenceEquals(owner, registration)) return;
            usageStatus = status;
            RenderUsageStatus();
            if (usageStatus.LimitReached)
                SetStatus(UsageLimitPausedMessage, good: false);
        }
        catch (Exception)
        {
            // A pause is authoritative even when the optional meter read is unavailable.
        }
    }

    private const string UsageLimitPausedMessage =
        "Model-call limit reached, so the world is paused. Raise the limit in Settings → Game, then resume.";

    private void RenderUsageStatus()
    {
        if (usageStatus is null)
        {
            usageMeterStatus.Text = "Loading model calls...";
            usageMeterStatus.TooltipText = string.Empty;
            grantUsageCallsButton.Hide();
            RefreshControlAvailability();
            return;
        }
        if (usageStatus.AccountingError is not null)
        {
            usageMeterStatus.Text = usageStatus.AccountingError;
            usageMeterStatus.TooltipText = "Call totals are unavailable until accounting is restored.";
            grantUsageCallsButton.Visible = false;
            RefreshControlAvailability();
            return;
        }
        if (!usageLimitEdited)
            usageAttemptLimitInput.Text = usageStatus.AttemptLimit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        var rows = usageStatus.Rows.OrderByDescending(row => row.Attempts)
            .Select(row => $"{row.Provider} / {row.Model}: {Count(row.Attempts)} calls");
        usageMeterStatus.Text = usageStatus.AttemptLimit is { } limit
            ? $"{Count(usageStatus.Attempts)} of {Count(limit)} calls used across all worlds."
            : $"{Count(usageStatus.Attempts)} calls used across all worlds. No limit is set.";
        if (usageStatus.LimitReached)
            usageMeterStatus.Text += " Time is paused. Raise the limit or allow more calls, then resume.";
        if (usageStatus.Rows.Count > 0)
            usageMeterStatus.Text += "\n" + string.Join("\n", rows);
        usageMeterStatus.TooltipText = $"Started {Count(usageStatus.Attempts)}, completed {Count(usageStatus.Completed)}, " +
            $"failed {Count(usageStatus.Failed)} and interrupted {Count(usageStatus.Abandoned)}; providers reported " +
            $"{Count(usageStatus.InputTokens)} input and {Count(usageStatus.OutputTokens)} output tokens, shown for information only.";
        grantUsageCallsButton.Visible = usageStatus.LimitReached;
        RefreshControlAvailability();
    }

    private async Task ConfigureUsageAsync(bool grant)
    {
        if (!TryGetOwner(out var authority, out var deviceId, out var signer)) return;
        long? cap = null;
        if (!grant && !string.IsNullOrWhiteSpace(usageAttemptLimitInput.Text))
        {
            if (!long.TryParse(usageAttemptLimitInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
                parsed is < 1 or > 1_000_000)
            {
                SetStatus("Enter a number of calls from 1 to 1,000,000, or leave it blank for no limit.", good: false);
                return;
            }
            cap = parsed;
        }
        var action = grant ? new OwnerUsageLimitAction(null, AdditionalCalls: 100) :
            new OwnerUsageLimitAction(cap);
        await RunOwnerActionAsync(async () =>
        {
            var status = await AwaitCurrentWorldResultAsync(ownerApi.ConfigureUsageLimitAsync(ResolveWorldUri(), authority, deviceId,
                action, signer, CancellationToken.None));
            usageReads++;
            usageStatus = status;
            usageLimitEdited = false;
            RenderUsageStatus();
            if (grant)
                return "Allowed 100 more model calls. Resume the world when ready";
            return cap is null ? "Model-call limit turned off"
                : string.Create(CultureInfo.InvariantCulture, $"Model-call limit set to {cap:N0} calls");
        });
    }

    /// <summary>
    /// The installation's model-call count and optional limit. One count and
    /// limit cover every world, so the box sits on the Game page and opens
    /// from the Main Menu as well as in a world.
    /// </summary>
    private void BuildUsageLimitPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        usageScopeHint.Text = "Counts every model call attempt in all your worlds, including ones that fail or are retried. " +
            "The count never resets; the Event Log warns you once at 80% of the limit.";
        usageScopeHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        usageScopeHint.ThemeTypeVariation = "DimLabel";
        body.AddChild(usageScopeHint);
        usageMeterStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(usageMeterStatus);
        usageAttemptLimitInput.PlaceholderText = "No limit";
        usageAttemptLimitInput.TooltipText = "Your worlds pause when this many calls have been made; it counts calls, not money.";
        usageAttemptLimitInput.TextChanged += _ => usageLimitEdited = true;
        body.AddChild(DisplaySettingRow("Limit", usageAttemptLimitInput));
        var usageButtons = new HBoxContainer();
        usageButtons.AddThemeConstantOverride("separation", 6);
        applyUsageLimitButton.Text = "Set limit";
        StyleButton(applyUsageLimitButton);
        applyUsageLimitButton.Pressed += () => _ = ConfigureUsageAsync(grant: false);
        usageButtons.AddChild(applyUsageLimitButton);
        grantUsageCallsButton.Text = "Allow 100 more calls";
        grantUsageCallsButton.TooltipText = "Allow 100 more model calls; the world stays paused until you resume it.";
        StyleButton(grantUsageCallsButton, primary: true);
        grantUsageCallsButton.Pressed += () => _ = ConfigureUsageAsync(grant: true);
        grantUsageCallsButton.Visible = false;
        usageButtons.AddChild(grantUsageCallsButton);
        refreshUsageButton.Text = "Refresh";
        StyleButton(refreshUsageButton);
        refreshUsageButton.Pressed += () => _ = RefreshUsageAsync();
        usageButtons.AddChild(refreshUsageButton);
        body.AddChild(usageButtons);
        AddPanelContents(usageLimitPanel, "Model calls", body);
        usageLimitPanel.ThemeTypeVariation = "InsetPanel";
        RenderUsageStatus();
    }
}
