using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Server.Providers;

namespace ClankerWorld.Server.Pairing;

/// <summary>
/// Transport DTOs and canonical bytes for the owner-device HTTP boundary.
/// These types intentionally carry no bearer token. Every protected operation
/// is authorized by consuming a short-lived, one-use signed challenge.
/// </summary>





public sealed record StartOwnerPairingHttpRequest(string PublicKeySpkiBase64);

public sealed record ActivateOwnerPairingHttpRequest(
    string PairingId,
    string CanonicalProof,
    string SignatureBase64);

public sealed record IssueOwnerChallengeHttpRequest(
    string DeviceId,
    string RequestId,
    string CanonicalProof,
    string SignatureBase64);

public sealed record OwnerSignedHttpRequest<TAction>(
    string DeviceId,
    string ChallengeId,
    string Nonce,
    string Binding,
    string CanonicalProof,
    string SignatureBase64,
    string RequestId,
    TAction Action)
    where TAction : class;

















/// <summary>
/// Deliberately empty action body for a signed paired-device registry query.
/// The endpoint and its fixed canonical payload make this read just as
/// challenge-bound and one-use as a world-changing owner request.
/// </summary>


/// <summary>
/// Empty signed action used to read only non-secret provider status. API keys
/// are never represented in the response contract.
/// </summary>








/// <summary>
/// Asks the host for the game's model list for a provider. With
/// <see cref="CheckKey"/>, the host also asks the provider which of those
/// models a key can use. <see cref="ApiKey"/> is a key the owner has just
/// pasted and not saved yet; it is used for this check only. Without it, the
/// named key slot or the provider's saved key is used.
/// </summary>


/// <summary>
/// Explicitly tests one model with either a saved provider key, a named
/// key slot, or a key pasted for this request only, at the chosen thinking
/// level (<see langword="null"/> for the model default). This action does not
/// change provider configuration and always represents one metered model call.
/// </summary>


/// <summary>A bounded result for an owner-triggered, metered model setup check.</summary>


/// <summary>One listed model, and whether the checked key can use it.</summary>


/// <summary>
/// The game's models for a provider, in display order. <see cref="DefaultModel"/>
/// is the game's default for this provider, chosen for a new agent when the
/// key can use it. <see cref="Error"/> explains, in plain words, a key that
/// couldn't be checked; the models are then all shown as usable.
/// </summary>




/// <summary>
/// One agent's model for one role. <see cref="Thinking"/> is how much a hosted
/// model reasons before answering: low, medium or high, or
/// <see langword="null"/> for the model's own default, which saved files leave out.
/// </summary>






























/// <summary>
/// A stable scalar representation keeps authoring requests independent of a
/// client JSON serializer's ordering or polymorphism behavior. The server
/// maps it to a typed simulation operation only after signed authorization.
/// </summary>




public static class OwnerHttpBinding
{
    public const string Domain = "clankerworld.owner-http-binding.v1";

