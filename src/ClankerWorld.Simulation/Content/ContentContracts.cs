using System.Security.Cryptography;
using System.Text;

namespace ClankerWorld.Simulation.Content;

/// <summary>
/// Strict three-part semantic versioning for data packages. Pre-release
/// versions are intentionally rejected until the compatibility policy defines
/// their ordering.
/// </summary>
public readonly record struct ContentVersion(int Major, int Minor, int Patch) : IComparable<ContentVersion>
{
    public static ContentVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Trim().Split('.', StringSplitOptions.None);
        if (parts.Length != 3 || parts.Any(part => part.Length == 0 ||
                (part.Length > 1 && part[0] == '0') || !part.All(char.IsDigit)) ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch) ||
            major < 0 || minor < 0 || patch < 0)
        {
            throw new FormatException($"Content version '{value}' must be a stable major.minor.patch version.");
        }

        return new ContentVersion(major, minor, patch);
    }

    public int CompareTo(ContentVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator <(ContentVersion left, ContentVersion right) => left.CompareTo(right) < 0;

    public static bool operator <=(ContentVersion left, ContentVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >(ContentVersion left, ContentVersion right) => left.CompareTo(right) > 0;

    public static bool operator >=(ContentVersion left, ContentVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public readonly record struct ContentVersionRange(ContentVersion Minimum, ContentVersion MaximumExclusive)
{
    public bool Contains(ContentVersion version) =>
        version.CompareTo(Minimum) >= 0 && version.CompareTo(MaximumExclusive) < 0;

    public void Validate()
    {
        if (MaximumExclusive.CompareTo(Minimum) <= 0)
        {
            throw new ArgumentException("A content compatibility range must have an exclusive upper bound after its lower bound.");
        }
    }

    public override string ToString() => $">={Minimum} <{MaximumExclusive}";
}

public sealed record ContentDependency(
    string PackageId,
    ContentVersionRange VersionRange,
    bool Optional = false)
{
    public void Validate()
    {
        ContentPackageRules.ValidatePackageId(PackageId);
        VersionRange.Validate();
    }
}

public sealed record ContentDefinition(
    string Kind,
    string LocalId,
    ContentVersion Version,
    string DisplayName,
    string PayloadDigest,
    string? PayloadJson = null)
{
    public string CanonicalId(string packageDigest) =>
        ContentPackageRules.CanonicalDefinitionId(packageDigest, Kind, LocalId, Version);

    public void Validate()
    {
        ContentPackageRules.ValidateSchemaKind(Kind);
        ContentPackageRules.ValidateContentLocalId(LocalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ContentPackageRules.ValidateDigest(PayloadDigest, nameof(PayloadDigest));
        if (PayloadJson is not null && string.IsNullOrWhiteSpace(PayloadJson))
        {
            throw new ArgumentException("A content payload must be non-empty when supplied.", nameof(PayloadJson));
        }
        ContentDefinitionPayloadCodec.ValidateContentIdLengths(this);
    }
}

public sealed record ContentPackageManifest(
    string PackageId,
    ContentVersion Version,
    string PackageDigest,
    IReadOnlyList<ContentDependency> Dependencies,
    IReadOnlyList<ContentDefinition> Definitions,
    IReadOnlyList<string> DeclaredCapabilities,
    IReadOnlyList<WorldAssetReservationRequest>? AssetReservations = null)
{
    public void Validate()
    {
        ContentPackageRules.ValidatePackageId(PackageId);
        ContentPackageRules.ValidateDigest(PackageDigest, nameof(PackageDigest));
        ArgumentNullException.ThrowIfNull(Dependencies);
        ArgumentNullException.ThrowIfNull(Definitions);
        ArgumentNullException.ThrowIfNull(DeclaredCapabilities);

        foreach (var dependency in Dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            dependency.Validate();
        }

        if (Dependencies.Select(item => item.PackageId).Distinct(StringComparer.Ordinal).Count() != Dependencies.Count)
        {
            throw new ArgumentException("A package may declare each dependency only once.", nameof(Dependencies));
        }

        foreach (var definition in Definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
        }

        var definitionIds = Definitions
            .Select(definition => definition.CanonicalId(PackageDigest))
            .ToArray();
        if (definitionIds.Distinct(StringComparer.Ordinal).Count() != definitionIds.Length)
        {
            throw new ArgumentException("A package may not define the same immutable content ID twice.", nameof(Definitions));
        }

        foreach (var capability in DeclaredCapabilities)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(capability);
            if (capability.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("Content capabilities cannot contain whitespace.", nameof(DeclaredCapabilities));
            }
        }

        foreach (var asset in AssetReservations ?? [])
        {
            ArgumentNullException.ThrowIfNull(asset);
            asset.Validate(PackageDigest);
        }

        var assetIds = (AssetReservations ?? [])
            .Select(asset => asset.AssetId)
            .ToArray();
        if (assetIds.Distinct(StringComparer.Ordinal).Count() != assetIds.Length)
        {
            throw new ArgumentException("A package may reserve each asset ID only once.", nameof(AssetReservations));
        }
    }
}

public sealed record ContentLockEntry(
    string PackageId,
    ContentVersion Version,
    string PackageDigest,
    IReadOnlyList<string> CompatibilityDecisions);

public sealed record ContentResolutionResult(
    bool IsSuccess,
    IReadOnlyList<ContentLockEntry> Lock,
    string? FailureCode,
    string? Diagnostic)
{
    public static ContentResolutionResult Success(IReadOnlyList<ContentLockEntry> contentLock) =>
        new(true, contentLock, null, null);

    public static ContentResolutionResult Failure(string code, string diagnostic) =>
        new(false, [], code, diagnostic);
}

public enum ContentPackageLifecycle
{
    Proposed,
    Validated,
    Approved,
    Staged,
    Active,
    Quarantined,
}

public sealed record ContentPackageRecord(
    ContentPackageManifest Manifest,
    ContentPackageLifecycle Lifecycle,
    string? LockDigest,
    long? ValidationTick,
    long? ActivationTick,
    long? StagedTick = null,
    string? ManifestDigest = null);

public sealed record ContentGovernanceEvent(
    long EventId,
    long WorldTick,
    string PackageId,
    string Kind,
    string Detail);

public sealed record ContentRegistryState(
    IReadOnlyList<ContentPackageRecord> Packages,
    IReadOnlyList<ContentGovernanceEvent> Events);

public static class ContentPackageRules
{
    public const int MaximumContentIdLength = 512;

    public static string CanonicalDefinitionId(
        string packageDigest,
        string kind,
        string localId,
        ContentVersion version)
    {
        ValidateDigest(packageDigest, nameof(packageDigest));
        ValidateSchemaKind(kind);
        ValidateContentLocalId(localId);
        var id = $"{packageDigest}/{kind}/{localId}@{version}";
        ValidateContentIdLength(id);
        return id;
    }

    public static void ValidatePackageId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ValidateContentIdLength(value);
        if (value != value.Trim() || value.Any(character =>
                !(char.IsLower(character) || char.IsDigit(character) || character is '.' or '-' or '_')) ||
            !char.IsLower(value[0]) || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"Package ID '{value}' is not a canonical lowercase package ID.", nameof(value));
        }
    }

    public static void ValidateSchemaKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ValidateContentIdLength(value);
        if (value != value.Trim() || value.Any(character =>
                !(char.IsLower(character) || char.IsDigit(character) || character == '_')))
        {
            throw new ArgumentException($"Schema kind '{value}' is not canonical.", nameof(value));
        }
    }

    public static void ValidateLocalId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value != value.Trim() || value.Any(character =>
                !(char.IsLower(character) || char.IsDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException($"Local content ID '{value}' is not canonical.", nameof(value));
        }
    }

    public static void ValidateContentLocalId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ValidateContentIdLength(value);
        ValidateLocalId(value);
    }

    public static bool IsValidContentLocalId(string? value) => value is { Length: > 0 and <= MaximumContentIdLength } &&
        value.All(character => char.IsLower(character) || char.IsDigit(character) || character is '.' or '-' or '_');

    internal static void ValidateContentIdLength(string value)
    {
        if (value.Length > MaximumContentIdLength)
            throw new ArgumentException($"Content IDs must be at most {MaximumContentIdLength} characters; this ID has {value.Length}.", nameof(value));
    }

    public static void ValidateDigest(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal) ||
            value[7..].Any(character => !Uri.IsHexDigit(character)) ||
            !string.Equals(value[7..], value[7..].ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Digest '{value}' must be lowercase sha256:<64-hex>.", parameterName);
        }
    }

    public static string LockDigest(IEnumerable<ContentLockEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var canonical = string.Join(
            '\n',
            entries.OrderBy(entry => entry.PackageId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Version)
                .ThenBy(entry => entry.PackageDigest, StringComparer.Ordinal)
                .Select(entry =>
                    $"{entry.PackageId}@{entry.Version}|{entry.PackageDigest}|" +
                    string.Join(',', entry.CompatibilityDecisions.Order(StringComparer.Ordinal))));
        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}";
    }
}

