using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.GodotClient.Protocol;

/// <summary>
/// Canonical scalar payloads for every currently exposed owner operation.
/// Matching operations use shared builders rather than trusting JSON field order.
/// </summary>
public static class OwnerWorldActionPayload
{
    public static string Reconnect(OwnerReconnectAction action) =>
        OwnerActionPayload.Reconnect(action);

    public static string Control(string operation) => string.Join(
        '\n',
        "clankerworld.owner-control.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}");

    public static string Deletion(OwnerDeletionAction action) =>
        OwnerActionPayload.Deletion(action);

    public static string RecoveryCleanup(OwnerRecoveryCleanupAction action) =>
        OwnerActionPayload.RecoveryCleanup(action);

    public static string ManualSave(OwnerManualSaveAction action) =>
        OwnerActionPayload.ManualSave(action);

    public const string WorldCreationPayloadDomain = OwnerActionPayload.WorldCreationPayloadDomain;
    public const string AgentPlacementPayloadDomain = "clankerworld.owner-agent-placement.v2";

    /// <summary>
    /// A host that advertises this accepts Anthropic models and a thinking
    /// line. Hosts that don't are asked to update instead of failing a signature.
    /// </summary>
    public const string ModelThinkingPayloadDomain = "clankerworld.owner-model-thinking.v1";

    /// <summary>Whether a request names Anthropic or a thinking level, which older hosts don't accept.</summary>
    public static bool NeedsModelThinkingHost(string? provider, string? thinking = null) =>
        thinking is not null || provider?.Trim().ToLowerInvariant() is "anthropic" or "claude";

    public static string WorldCreation(OwnerWorldCreationAction action) =>
        OwnerActionPayload.WorldCreation(action);

    public const string AutosaveConfigurationPayloadDomain = OwnerActionPayload.AutosaveConfigurationPayloadDomain;

    public static string AutosaveConfiguration(OwnerAutosaveConfigurationAction action) =>
        OwnerActionPayload.AutosaveConfiguration(action);

    public static string DeveloperEdit(OwnerDeveloperEditAction action) =>
        OwnerActionPayload.DeveloperEdit(action);

    public const string AgingOverridePayloadDomain = "clankerworld.owner-aging-override.v2";
    public const string JevAssistancePayloadDomain = "clankerworld.owner-jev-assistance.v2";

    public static string AgingOverride(OwnerAgingOverrideAction action) => string.Join('\n',
        AgingOverridePayloadDomain,
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"rate={action.Rate.ToString(CultureInfo.InvariantCulture)}");

    public static string JevAssistance(OwnerJevAssistanceAction action) => string.Join('\n',
        JevAssistancePayloadDomain,
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"enabled={action.Enabled.ToString().ToLowerInvariant()}");

    public static string RoutineHelper(OwnerRoutineHelperAction action) =>
        OwnerActionPayload.RoutineHelper(action);

    public static string PairingApproval(OwnerPairingApprovalAction action) => string.Join(
        '\n',
        "clankerworld.owner-pairing-approval.v1",
        $"pairing-id={EncodeRequired(action.PairingId, nameof(action.PairingId))}",
        $"pairing-code={EncodeRequired(action.PairingCode, nameof(action.PairingCode))}");

    public static string DeviceManagement(OwnerDeviceManagementAction action) => string.Join(
        '\n',
        "clankerworld.owner-device-management.v1",
        $"device-id={EncodeRequired(action.DeviceId, nameof(action.DeviceId))}");

    public static string DeviceList() => Control("list_devices");

    public static string ProviderStatus() => Control("provider_status");

    public static string UsageStatus() => Control("usage_status");

    public static string UsageLimit(OwnerUsageLimitAction action) =>
        OwnerActionPayload.UsageLimit(action);

    public static string CredentialSlotCreation(OwnerCredentialSlotCreationAction action) =>
        OwnerActionPayload.CredentialSlotCreation(action);

    public static string CredentialSlotDeletion(OwnerCredentialSlotDeletionAction action) =>
        OwnerActionPayload.CredentialSlotDeletion(action);

    public static string ProviderModelList(OwnerProviderModelListAction action) =>
        OwnerActionPayload.ProviderModelList(action);

    public const string HelperSetupCheckPayloadDomain = "clankerworld.owner-helper-setup-check.v1";