    public static string Create(
        string method,
        string path,
        string requestId,
        string canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        if (!path.StartsWith('/') || path.Contains('?'))
        {
            throw new ArgumentException("A binding path must be an absolute path without a query string.", nameof(path));
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload));
        return string.Join(
            '\n',
            Domain,
            $"method={ToBase64Url(Encoding.UTF8.GetBytes(method.ToUpperInvariant()))}",
            $"path={ToBase64Url(Encoding.UTF8.GetBytes(path))}",
            $"request-id={ToBase64Url(Encoding.UTF8.GetBytes(requestId))}",
            $"payload-sha256={ToBase64Url(digest)}");
    }

    public const string WorldControlPayloadDomain = OwnerActionPayload.WorldControlPayloadDomain;
    public const string FirstTownLayoutPayloadDomain = OwnerActionPayload.FirstTownLayoutPayloadDomain;

    public static string WorldControl(OwnerWorldControlAction action) =>
        OwnerActionPayload.WorldControl(action);

    public static string ReconnectPayload(OwnerReconnectAction action) =>
        OwnerActionPayload.Reconnect(action);

    public static string EmptyPayload(string operation) => string.Join(
        '\n',
        "clankerworld.owner-control.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}");

    public static string Deletion(OwnerDeletionAction action) =>
        OwnerActionPayload.Deletion(action);

    public static string RecoveryCleanup(OwnerRecoveryCleanupAction action) =>
        OwnerActionPayload.RecoveryCleanup(action);

    public static string ManualSavePayload(OwnerManualSaveAction action) =>
        OwnerActionPayload.ManualSave(action);

    public const string WorldCreationPayloadDomain = OwnerActionPayload.WorldCreationPayloadDomain;
    public const string AgentPlacementPayloadDomain = "clankerworld.owner-agent-placement.v2";

    /// <summary>
    /// Advertised by hosts that accept Anthropic models and a thinking line in
    /// provider configuration, setup check and placement payloads, so a newer
    /// client can ask an older host to update instead of failing a signature.
    /// </summary>
    public const string ModelThinkingPayloadDomain = "clankerworld.owner-model-thinking.v1";

    public static string WorldCreationPayload(OwnerWorldCreationAction action) =>
        OwnerActionPayload.WorldCreation(action);

    public const string AutosaveConfigurationPayloadDomain = OwnerActionPayload.AutosaveConfigurationPayloadDomain;

    public static string AutosaveConfigurationPayload(OwnerAutosaveConfigurationAction action) =>
        OwnerActionPayload.AutosaveConfiguration(action);

    public static string DeveloperEditPayload(OwnerDeveloperEditAction action) =>
        OwnerActionPayload.DeveloperEdit(action);

    public const string AgingOverridePayloadDomain = "clankerworld.owner-aging-override.v2";
    public const string JevAssistancePayloadDomain = "clankerworld.owner-jev-assistance.v2";

    public static string AgingOverridePayload(OwnerAgingOverrideAction action) => string.Join('\n',
        AgingOverridePayloadDomain,
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"rate={action.Rate.ToString(CultureInfo.InvariantCulture)}");

    public static string JevAssistancePayload(OwnerJevAssistanceAction action) => string.Join('\n',
        JevAssistancePayloadDomain,
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"enabled={action.Enabled.ToString().ToLowerInvariant()}");

    public static string RoutineHelperPayload(OwnerRoutineHelperAction action) =>
        OwnerActionPayload.RoutineHelper(action);

    public static string PairingApprovalPayload(OwnerPairingApprovalAction action) => string.Join(
        '\n',
        "clankerworld.owner-pairing-approval.v1",
        $"pairing-id={EncodeRequired(action.PairingId, nameof(action.PairingId))}",
        $"pairing-code={EncodeRequired(action.PairingCode, nameof(action.PairingCode))}");

    public static string DeviceManagementPayload(OwnerDeviceManagementAction action) => string.Join(
        '\n',
        "clankerworld.owner-device-management.v1",
        $"device-id={EncodeRequired(action.DeviceId, nameof(action.DeviceId))}");

    public static string DeviceListPayload() => EmptyPayload("list_devices");

    public static string ProviderStatusPayload() => EmptyPayload("provider_status");

    public static string UsageStatusPayload() => EmptyPayload("usage_status");

    public static string UsageLimitPayload(OwnerUsageLimitAction action) =>
        OwnerActionPayload.UsageLimit(action);

    public static string CredentialSlotCreationPayload(OwnerCredentialSlotCreationAction action) =>
        OwnerActionPayload.CredentialSlotCreation(action);

    public static string CredentialSlotDeletionPayload(OwnerCredentialSlotDeletionAction action) =>
        OwnerActionPayload.CredentialSlotDeletion(action);

    public static string ProviderModelListPayload(OwnerProviderModelListAction action) =>
        OwnerActionPayload.ProviderModelList(action);

    public const string HelperSetupCheckPayloadDomain = "clankerworld.owner-helper-setup-check.v1";

    public static string ProviderSetupCheckPayload(OwnerProviderSetupCheckAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = ModelThinking.Normalize(action.Thinking);
        var provider = PlayerDecisionProviders.Normalize(action.Provider);
        if (!PlayerDecisionProviders.IsModelProvider(provider) && provider is not ("jev" or "decisions"))
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

    public static string ProviderConfigurationPayload(OwnerProviderConfigurationAction action) =>
        OwnerActionPayload.ProviderConfiguration(action);

    public static string FounderPlacementPayload(OwnerFounderPlacementAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var cognition = ProviderConfigurationPayload(action.Cognition);
        var digest = ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(cognition)));
        return string.Join('\n',
            "clankerworld.owner-founder-placement.v1",
            $"founder={EncodeRequired(action.FounderId, nameof(action.FounderId))}",
            $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
            $"y={action.Y.ToString(CultureInfo.InvariantCulture)}",
            $"cognition-sha256={digest}");
    }

    public static string FirstTownLayoutPayload(OwnerFirstTownLayoutAction action) =>
        OwnerActionPayload.FirstTownLayout(action);

    public static string FounderMovePayload(OwnerFounderMoveAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return string.Join('\n',
            "clankerworld.owner-founder-move.v1",
            $"founder={EncodeRequired(action.FounderId, nameof(action.FounderId))}",
            $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
            $"y={action.Y.ToString(CultureInfo.InvariantCulture)}");
    }

    public static string FounderUndoPayload(OwnerFounderUndoAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return string.Join('\n', "clankerworld.owner-founder-undo.v1",
            $"founder={EncodeRequired(action.FounderId, nameof(action.FounderId))}");
    }

    public static string AgentPlacementPayload(OwnerAgentPlacementAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var cognition = ProviderConfigurationPayload(action.Cognition);
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

    public static string AgentRenamePayload(OwnerAgentRenameAction action) =>
        OwnerActionPayload.AgentRename(action);

    public static string InstructionPayload(OwnerInstructionAction action) =>
        OwnerActionPayload.Instruction(action);

    public static string OrderCancelPayload(OwnerOrderCancelAction action) =>
        OwnerActionPayload.OrderCancel(action);

    public static string AuthoringPayload(OwnerAuthoringBatchAction action)
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
            var operation = action.Operations[index] ?? throw new ArgumentException("Authoring operations cannot contain null.", nameof(action));
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

    private static string EncodeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return ToBase64Url(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeOptional(string? value) => value is null
        ? "-"
        : ToBase64Url(Encoding.UTF8.GetBytes(value));

    private static string EncodeOptionalInteger(int? value) => value is null
        ? "-"
        : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string EncodeOptionalBoolean(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => "-",
    };

    private static string ToBase64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

public sealed record OwnerRequestAuthorization(
    string DeviceId,
    string PublicKeyFingerprint,
    string RequestId);

public sealed record OwnerRequestAuthorizationResult(
    OwnerAuthorityFailure Failure,
    OwnerRequestAuthorization? Value)
{
    public bool IsSuccess => Failure == OwnerAuthorityFailure.None && Value is not null;
}

/// <summary>
/// Single ingress guard for signed owner operations. It compares the server's
/// reconstructed binding before consuming a process-local one-use challenge.
/// Restart invalidates all old challenges; only durable pairing/device changes
/// require a file rewrite before a caller asks the simulation to commit.
/// </summary>
public sealed class OwnerRequestAuthorizer(
    OwnerAuthorityStore authority,
    OwnerAuthorityStateFile stateFile)
{
    public OwnerRequestAuthorizationResult Authorize<TAction>(
        OwnerSignedHttpRequest<TAction>? request,
        string method,
        string path,
        string canonicalPayload)
        where TAction : class
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.ChallengeId) ||
            string.IsNullOrWhiteSpace(request.Nonce) ||
            string.IsNullOrWhiteSpace(request.CanonicalProof) ||
            string.IsNullOrWhiteSpace(request.SignatureBase64) ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            request.Action is null)
        {
            return new OwnerRequestAuthorizationResult(OwnerAuthorityFailure.InvalidRequest, null);
        }

        var expectedBinding = OwnerHttpBinding.Create(method, path, request.RequestId, canonicalPayload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedBinding),
                Encoding.UTF8.GetBytes(request.Binding ?? string.Empty)))
        {
            return new OwnerRequestAuthorizationResult(OwnerAuthorityFailure.InvalidCanonicalProof, null);
        }

        var consumed = authority.ConsumeChallenge(new OwnerChallengeConsumeRequest(
            request.DeviceId,
            request.ChallengeId,
            request.Nonce,
            expectedBinding,
            request.CanonicalProof,
            request.SignatureBase64));
        // Persist any pairing/device cleanup, but not process-local challenges.
        // A restart discards all pending and consumed challenges, failing closed.
        stateFile.Save(authority);
        if (!consumed.IsSuccess)
        {
            return new OwnerRequestAuthorizationResult(consumed.Failure, null);
        }

        return new OwnerRequestAuthorizationResult(
            OwnerAuthorityFailure.None,
            new OwnerRequestAuthorization(
                consumed.Value!.DeviceId,
                consumed.Value.PublicKeyFingerprint,
                request.RequestId));
    }
}