/// <summary>
/// Resolves immutable package manifests before any activation. It chooses the
/// highest matching stable version, uses digest order as the tie-breaker, and
/// returns a dependency-first lock with deterministic diagnostics.
/// </summary>
public static class ContentPackageResolver
{
    public static ContentResolutionResult Resolve(
        IEnumerable<ContentPackageManifest> availablePackages,
        IEnumerable<string> rootPackageIds)
    {
        ArgumentNullException.ThrowIfNull(availablePackages);
        ArgumentNullException.ThrowIfNull(rootPackageIds);
        var packages = availablePackages.ToArray();
        try
        {
            foreach (var package in packages)
            {
                ArgumentNullException.ThrowIfNull(package);
                package.Validate();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return ContentResolutionResult.Failure("invalid_manifest", exception.Message);
        }

        var byId = packages
            .GroupBy(package => package.PackageId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var group in byId.Values)
        {
            if (group.GroupBy(package => package.Version).Any(version =>
                    version.Select(package => package.PackageDigest).Distinct(StringComparer.Ordinal).Count() > 1))
            {
                return ContentResolutionResult.Failure(
                    "package_digest_mismatch",
                    $"Package '{group[0].PackageId}' has multiple digests for one version.");
            }
        }

        var selected = new Dictionary<string, ContentPackageManifest>(StringComparer.Ordinal);
        var visiting = new List<string>();
        var processed = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<ContentPackageManifest>();
        foreach (var root in rootPackageIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var result = ResolvePackage(
                root,
                null,
                byId,
                selected,
                visiting,
                processed,
                ordered);
            if (result is not null)
            {
                return result;
            }
        }

        var contentLock = ordered
            .Select(package => new ContentLockEntry(
                package.PackageId,
                package.Version,
                package.PackageDigest,
                ["kernel-api>=1.0.0<2.0.0", "save-schema>=1.0.0<2.0.0"]))
            .ToArray();
        return ContentResolutionResult.Success(contentLock);
    }

    private static ContentResolutionResult? ResolvePackage(
        string packageId,
        ContentVersionRange? requiredRange,
        IReadOnlyDictionary<string, ContentPackageManifest[]> byId,
        IDictionary<string, ContentPackageManifest> selected,
        ICollection<string> visiting,
        ISet<string> processed,
        ICollection<ContentPackageManifest> ordered)
    {
        if (!byId.TryGetValue(packageId, out var candidates))
        {
            return ContentResolutionResult.Failure(
                "missing_dependency",
                $"Required package '{packageId}' is not available.");
        }

        if (visiting.Contains(packageId, StringComparer.Ordinal))
        {
            var path = string.Join(" -> ", visiting.Append(packageId));
            return ContentResolutionResult.Failure("dependency_cycle", $"Dependency cycle detected: {path}.");
        }

        if (selected.TryGetValue(packageId, out var alreadySelected))
        {
            if (requiredRange is not null && !requiredRange.Value.Contains(alreadySelected.Version))
            {
                return ContentResolutionResult.Failure(
                    "dependency_conflict",
                    $"Package '{packageId}' selected at {alreadySelected.Version} does not satisfy {requiredRange.Value}.");
            }

            return null;
        }

        var candidate = candidates
            .Where(package => requiredRange is null || requiredRange.Value.Contains(package.Version))
            .OrderByDescending(package => package.Version)
            .ThenBy(package => package.PackageDigest, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is null)
        {
            return ContentResolutionResult.Failure(
                "compatibility_failure",
                $"No version of package '{packageId}' satisfies {requiredRange}.");
        }

        selected.Add(packageId, candidate);
        visiting.Add(packageId);
        foreach (var dependency in candidate.Dependencies.OrderBy(item => item.PackageId, StringComparer.Ordinal))
        {
            if (!byId.ContainsKey(dependency.PackageId) && dependency.Optional)
            {
                continue;
            }

            var failure = ResolvePackage(
                dependency.PackageId,
                dependency.VersionRange,
                byId,
                selected,
                visiting,
                processed,
                ordered);
            if (failure is not null)
            {
                return failure;
            }
        }

        visiting.Remove(packageId);
        processed.Add(packageId);
        if (!ordered.Contains(candidate))
        {
            ordered.Add(candidate);
        }

        return null;
    }
}

/// <summary>
/// Data-only package lifecycle. Executable, network, filesystem, and host
/// capabilities are rejected here; a later sandbox contract can add a
/// separate stronger activation path without changing this registry.
/// </summary>
public sealed class ContentPackageRegistry
{
    private readonly Dictionary<string, ContentPackageRecord> packages = new(StringComparer.Ordinal);
    private readonly List<ContentGovernanceEvent> events = [];
    private long nextEventId = 1;