    public static string ProviderSetupCheck(OwnerProviderSetupCheckAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var provider = action.Provider?.Trim().ToLowerInvariant();
        if (provider == "claude") provider = "anthropic";
        if (provider is not ("openai" or "ollama-cloud" or "anthropic" or "jev" or "decisions"))
            throw new ArgumentException("Choose a personal model provider, Jev or OpenAI Decisions for a setup check.", nameof(action));
        if (provider is "jev" or "decisions" && action.Thinking is not null)
            throw new ArgumentException("Routine helpers do not use a Thinking setting.", nameof(action));
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Model);
        if (action.Model.Length > 200 || action.Model.Any(char.IsControl))
            throw new ArgumentException("Model names must be 200 characters or fewer and contain no control characters.", nameof(action));
        if (action.ApiKey?.Length > 4096)
            throw new ArgumentException("API keys must be 4096 characters or fewer.", nameof(action));
        if (action.CredentialSlotId is { } slotId && !Guid.TryParseExact(slotId, "N", out _))
            throw new ArgumentException("Choose a valid saved key.", nameof(action));
        if (action.ApiKey is not null && action.CredentialSlotId is not null)
            throw new ArgumentException("Choose a pasted key or a saved key, not both.", nameof(action));
        var apiKeyDigest = action.ApiKey is null
            ? "-"
            : ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(action.ApiKey)));
        var payload = string.Join('\n', "clankerworld.owner-provider-setup-check.v1",
            $"provider={EncodeRequired(provider, nameof(action.Provider))}",
            $"model={EncodeRequired(action.Model.Trim(), nameof(action.Model))}",
            $"credential-slot={EncodeOptional(action.CredentialSlotId)}",
            $"api-key-sha256={apiKeyDigest}");
        if (action.Thinking is not null)
            payload += "\nthinking=" + EncodeRequired(action.Thinking, nameof(action.Thinking));
        return payload;
    }

    public static string ProviderConfiguration(OwnerProviderConfigurationAction action) =>
        OwnerActionPayload.ProviderConfiguration(action);

    public static string FounderPlacement(OwnerFounderPlacementAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var cognition = ProviderConfiguration(action.Cognition);
        var digest = ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(cognition)));
        return string.Join('\n',
            "clankerworld.owner-founder-placement.v1",
            $"founder={EncodeRequired(action.FounderId, nameof(action.FounderId))}",
            $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
            $"y={action.Y.ToString(CultureInfo.InvariantCulture)}",
            $"cognition-sha256={digest}");
    }

    public static string FirstTownLayout(OwnerFirstTownLayoutAction action) => string.Join('\n',
        "clankerworld.owner-first-town-layout.v1",
        $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
        $"y={action.Y.ToString(CultureInfo.InvariantCulture)}");

    public static string FounderMove(OwnerFounderMoveAction action) => string.Join('\n',
        "clankerworld.owner-founder-move.v1",
        $"founder={EncodeRequired(action.FounderId, nameof(action.FounderId))}",
        $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
        $"y={action.Y.ToString(CultureInfo.InvariantCulture)}");

    public static string FounderUndo(OwnerFounderUndoAction action) => string.Join('\n',
        "clankerworld.owner-founder-undo.v1",
        $"founder={EncodeRequired(action.FounderId, nameof(action.FounderId))}");

    public static string AgentPlacement(OwnerAgentPlacementAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var cognition = ProviderConfiguration(action.Cognition);
        var digest = ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(cognition)));
        return string.Join('\n',
            AgentPlacementPayloadDomain,
            $"agent={EncodeRequired(action.AgentId, nameof(action.AgentId))}",
            $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
            $"y={action.Y.ToString(CultureInfo.InvariantCulture)}",
            $"expected-household={EncodeOptional(action.ExpectedHouseholdId)}",
            $"expected-town={EncodeOptional(action.ExpectedTownId)}",
            $"cognition-sha256={digest}");
    }

    public static string AgentRename(OwnerAgentRenameAction action) =>
        OwnerActionPayload.AgentRename(action);

    public static string Instruction(OwnerInstructionAction action) =>
        OwnerActionPayload.Instruction(action);

    public static string OrderCancel(OwnerOrderCancelAction action) =>
        OwnerActionPayload.OrderCancel(action);

    public static string Authoring(OwnerAuthoringBatchAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(action.Operations);
        var lines = new List<string>
        {
            action.WorldId is null ? "clankerworld.owner-authoring.v1" : "clankerworld.owner-authoring.v2",
            $"batch-id={EncodeRequired(action.BatchId, nameof(action.BatchId))}",
            $"operation-count={action.Operations.Count.ToString(CultureInfo.InvariantCulture)}",
        };

        if (action.WorldId is not null)
        {
            lines.Add($"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}");
            lines.Add($"expected-event-id={action.ExpectedEventId.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"run-epoch={action.RunEpoch.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"observer-instance={EncodeOptional(action.Timeline?.InstanceId)}");
            lines.Add($"observer-generation={action.Timeline?.Generation.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        }

        for (var index = 0; index < action.Operations.Count; index++)
        {
            var operation = action.Operations[index] ?? throw new ArgumentException("Authoring operations cannot be null.", nameof(action));
            var prefix = $"op-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.kind={EncodeRequired(operation.Kind, nameof(operation.Kind))}");
            lines.Add($"{prefix}.id={EncodeOptional(operation.Id)}");
            lines.Add($"{prefix}.value={EncodeOptional(operation.Value)}");
            lines.Add($"{prefix}.secondary-value={EncodeOptional(operation.SecondaryValue)}");
            lines.Add($"{prefix}.x={EncodeOptionalInteger(operation.X)}");
            lines.Add($"{prefix}.y={EncodeOptionalInteger(operation.Y)}");
            lines.Add($"{prefix}.is-renewable={EncodeOptionalBoolean(operation.IsRenewable)}");
        }

        return string.Join('\n', lines);
    }

    public static string BuildingDesign(OwnerBuildingDesignAction action) => string.Join('\n',
        "clankerworld.owner-building-design.v1",
        "name=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(action.Name)),
        "purpose=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(action.Purpose)),
        "wood-cost=" + action.WoodCost.ToString(CultureInfo.InvariantCulture));

    public static string BuildingRemoval(OwnerBuildingRemovalAction action) =>
        OwnerActionPayload.BuildingRemoval(action);

    public static string BuildingReassignment(OwnerBuildingReassignmentAction action) =>
        OwnerActionPayload.BuildingReassignment(action);

    private static string EncodeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return ToBase64Url(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeOptional(string? value) => value is null ? "-" : ToBase64Url(Encoding.UTF8.GetBytes(value));

    private static string EncodeOptionalInteger(int? value) => value is null
        ? "-"
        : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string EncodeOptionalBoolean(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => "-",
    };

    private static string ToBase64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
