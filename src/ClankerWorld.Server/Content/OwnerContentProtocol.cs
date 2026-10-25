using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Server.Content;

/// <summary>
/// Scalar, signed transport records for the world data-only content
/// lifecycle. The wire format never accepts a client-supplied lock digest or
/// activation tick; those are authoritative server outputs.
/// </summary>






















public static class OwnerContentBinding
{
    public static string ProposePayload(OwnerContentPackageAction action) =>
        OwnerActionPayload.ContentPropose(action);

    public static string PackageIdPayload(string operation, OwnerContentPackageIdAction action) =>
        OwnerActionPayload.ContentPackageId(operation, action);

    public static string RollbackPayload(OwnerContentRollbackAction action) =>
        OwnerActionPayload.ContentRollback(action);

    public static string BuildingPlacementPayload(OwnerBuildingPlacementAction action) =>
        OwnerActionPayload.BuildingPlacement(action);

    public static string BuildingRemovalPayload(OwnerBuildingRemovalAction action) =>
        OwnerActionPayload.BuildingRemoval(action);

    public static string BuildingReassignmentPayload(OwnerBuildingReassignmentAction action) =>
        OwnerActionPayload.BuildingReassignment(action);

    public static string ProductionStartPayload(OwnerProductionStartAction action) =>
        OwnerActionPayload.ProductionStart(action);

    public static bool TryMapManifest(
        OwnerContentPackageAction? action,
        out ContentPackageManifest? manifest,
        out string failure)
    {
        manifest = null;
        failure = string.Empty;
        if (action is null || action.Dependencies is null || action.Definitions is null || action.DeclaredCapabilities is null)
        {
            failure = "A content package and all of its dependency, definition, and capability lists are required.";
            return false;
        }

        try
        {
            var dependencies = action.Dependencies
                .Select(dependency =>
                {
                    ArgumentNullException.ThrowIfNull(dependency);
                    return new ContentDependency(
                        dependency.PackageId,
                        new ContentVersionRange(
                            ContentVersion.Parse(dependency.MinimumVersion),
                            ContentVersion.Parse(dependency.MaximumExclusiveVersion)),
                        dependency.Optional);
                })
                .ToArray();
            var definitions = action.Definitions
                .Select(definition =>
                {
                    ArgumentNullException.ThrowIfNull(definition);
                    return new ContentDefinition(
                        definition.Kind,
                        definition.LocalId,
                        ContentVersion.Parse(definition.Version),
                        definition.DisplayName,
                        definition.PayloadDigest,
                        definition.PayloadJson);
                })
                .ToArray();
            var assets = (action.Assets ?? [])
                .Select(asset =>
                {
                    ArgumentNullException.ThrowIfNull(asset);
                    return new WorldAssetReservationRequest(
                        asset.AssetId,
                        asset.NormalizedDigest,
                        asset.DecodeProfile,
                        asset.DurableStorageBytes,
                        asset.DecodedCacheBytes,
                        asset.GpuBytes,
                        asset.RenderUnits);
                })
                .ToArray();
            manifest = new ContentPackageManifest(
                action.PackageId,
                ContentVersion.Parse(action.Version),
                action.PackageDigest,
                dependencies,
                definitions,
                action.DeclaredCapabilities.ToArray(),
                assets);
            manifest.Validate();
            return true;
        }
        catch (ArgumentException exception)
        {
            failure = exception.Message;
            return false;
        }
        catch (FormatException exception)
        {
            failure = exception.Message;
            return false;
        }
    }
}