    public ContentRegistryState ExportState() => new(
        packages.Values.OrderBy(item => item.Manifest.PackageId, StringComparer.Ordinal).ToArray(),
        events.ToArray());

    public static ContentPackageRegistry Restore(ContentRegistryState? state)
    {
        var registry = new ContentPackageRegistry();
        if (state is null)
        {
            return registry;
        }

        ArgumentNullException.ThrowIfNull(state.Packages);
        ArgumentNullException.ThrowIfNull(state.Events);
        foreach (var package in state.Packages)
        {
            ArgumentNullException.ThrowIfNull(package);
            package.Manifest.Validate();
            RejectForbiddenCapabilities(package.Manifest);
            var expectedManifestDigest = ContentPackageManifestCodec.ComputeManifestDigest(package.Manifest);
            if (package.ManifestDigest is not null &&
                !string.Equals(package.ManifestDigest, expectedManifestDigest, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Content package '{package.Manifest.PackageId}' has a mismatched manifest digest.");
            }

            var normalizedPackage = package with { ManifestDigest = expectedManifestDigest };
            if (!string.Equals(package.Manifest.PackageId, package.Manifest.PackageId.Trim(), StringComparison.Ordinal) ||
                !registry.packages.TryAdd(package.Manifest.PackageId, normalizedPackage))
            {
                throw new InvalidDataException("The content registry contains duplicate or non-canonical package records.");
            }

            ValidateRecord(normalizedPackage);
        }

        var expectedEventId = 1L;
        var previousTick = 0L;
        foreach (var governanceEvent in state.Events)
        {
            ArgumentNullException.ThrowIfNull(governanceEvent);
            if (governanceEvent.EventId != expectedEventId ||
                governanceEvent.WorldTick < previousTick ||
                !registry.packages.ContainsKey(governanceEvent.PackageId))
            {
                throw new InvalidDataException("The content governance event stream is not ordered or references an unknown package.");
            }

            expectedEventId = checked(expectedEventId + 1);
            previousTick = governanceEvent.WorldTick;
        }

        registry.events.AddRange(state.Events);
        registry.nextEventId = expectedEventId;
        return registry;
    }

    public void Validate()
    {
        _ = Restore(ExportState());
    }

    public ContentPackageRecord Propose(ContentPackageManifest manifest, long worldTick = 0) =>
        Propose(manifest, worldTick, proposedByAgentId: null);

    public ContentPackageRecord Propose(
        ContentPackageManifest manifest,
        long worldTick,
        string? proposedByAgentId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (proposedByAgentId is not null &&
            (string.IsNullOrWhiteSpace(proposedByAgentId) ||
             proposedByAgentId != proposedByAgentId.Trim() ||
             proposedByAgentId.Length > 256 ||
             proposedByAgentId.Any(char.IsControl)))
        {
            throw new ArgumentException("An agent proposal requires a canonical author ID.", nameof(proposedByAgentId));
        }
        manifest.Validate();
        RejectForbiddenCapabilities(manifest);
        if (packages.ContainsKey(manifest.PackageId))
        {
            throw new InvalidOperationException($"Package '{manifest.PackageId}' already has a lifecycle record.");
        }

        var record = new ContentPackageRecord(
            manifest,
            ContentPackageLifecycle.Proposed,
            null,
            null,
            null,
            null,
            ContentPackageManifestCodec.ComputeManifestDigest(manifest));
        packages.Add(manifest.PackageId, record);
        AppendEvent(worldTick, manifest.PackageId, "package_proposed", manifest.PackageDigest);
        if (proposedByAgentId is not null)
        {
            AppendEvent(worldTick, manifest.PackageId, "package_proposed_by_agent", proposedByAgentId);
        }
        return record;
    }

    public ContentPackageRecord Validate(
        string packageId,
        ContentResolutionResult resolution,
        long worldTick)
    {
        var record = Get(packageId);
        if (record.Lifecycle != ContentPackageLifecycle.Proposed)
        {
            throw new InvalidOperationException("Only proposed packages can be validated.");
        }

        if (!resolution.IsSuccess)
        {
            throw new InvalidOperationException($"Package resolution failed: {resolution.FailureCode}: {resolution.Diagnostic}");
        }

        var lockDigest = ContentPackageRules.LockDigest(resolution.Lock);
        record = record with { Lifecycle = ContentPackageLifecycle.Validated, LockDigest = lockDigest, ValidationTick = worldTick };
        packages[packageId] = record;
        AppendEvent(worldTick, packageId, "package_validated", lockDigest);
        return record;
    }

    public ContentPackageRecord Approve(string packageId, long worldTick)
    {
        var record = Get(packageId);
        RequireState(record, ContentPackageLifecycle.Validated);
        record = record with { Lifecycle = ContentPackageLifecycle.Approved };
        packages[packageId] = record;
        AppendEvent(worldTick, packageId, "package_approved", "owner_approval");
        return record;
    }

    public ContentPackageRecord Stage(string packageId, long worldTick)
    {
        var record = Get(packageId);
        RequireState(record, ContentPackageLifecycle.Approved);
        var dependencyLock = RequireValidLock(record);
        if (dependencyLock.Any(entry => entry.PackageId != packageId && Get(entry.PackageId).Lifecycle is not
                (ContentPackageLifecycle.Approved or ContentPackageLifecycle.Staged or ContentPackageLifecycle.Active)))
        {
            throw new InvalidOperationException("Locked dependencies must be approved before staging.");
        }
        record = record with { Lifecycle = ContentPackageLifecycle.Staged, StagedTick = worldTick };
        packages[packageId] = record;
        AppendEvent(worldTick, packageId, "package_staged", record.LockDigest ?? string.Empty);
        return record;
    }

    public ContentPackageRecord Activate(string packageId, long worldTick)
    {
        var record = Get(packageId);
        RequireState(record, ContentPackageLifecycle.Staged);
        if (RequireValidLock(record).Any(entry => entry.PackageId != packageId && Get(entry.PackageId).Lifecycle != ContentPackageLifecycle.Active))
        {
            throw new InvalidOperationException("Locked dependencies must be active before activation.");
        }
        if (record.StagedTick is { } stagedTick && worldTick <= stagedTick)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' can activate only after its staging tick {stagedTick}.");
        }

        record = record with { Lifecycle = ContentPackageLifecycle.Active, ActivationTick = worldTick };
        packages[packageId] = record;
        AppendEvent(worldTick, packageId, "package_activated", record.LockDigest ?? string.Empty);
        return record;
    }

