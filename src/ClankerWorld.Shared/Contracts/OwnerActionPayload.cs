using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Contracts;

/// <summary>Canonical text covered by signed owner requests on both client and server.</summary>
public static class OwnerActionPayload
{
    public const string AutosaveConfigurationPayloadDomain = "clankerworld.owner-autosave-configuration.v2";

    public const string WorldCreationPayloadDomain = "clankerworld.owner-world-creation.v3";

    public static string Reconnect(OwnerReconnectAction action) =>
        action.KnownMapLayersDigest is not null
            ? string.Join('\n', "clankerworld.owner-reconnect.v3",
                $"after-event-id={action.AfterEventId.ToString(CultureInfo.InvariantCulture)}",
                $"terrain-world-id={EncodeRequired(action.KnownTerrainWorldId!, nameof(action.KnownTerrainWorldId))}",
                $"terrain-digest={EncodeRequired(action.KnownTerrainDigest!, nameof(action.KnownTerrainDigest))}",
                $"map-layers-digest={EncodeRequired(action.KnownMapLayersDigest, nameof(action.KnownMapLayersDigest))}")
            : action.KnownTerrainWorldId is null && action.KnownTerrainDigest is null
                ? string.Join('\n', "clankerworld.owner-reconnect.v1",
                    $"after-event-id={action.AfterEventId.ToString(CultureInfo.InvariantCulture)}")
                : string.Join('\n', "clankerworld.owner-reconnect.v2",
                    $"after-event-id={action.AfterEventId.ToString(CultureInfo.InvariantCulture)}",
                    $"terrain-world-id={EncodeRequired(action.KnownTerrainWorldId!, nameof(action.KnownTerrainWorldId))}",
                    $"terrain-digest={EncodeRequired(action.KnownTerrainDigest!, nameof(action.KnownTerrainDigest))}");

    public static string Deletion(OwnerDeletionAction action) => string.Join(
        '\n', "clankerworld.owner-deletion.v1",
        $"kind={EncodeRequired(action.Kind, nameof(action.Kind))}",
        $"id={EncodeRequired(action.Id, nameof(action.Id))}",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"created-utc={action.ExpectedCreatedUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "-"}");

    public static string RecoveryCleanup(OwnerRecoveryCleanupAction action) => string.Join(
        '\n', "clankerworld.owner-recovery-cleanup.v1",
        $"operation={EncodeRequired(action.Operation, nameof(action.Operation))}",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"keep-count={action.KeepCount.ToString(CultureInfo.InvariantCulture)}",
        $"expected-digest={EncodeOptional(action.ExpectedDigest)}");

    public static string ManualSave(OwnerManualSaveAction action) => string.Join(
        '\n',
        "clankerworld.owner-manual-save.v1",
        $"operation={EncodeRequired(action.Operation, nameof(action.Operation))}",
        $"value={EncodeRequired(action.Value, nameof(action.Value))}") +
        (action.ModelSettingsApproval is null ? string.Empty : "\nmodel-settings=" + EncodeRequired(action.ModelSettingsApproval, nameof(action.ModelSettingsApproval)));

    public static string WorldCreation(OwnerWorldCreationAction action) => string.Join(
        '\n',
        WorldCreationPayloadDomain,
        $"name={EncodeRequired(action.Name, nameof(action.Name))}",
        $"seed={EncodeRequired(action.Seed, nameof(action.Seed))}",
        $"size={EncodeRequired(action.Size, nameof(action.Size))}",
        $"water-percent={action.WaterPercent.ToString(CultureInfo.InvariantCulture)}",
        $"wrap-east-west={action.WrapEastWest.ToString().ToLowerInvariant()}",
        $"climate-mode={EncodeRequired(action.ClimateMode, nameof(action.ClimateMode))}",
        $"selected-climate={EncodeRequired(action.SelectedClimate, nameof(action.SelectedClimate))}",
        $"latitude-cooling={action.LatitudeCooling.ToString().ToLowerInvariant()}",
        $"resource-abundance={EncodeRequired(action.ResourceAbundance, nameof(action.ResourceAbundance))}",
        $"forest-cover={EncodeRequired(action.ForestCover, nameof(action.ForestCover))}",
        $"mountain-relief={EncodeRequired(action.MountainRelief, nameof(action.MountainRelief))}",
        $"river-abundance={EncodeRequired(action.RiverAbundance, nameof(action.RiverAbundance))}",
        $"candidate-attempt={action.CandidateAttempt?.ToString(CultureInfo.InvariantCulture) ?? "-"}",
        $"expected-manifest-digest={EncodeOptional(action.ExpectedManifestDigest)}",
        $"expected-map-layers-digest={EncodeOptional(action.ExpectedMapLayersDigest)}",
        $"accept-unmet-targets={action.AcceptUnmetTargets.ToString().ToLowerInvariant()}");

