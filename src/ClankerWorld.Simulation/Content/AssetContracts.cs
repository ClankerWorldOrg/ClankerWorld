using System.Security.Cryptography;

namespace ClankerWorld.Simulation.Content;

public enum AssetDiagnosticSeverity
{
    Error,
    Warning,
}

public sealed record AssetDiagnostic(
    AssetDiagnosticSeverity Severity,
    string Code,
    string Field,
    string Message,
    string? ObservedValue = null,
    string? Limit = null)
{
    public override string ToString() =>
        string.Join(
            '|',
            Severity.ToString().ToLowerInvariant(),
            Code,
            Field,
            ObservedValue ?? string.Empty,
            Limit ?? string.Empty,
            Message);
}

/// <summary>
/// Naming and digest helpers shared by the asset contract. Asset IDs reuse the
/// existing immutable content-definition form with the registered <c>asset</c>
/// kind.
/// </summary>
public static class AssetRules
{
    public const string SchemaKind = "asset";

    public static string CanonicalAssetId(
        string packageDigest,
        string localId,
        ContentVersion version)
    {
        ValidateVersion(version);
        return ContentPackageRules.CanonicalDefinitionId(packageDigest, SchemaKind, localId, version);
    }

    public static string CanonicalId(
        string packageDigest,
        string localId,
        ContentVersion version) => CanonicalAssetId(packageDigest, localId, version);

    public static void ValidateAssetId(string value) => ValidateCanonicalAssetId(value);

    public static void ValidateCanonicalAssetId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var assetMarker = "/asset/";
        var markerIndex = value.IndexOf(assetMarker, StringComparison.Ordinal);
        var versionMarker = value.LastIndexOf('@');
        if (markerIndex <= 0 || versionMarker <= markerIndex + assetMarker.Length ||
            versionMarker == value.Length - 1 || value.IndexOf('/', versionMarker + 1) >= 0)
        {
            throw new ArgumentException($"Asset ID '{value}' is not canonical.", nameof(value));
        }

        var packageDigest = value[..markerIndex];
        var localId = value[(markerIndex + assetMarker.Length)..versionMarker];
        var versionText = value[(versionMarker + 1)..];
        ContentPackageRules.ValidateDigest(packageDigest, nameof(value));
        ContentPackageRules.ValidateContentLocalId(localId);
        var version = ContentVersion.Parse(versionText);
        ValidateVersion(version);
        if (!string.Equals(value, CanonicalAssetId(packageDigest, localId, version), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Asset ID '{value}' is not canonical.", nameof(value));
        }
    }

    public static string Sha256(ReadOnlySpan<byte> bytes) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(bytes))}";

    private static void ValidateVersion(ContentVersion version)
    {
        if (version.Major < 0 || version.Minor < 0 || version.Patch < 0)
        {
            throw new ArgumentException("Asset versions cannot contain negative components.", nameof(version));
        }
    }
}