    internal ContentPackageRecord ActivateAtCreation(string packageId)
    {
        var record = Get(packageId);
        RequireState(record, ContentPackageLifecycle.Staged);
        if (record.StagedTick != 0 ||
            RequireValidLock(record).Any(entry => entry.PackageId != packageId &&
                Get(entry.PackageId).Lifecycle != ContentPackageLifecycle.Active))
            throw new InvalidOperationException("Only genesis-staged content with active dependencies can join a new world.");
        record = record with { Lifecycle = ContentPackageLifecycle.Active, ActivationTick = 0 };
        packages[packageId] = record;
        AppendEvent(0, packageId, "package_activated_at_creation", record.LockDigest ?? string.Empty);
        return record;
    }

    public IReadOnlyList<ContentPackageRecord> ActivateReady(long worldTick)
        => GetActivationCandidates(worldTick).Select(item => Activate(item.Manifest.PackageId, worldTick)).ToArray();

    public IReadOnlyList<ContentPackageRecord> GetActivationCandidates(long worldTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        var pending = packages.Values
            .Where(item => item.Lifecycle == ContentPackageLifecycle.Staged &&
                item.StagedTick is { } stagedTick && worldTick > stagedTick)
            .OrderBy(item => item.Manifest.PackageId, StringComparer.Ordinal)
            .ToList();
        var active = packages.Values.Where(item => item.Lifecycle == ContentPackageLifecycle.Active)
            .Select(item => item.Manifest.PackageId).ToHashSet(StringComparer.Ordinal);
        var ready = new List<ContentPackageRecord>();
        while (pending.Count > 0)
        {
            var progressed = false;
            foreach (var record in pending.ToArray())
            {
                IReadOnlyList<ContentLockEntry> dependencyLock;
                try
                {
                    dependencyLock = RequireValidLock(record);
                }
                catch (InvalidOperationException)
                {
                    // A blocked staged package stays inspectable and can be rolled back.
                    // It must not install definitions or stop unrelated world ticks.
                    continue;
                }
                if (dependencyLock.Any(entry => entry.PackageId != record.Manifest.PackageId && !active.Contains(entry.PackageId)))
                {
                    continue;
                }
                ready.Add(record);
                active.Add(record.Manifest.PackageId);
                pending.Remove(record);
                progressed = true;
            }
            if (!progressed)
            {
                break;
            }
        }
        return ready;
    }