    public static string AutosaveConfiguration(OwnerAutosaveConfigurationAction action) => string.Join(
        '\n',
        AutosaveConfigurationPayloadDomain,
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"enabled={action.Enabled.ToString().ToLowerInvariant()}",
        $"interval-minutes={action.IntervalMinutes.ToString(CultureInfo.InvariantCulture)}",
        $"rotation-count={action.RotationCount.ToString(CultureInfo.InvariantCulture)}");

    public static string DeveloperEdit(OwnerDeveloperEditAction action) => string.Join(
        '\n', "clankerworld.owner-developer-edit.v1",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"expected-event-id={action.ExpectedEventId.ToString(CultureInfo.InvariantCulture)}",
        $"agent-id={EncodeRequired(action.AgentId, nameof(action.AgentId))}",
        $"operation={EncodeRequired(action.Operation, nameof(action.Operation))}",
        $"value={EncodeRequired(action.Value, nameof(action.Value))}",
        $"amount={action.Amount.ToString(CultureInfo.InvariantCulture)}",
        $"other-agent-id={EncodeOptional(action.OtherAgentId)}");

    public static string RoutineHelper(OwnerRoutineHelperAction action) => string.Join(
        '\n', "clankerworld.owner-routine-helper.v1",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"provider={EncodeRequired(action.Provider, nameof(action.Provider))}",
        $"model={EncodeOptional(action.Model)}",
        $"credential-slot-id={EncodeOptional(action.CredentialSlotId)}");

