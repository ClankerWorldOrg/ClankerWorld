using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClankerWorld.Simulation.Content;

/// <summary>
/// Canonical, value-only serialization for package manifests. The declared
/// package digest remains the immutable package namespace used by definition
/// IDs; this separate manifest digest fingerprints the exact canonical bytes
/// persisted and reviewed by governance.
/// </summary>
public static class ContentPackageManifestCodec
{
    public const string Schema = "clankerworld.content-package-manifest/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static byte[] Encode(ContentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        return JsonSerializer.SerializeToUtf8Bytes(ToWire(manifest), JsonOptions);
    }

    public static ContentPackageManifest Decode(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length > ContentPackageRules.MaximumManifestBytes)
            throw new InvalidDataException("The content package manifest exceeds its supported size.");
        CanonicalManifest? wire;
        try
        {
            wire = JsonSerializer.Deserialize<CanonicalManifest>(utf8Json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The content package manifest is not valid JSON.", exception);
        }

        if (wire is null)
        {
            throw new InvalidDataException("The content package manifest is empty.");
        }

        var manifest = FromWire(wire);
        var canonical = Encode(manifest);
        if (!utf8Json.SequenceEqual(canonical))
        {
            throw new InvalidDataException("The content package manifest is not in canonical byte form.");
        }

        return manifest;
    }

    public static string ComputeManifestDigest(ContentPackageManifest manifest) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encode(manifest)))}";

    private static CanonicalManifest ToWire(ContentPackageManifest manifest) => new(
        Schema,
        manifest.PackageId,
        manifest.Version.ToString(),
        manifest.PackageDigest,
        manifest.Dependencies
            .OrderBy(item => item.PackageId, StringComparer.Ordinal)
            .ThenBy(item => item.VersionRange.Minimum)
            .ThenBy(item => item.VersionRange.MaximumExclusive)
            .ThenBy(item => item.Optional)
            .Select(item => new CanonicalDependency(
                item.PackageId,
                item.VersionRange.Minimum.ToString(),
                item.VersionRange.MaximumExclusive.ToString(),
                item.Optional))
            .ToArray(),
        manifest.Definitions
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.LocalId, StringComparer.Ordinal)
            .ThenBy(item => item.Version)
            .ThenBy(item => item.PayloadDigest, StringComparer.Ordinal)
            .Select(item => new CanonicalDefinition(
                item.Kind,
                item.LocalId,
                item.Version.ToString(),
                item.DisplayName,
                item.PayloadDigest,
                item.PayloadJson))
            .ToArray(),
        manifest.DeclaredCapabilities.Order(StringComparer.Ordinal).ToArray(),
        (manifest.AssetReservations ?? [])
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ThenBy(item => item.NormalizedDigest, StringComparer.Ordinal)
            .ThenBy(item => item.DecodeProfile, StringComparer.Ordinal)
            .ToArray());

    private static ContentPackageManifest FromWire(CanonicalManifest wire)
    {
        if (!string.Equals(wire.Schema, Schema, StringComparison.Ordinal) ||
            wire.Dependencies is null ||
            wire.Definitions is null ||
            wire.DeclaredCapabilities is null ||
            wire.AssetReservations is null)
        {
            throw new InvalidDataException("The content package manifest schema or collections are invalid.");
        }

        var manifest = new ContentPackageManifest(
            wire.PackageId,
            ContentVersion.Parse(wire.Version),
            wire.PackageDigest,
            wire.Dependencies
                .Select(item => new ContentDependency(
                    item.PackageId,
                    new ContentVersionRange(
                        ContentVersion.Parse(item.MinimumVersion),
                        ContentVersion.Parse(item.MaximumExclusiveVersion)),
                    item.Optional))
                .ToArray(),
            wire.Definitions
                .Select(item => new ContentDefinition(
                    item.Kind,
                    item.LocalId,
                    ContentVersion.Parse(item.Version),
                    item.DisplayName,
                    item.PayloadDigest,
                    item.PayloadJson))
                .ToArray(),
            wire.DeclaredCapabilities.ToArray(),
            wire.AssetReservations
                .Select(item => item with { })
                .ToArray());
        manifest.Validate();
        return manifest;
    }

    private sealed record CanonicalManifest(
        string Schema,
        string PackageId,
        string Version,
        string PackageDigest,
        IReadOnlyList<CanonicalDependency>? Dependencies,
        IReadOnlyList<CanonicalDefinition>? Definitions,
        IReadOnlyList<string>? DeclaredCapabilities,
        IReadOnlyList<WorldAssetReservationRequest>? AssetReservations);

    private sealed record CanonicalDependency(
        string PackageId,
        string MinimumVersion,
        string MaximumExclusiveVersion,
        bool Optional);

    private sealed record CanonicalDefinition(
        string Kind,
        string LocalId,
        string Version,
        string DisplayName,
        string PayloadDigest,
        string? PayloadJson);
}