    private IReadOnlyList<ContentLockEntry> RequireValidLock(ContentPackageRecord record)
    {
        var resolution = ContentPackageResolver.Resolve(packages.Values
            .Where(item => item.Lifecycle != ContentPackageLifecycle.Quarantined)
            .Select(item => item.Manifest), [record.Manifest.PackageId]);
        if (!resolution.IsSuccess || ContentPackageRules.LockDigest(resolution.Lock) != record.LockDigest)
        {
            throw new InvalidOperationException("The validated dependency lock is no longer available.");
        }
        return resolution.Lock;
    }

    public ContentPackageRecord Rollback(string packageId, long worldTick, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var record = Get(packageId);
        if (packages.Values.Any(package => package.Lifecycle == ContentPackageLifecycle.Active &&
                package.Manifest.PackageId != packageId &&
                package.Manifest.Dependencies.Any(dependency => dependency.PackageId == packageId)))
        {
            throw new InvalidOperationException("Roll back active dependents before removing their dependency.");
        }
        if (record.Lifecycle is not (ContentPackageLifecycle.Active or ContentPackageLifecycle.Staged))
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' is {record.Lifecycle}, and only active or staged packages can be rolled back.");
        }

        record = record with { Lifecycle = ContentPackageLifecycle.Quarantined };
        packages[packageId] = record;
        AppendEvent(worldTick, packageId, "package_rolled_back", reason.Trim());
        return record;
    }