    public static string UsageLimit(OwnerUsageLimitAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.AttemptLimit is < 1 or > 1_000_000 || action.AdditionalCalls is < 0 or > 1_000_000 ||
            action.AdditionalCalls > 0 && action.AttemptLimit is not null)
            throw new ArgumentOutOfRangeException(nameof(action));
        return string.Join('\n', "clankerworld.owner-usage-limit.v1",
            $"attempt-limit={action.AttemptLimit?.ToString(CultureInfo.InvariantCulture) ?? "off"}",
            $"additional-calls={action.AdditionalCalls.ToString(CultureInfo.InvariantCulture)}");
    }

    public static string CredentialSlotCreation(OwnerCredentialSlotCreationAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(action.ApiKey);
        return string.Join('\n', "clankerworld.owner-credential-slot-creation.v1",
            $"credential-slot={EncodeRequired(action.CredentialSlotId, nameof(action.CredentialSlotId))}",
            $"provider={EncodeRequired(action.Provider, nameof(action.Provider))}",
            $"label={EncodeRequired(action.Label, nameof(action.Label))}",
            $"api-key-sha256={ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(action.ApiKey)))}");
    }

    public static string CredentialSlotDeletion(OwnerCredentialSlotDeletionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return string.Join('\n', "clankerworld.owner-credential-slot-deletion.v1",
            $"credential-slot={EncodeRequired(action.CredentialSlotId, nameof(action.CredentialSlotId))}");
    }

    public static string ProviderModelList(OwnerProviderModelListAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var apiKeyDigest = action.ApiKey is null
            ? "-"
            : ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(action.ApiKey)));
        return string.Join('\n', "clankerworld.owner-provider-models.v1",
            $"provider={EncodeRequired(action.Provider, nameof(action.Provider))}",
            $"credential-slot={EncodeOptional(action.CredentialSlotId)}",
            $"api-key-sha256={apiKeyDigest}",
            $"check-key={action.CheckKey.ToString().ToLowerInvariant()}");
    }

    public static string ProviderConfiguration(OwnerProviderConfigurationAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var apiKeyDigest = action.ApiKey is null
            ? "-"
            : ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(action.ApiKey)));
        var payload = string.Join(
            '\n',
            "clankerworld.owner-provider-configuration.v1",
            $"role={EncodeRequired(action.Role, nameof(action.Role))}",
            $"provider={EncodeRequired(action.Provider, nameof(action.Provider))}",
            $"model={EncodeOptional(action.Model)}",
            $"api-key-sha256={apiKeyDigest}",
            $"forget-credential={action.ForgetCredential.ToString().ToLowerInvariant()}");
        if (action.AgentId is not null)
            payload += "\ninhabitant=" + EncodeRequired(action.AgentId, nameof(action.AgentId));
        if (action.CredentialSlotId is not null)
            payload += "\ncredential-slot=" + EncodeRequired(action.CredentialSlotId, nameof(action.CredentialSlotId));
        if (action.NewCredentialLabel is not null)
            payload += "\ncredential-label=" + EncodeRequired(action.NewCredentialLabel, nameof(action.NewCredentialLabel));
        if (action.Thinking is not null)
            payload += "\nthinking=" + EncodeRequired(action.Thinking, nameof(action.Thinking));
        return payload;
    }

    public static string AgentRename(OwnerAgentRenameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var name = EncodeRequired(action.Name, nameof(action.Name));
        return string.Join('\n',
            "clankerworld.owner-agent-rename.v1",
            $"agent={EncodeRequired(action.AgentId, nameof(action.AgentId))}",
            $"name-sha256={ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(name)))}");
    }

    public static string Instruction(OwnerInstructionAction action) => string.Join(
        '\n',
        action.Queue ? "clankerworld.owner-instruction.v3" : "clankerworld.owner-instruction.v2",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"idempotency-key={EncodeRequired(action.IdempotencyKey, nameof(action.IdempotencyKey))}",
        $"target-agent-id={EncodeRequired(action.TargetAgentId, nameof(action.TargetAgentId))}",
        $"kind={EncodeRequired(action.Kind, nameof(action.Kind))}",
        $"text={EncodeRequired(action.Text, nameof(action.Text))}" +
            (action.Queue ? "\nqueue=true" : string.Empty));

    public static string OrderCancel(OwnerOrderCancelAction action) => string.Join(
        '\n',
        "clankerworld.owner-order-cancel.v1",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}",
        $"idempotency-key={EncodeRequired(action.IdempotencyKey, nameof(action.IdempotencyKey))}",
        $"target-agent-id={EncodeRequired(action.TargetAgentId, nameof(action.TargetAgentId))}",
        $"order-id={EncodeRequired(action.OrderId, nameof(action.OrderId))}");

    public static string ContentPropose(OwnerContentPackageAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(action.Dependencies);
        ArgumentNullException.ThrowIfNull(action.Definitions);
        ArgumentNullException.ThrowIfNull(action.DeclaredCapabilities);
        var assets = action.Assets ?? [];
        var lines = new List<string>
        {
            "clankerworld.owner-content-propose.v1",
            $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}",
            $"version={EncodeRequired(action.Version, nameof(action.Version))}",
            $"package-digest={EncodeRequired(action.PackageDigest, nameof(action.PackageDigest))}",
            $"dependency-count={action.Dependencies.Count.ToString(CultureInfo.InvariantCulture)}",
            $"definition-count={action.Definitions.Count.ToString(CultureInfo.InvariantCulture)}",
            $"asset-count={assets.Count.ToString(CultureInfo.InvariantCulture)}",
            $"capability-count={action.DeclaredCapabilities.Count.ToString(CultureInfo.InvariantCulture)}",
        };

        for (var index = 0; index < action.Dependencies.Count; index++)
        {
            var dependency = action.Dependencies[index] ?? throw new ArgumentException(
                "Content dependencies cannot contain null.",
                nameof(action));
            var prefix = $"dependency-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.package-id={EncodeRequired(dependency.PackageId, nameof(dependency.PackageId))}");
            lines.Add($"{prefix}.minimum={EncodeRequired(dependency.MinimumVersion, nameof(dependency.MinimumVersion))}");
            lines.Add($"{prefix}.maximum={EncodeRequired(dependency.MaximumExclusiveVersion, nameof(dependency.MaximumExclusiveVersion))}");
            lines.Add($"{prefix}.optional={dependency.Optional.ToString().ToLowerInvariant()}");
        }

        for (var index = 0; index < action.Definitions.Count; index++)
        {
            var definition = action.Definitions[index] ?? throw new ArgumentException(
                "Content definitions cannot contain null.",
                nameof(action));
            var prefix = $"definition-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.kind={EncodeRequired(definition.Kind, nameof(definition.Kind))}");
            lines.Add($"{prefix}.local-id={EncodeRequired(definition.LocalId, nameof(definition.LocalId))}");
            lines.Add($"{prefix}.version={EncodeRequired(definition.Version, nameof(definition.Version))}");
            lines.Add($"{prefix}.display-name={EncodeRequired(definition.DisplayName, nameof(definition.DisplayName))}");
            lines.Add($"{prefix}.payload-digest={EncodeRequired(definition.PayloadDigest, nameof(definition.PayloadDigest))}");
            lines.Add($"{prefix}.payload-json={EncodeOptional(definition.PayloadJson)}");
        }

        for (var index = 0; index < assets.Count; index++)
        {
            var asset = assets[index] ?? throw new ArgumentException(
                "Content asset reservations cannot contain null.",
                nameof(action));
            var prefix = $"asset-{index.ToString(CultureInfo.InvariantCulture)}";
            lines.Add($"{prefix}.asset-id={EncodeRequired(asset.AssetId, nameof(asset.AssetId))}");
            lines.Add($"{prefix}.normalized-digest={EncodeRequired(asset.NormalizedDigest, nameof(asset.NormalizedDigest))}");
            lines.Add($"{prefix}.decode-profile={EncodeRequired(asset.DecodeProfile, nameof(asset.DecodeProfile))}");
            lines.Add($"{prefix}.durable-storage={asset.DurableStorageBytes.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}.decoded-cache={asset.DecodedCacheBytes.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}.gpu-bytes={asset.GpuBytes.ToString(CultureInfo.InvariantCulture)}");
            lines.Add($"{prefix}.render-units={asset.RenderUnits.ToString(CultureInfo.InvariantCulture)}");
        }

        for (var index = 0; index < action.DeclaredCapabilities.Count; index++)
        {
            lines.Add($"capability-{index.ToString(CultureInfo.InvariantCulture)}={EncodeRequired(
                action.DeclaredCapabilities[index],
                nameof(action.DeclaredCapabilities))}");
        }

        return string.Join('\n', lines);
    }

    public static string ContentPackageId(string operation, OwnerContentPackageIdAction action) => string.Join(
        '\n',
        "clankerworld.owner-content-lifecycle.v1",
        $"operation={EncodeRequired(operation, nameof(operation))}",
        $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}");

    public static string ContentRollback(OwnerContentRollbackAction action) => string.Join(
        '\n',
        "clankerworld.owner-content-rollback.v1",
        $"package-id={EncodeRequired(action.PackageId, nameof(action.PackageId))}",
        $"reason={EncodeRequired(action.Reason, nameof(action.Reason))}");

    public static string BuildingPlacement(OwnerBuildingPlacementAction action) => string.Join(
        '\n',
        "clankerworld.owner-building-placement.v1",
        $"instance-id={EncodeRequired(action.InstanceId, nameof(action.InstanceId))}",
        $"definition-id={EncodeRequired(action.DefinitionId, nameof(action.DefinitionId))}",
        $"x={action.X.ToString(CultureInfo.InvariantCulture)}",
        $"y={action.Y.ToString(CultureInfo.InvariantCulture)}");

    public static string BuildingRemoval(OwnerBuildingRemovalAction action) => string.Join(
        '\n',
        "clankerworld.owner-building-removal.v1",
        $"instance-id={EncodeRequired(action.InstanceId, nameof(action.InstanceId))}",
        $"expected-town-id={EncodeOptional(action.ExpectedTownId)}",
        $"expected-household-id={EncodeOptional(action.ExpectedHouseholdId)}",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}");

    public static string BuildingReassignment(OwnerBuildingReassignmentAction action) => string.Join(
        '\n',
        "clankerworld.owner-building-reassignment.v1",
        $"instance-id={EncodeRequired(action.InstanceId, nameof(action.InstanceId))}",
        $"expected-town-id={EncodeOptional(action.ExpectedTownId)}",
        $"expected-household-id={EncodeOptional(action.ExpectedHouseholdId)}",
        $"target-town-id={EncodeOptional(action.TargetTownId)}",
        $"target-household-id={EncodeOptional(action.TargetHouseholdId)}",
        $"world-id={EncodeRequired(action.WorldId, nameof(action.WorldId))}");

    public static string ProductionStart(OwnerProductionStartAction action) => string.Join(
        '\n',
        "clankerworld.owner-production-start.v1",
        $"recipe-id={EncodeRequired(action.RecipeId, nameof(action.RecipeId))}",
        $"building-instance-id={EncodeRequired(action.BuildingInstanceId, nameof(action.BuildingInstanceId))}",
        $"worker-id={EncodeRequired(action.WorkerId, nameof(action.WorkerId))}");

    private static string EncodeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return ToBase64Url(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeOptional(string? value) => value is null ? "-" : ToBase64Url(Encoding.UTF8.GetBytes(value));

    private static string ToBase64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