    private ContentPackageRecord Get(string packageId)
    {
        ContentPackageRules.ValidatePackageId(packageId);
        return packages.TryGetValue(packageId, out var record)
            ? record
            : throw new KeyNotFoundException($"Package '{packageId}' has no lifecycle record.");
    }

    private void AppendEvent(long worldTick, string packageId, string kind, string detail) =>
        events.Add(new ContentGovernanceEvent(nextEventId++, worldTick, packageId, kind, detail));

    private static void RequireState(ContentPackageRecord record, ContentPackageLifecycle expected)
    {
        if (record.Lifecycle != expected)
        {
            throw new InvalidOperationException(
                $"Package '{record.Manifest.PackageId}' is {record.Lifecycle}, expected {expected}.");
        }
    }

    private static void ValidateRecord(ContentPackageRecord record)
    {
        var expectedManifestDigest = ContentPackageManifestCodec.ComputeManifestDigest(record.Manifest);
        if (!string.Equals(record.ManifestDigest, expectedManifestDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Content package '{record.Manifest.PackageId}' has no valid canonical manifest digest.");
        }

        if (record.Lifecycle == ContentPackageLifecycle.Validated &&
            (record.LockDigest is null || record.ValidationTick is null))
        {
            throw new InvalidDataException("A validated content package must retain its lock digest and validation tick.");
        }

        if ((record.Lifecycle is ContentPackageLifecycle.Staged or ContentPackageLifecycle.Active) &&
            record.StagedTick is null)
        {
            throw new InvalidDataException("A staged or active content package must retain its staging tick.");
        }

        if (record.Lifecycle == ContentPackageLifecycle.Active && record.ActivationTick is null)
        {
            throw new InvalidDataException("An active content package must retain its activation tick.");
        }
    }

    private static void RejectForbiddenCapabilities(ContentPackageManifest manifest)
    {
        var forbidden = manifest.DeclaredCapabilities
            .Where(capability => capability is "execute" or "network" or "filesystem" or "host_integration")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (forbidden.Length > 0)
        {
            throw new InvalidOperationException(
                $"Package '{manifest.PackageId}' requests disabled capabilities: {string.Join(", ", forbidden)}.");
        }
    }
}
