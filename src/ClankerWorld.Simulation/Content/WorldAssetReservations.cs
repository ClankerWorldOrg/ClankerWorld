using System.Globalization;

namespace ClankerWorld.Simulation.Content;

/// <summary>
/// The value-only asset metadata a package asks the world to reserve. The
/// request contains no bytes or host locator; it is safe to persist and to
/// compare during a deterministic activation preflight.
/// </summary>
public sealed record WorldAssetReservationRequest(
    string AssetId,
    string NormalizedDigest,
    string DecodeProfile,
    long DurableStorageBytes,
    long DecodedCacheBytes,
    long GpuBytes,
    int RenderUnits)
{
    public void Validate(string packageDigest)
    {
        ContentPackageRules.ValidateDigest(packageDigest, nameof(packageDigest));
        AssetRules.ValidateCanonicalAssetId(AssetId);
        ContentPackageRules.ValidateDigest(NormalizedDigest, nameof(NormalizedDigest));
        ArgumentException.ThrowIfNullOrWhiteSpace(DecodeProfile);
        if (DecodeProfile != DecodeProfile.Trim() || DecodeProfile.Any(char.IsWhiteSpace) || DecodeProfile.Length > 64)
        {
            throw new ArgumentException("Asset decode profiles must be trimmed, bounded, and contain no whitespace.");
        }

        if (!AssetId.StartsWith($"{packageDigest}/asset/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("An asset reservation must belong to its declaring package digest.");
        }

        if (DurableStorageBytes <= 0 || DecodedCacheBytes <= 0 || GpuBytes <= 0 || RenderUnits is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(packageDigest),
                "Asset reservations must contain positive byte charges and between one and four render units.");
        }
    }
}

public sealed record WorldAssetReservation(
    string PackageId,
    string AssetId,
    string NormalizedDigest,
    string DecodeProfile,
    long DurableStorageBytes,
    long DecodedCacheBytes,
    long GpuBytes,
    int RenderUnits);

public sealed record WorldAssetReservationTotals(
    int DistinctAssetCount,
    long DurableStorageBytes,
    long DecodedCacheBytes,
    long GpuBytes,
    int RenderUnits);

public sealed record WorldAssetReservationEvent(
    long EventId,
    long WorldTick,
    string PackageId,
    string Kind,
    string Detail);

public sealed record WorldAssetReservationLedgerState(
    IReadOnlyList<WorldAssetReservation> Reservations,
    IReadOnlyList<WorldAssetReservationEvent> Events);

public sealed record WorldAssetReservationResult(
    bool IsSuccess,
    WorldAssetReservationTotals Totals,
    IReadOnlyList<AssetDiagnostic> Diagnostics)
{
    public string Diagnostic => string.Join('\n', Diagnostics.Select(item => item.ToString()));

    public string? FailureCode => Diagnostics
        .Where(item => item.Severity == AssetDiagnosticSeverity.Error)
        .Select(item => item.Code)
        .FirstOrDefault();

    public static WorldAssetReservationResult Success(WorldAssetReservationTotals totals) =>
        new(true, totals, []);

    public static WorldAssetReservationResult Failure(params AssetDiagnostic[] diagnostics) =>
        new(false, new WorldAssetReservationTotals(0, 0, 0, 0, 0), diagnostics);
}

/// <summary>
/// World-wide reservation ceilings. Durable storage is charged once per
/// normalized digest; cache and GPU are charged once per digest/decode profile;
/// render work is charged for every package reference.
/// </summary>
public sealed record WorldAssetReservationPolicy
{
    public const string Version = "world-asset-reservation-v1";
    public const long MiB = 1_048_576;

    public long MaxDurableStorageBytes { get; init; } = 512 * MiB;

    public long MaxDecodedCacheBytes { get; init; } = 256 * MiB;

    public long MaxGpuBytes { get; init; } = 256 * MiB;

    public int MaxRenderUnits { get; init; } = 8_192;

    public void Validate()
    {
        if (MaxDurableStorageBytes <= 0 || MaxDecodedCacheBytes <= 0 || MaxGpuBytes <= 0 || MaxRenderUnits <= 0)
        {
            throw new ArgumentException("World asset reservation limits must be positive.", nameof(WorldAssetReservationPolicy));
        }
    }

    public static WorldAssetReservationPolicy Default { get; } = new();
}

/// <summary>
/// Atomic world-wide reservation ledger for active content. It is deliberately
/// independent from rebuildable renderer cache entries, while retaining the
/// same deterministic charges needed to decide whether activation is safe.
/// </summary>
public sealed class WorldAssetReservationLedger
{
    private readonly WorldAssetReservationPolicy policy;
    private readonly Dictionary<string, WorldAssetReservation> reservations = new(StringComparer.Ordinal);
    private readonly List<WorldAssetReservationEvent> events = [];
    private long nextEventId = 1;

    public WorldAssetReservationLedger(WorldAssetReservationPolicy? policy = null)
    {
        this.policy = policy ?? WorldAssetReservationPolicy.Default;
        this.policy.Validate();
    }

    public WorldAssetReservationPolicy Policy => policy;

    public WorldAssetReservationLedgerState ExportState() => new(
        reservations.Values
            .OrderBy(item => item.PackageId, StringComparer.Ordinal)
            .ThenBy(item => item.AssetId, StringComparer.Ordinal)
            .ThenBy(item => item.DecodeProfile, StringComparer.Ordinal)
            .ToArray(),
        events.ToArray());

    public static WorldAssetReservationLedger Restore(
        WorldAssetReservationLedgerState? state,
        WorldAssetReservationPolicy? policy = null)
    {
        var ledger = new WorldAssetReservationLedger(policy);
        if (state is null)
        {
            return ledger;
        }

        ArgumentNullException.ThrowIfNull(state.Reservations);
        ArgumentNullException.ThrowIfNull(state.Events);
        WorldAssetReservation? previousReservation = null;
        foreach (var reservation in state.Reservations)
        {
            ArgumentNullException.ThrowIfNull(reservation);
            ValidateReservation(reservation);
            var reservationKey = ReservationKey(reservation);
            if (previousReservation is not null && CompareReservations(previousReservation, reservation) >= 0)
            {
                throw new InvalidDataException("The world asset reservation entries are not in canonical order.");
            }

            previousReservation = reservation;
            if (!ledger.reservations.TryAdd(reservationKey, reservation))
            {
                throw new InvalidDataException("The world asset reservation ledger contains duplicate entries.");
            }
        }

        var expectedEventId = 1L;
        var previousTick = 0L;
        foreach (var reservationEvent in state.Events)
        {
            ArgumentNullException.ThrowIfNull(reservationEvent);
            if (reservationEvent.EventId != expectedEventId ||
                reservationEvent.WorldTick < previousTick ||
                string.IsNullOrWhiteSpace(reservationEvent.PackageId) ||
                string.IsNullOrWhiteSpace(reservationEvent.Kind))
            {
                throw new InvalidDataException("The world asset reservation event stream is not canonical.");
            }

            expectedEventId = checked(expectedEventId + 1);
            previousTick = reservationEvent.WorldTick;
        }

        ledger.events.AddRange(state.Events);
        ledger.nextEventId = expectedEventId;
        ledger.Validate();
        return ledger;
    }

    public WorldAssetReservationResult TryReservePackage(
        string packageId,
        IEnumerable<WorldAssetReservationRequest> requestedAssets,
        long worldTick)
    {
        ContentPackageRules.ValidatePackageId(packageId);
        ArgumentNullException.ThrowIfNull(requestedAssets);
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);

        var requests = requestedAssets.ToArray();
        try
        {
            foreach (var request in requests)
            {
                ArgumentNullException.ThrowIfNull(request);
                request.ValidateForLedger();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return WorldAssetReservationResult.Failure(Diagnostic(
                "asset_reservation_invalid",
                "assets",
                exception.Message));
        }

        if (reservations.Values.Any(item => item.PackageId == packageId))
        {
            return WorldAssetReservationResult.Failure(Diagnostic(
                "asset_reservation_conflict",
                "package_id",
                $"Package '{packageId}' already has active world reservations."));
        }

        var duplicateIds = requests
            .GroupBy(item => item.AssetId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            return WorldAssetReservationResult.Failure(Diagnostic(
                "asset_reservation_duplicate",
                "asset_id",
                string.Join(',', duplicateIds)));
        }

        var candidate = reservations.Values
            .Concat(requests.Select(request => new WorldAssetReservation(
                packageId,
                request.AssetId,
                request.NormalizedDigest,
                request.DecodeProfile,
                request.DurableStorageBytes,
                request.DecodedCacheBytes,
                request.GpuBytes,
                request.RenderUnits)))
            .ToArray();
        WorldAssetReservationTotals totals;
        try
        {
            totals = SumTotals(candidate);
        }
        catch (OverflowException)
        {
            return WorldAssetReservationResult.Failure(Diagnostic("asset_reservation_overflow", "assets", "Aggregate asset charges exceed supported integer limits."));
        }
        catch (InvalidDataException)
        {
            return WorldAssetReservationResult.Failure(Diagnostic("asset_reservation_conflicting_charge", "assets", "Shared asset identities must declare identical charges."));
        }
        var diagnostics = BudgetDiagnostics(totals);
        if (diagnostics.Count > 0)
        {
            return new WorldAssetReservationResult(false, totals, diagnostics);
        }

        foreach (var request in requests)
        {
            var reservation = new WorldAssetReservation(
                packageId,
                request.AssetId,
                request.NormalizedDigest,
                request.DecodeProfile,
                request.DurableStorageBytes,
                request.DecodedCacheBytes,
                request.GpuBytes,
                request.RenderUnits);
            reservations.Add(ReservationKey(reservation), reservation);
        }

        AppendEvent(worldTick, packageId, "assets_reserved", FormatTotals(totals));
        return WorldAssetReservationResult.Success(totals);
    }

    public bool ReleasePackage(string packageId, long worldTick)
    {
        ContentPackageRules.ValidatePackageId(packageId);
        ArgumentOutOfRangeException.ThrowIfNegative(worldTick);
        var keys = reservations
            .Where(item => item.Value.PackageId == packageId)
            .Select(item => item.Key)
            .ToArray();
        foreach (var key in keys)
        {
            reservations.Remove(key);
        }

        if (keys.Length == 0)
        {
            return false;
        }

        AppendEvent(worldTick, packageId, "assets_released", keys.Length.ToString(CultureInfo.InvariantCulture));
        return true;
    }

    public IReadOnlyList<WorldAssetReservation> GetPackageReservations(string packageId)
    {
        ContentPackageRules.ValidatePackageId(packageId);
        return reservations.Values
            .Where(item => item.PackageId == packageId)
            .OrderBy(item => item.AssetId, StringComparer.Ordinal)
            .ThenBy(item => item.DecodeProfile, StringComparer.Ordinal)
            .ToArray();
    }

    public WorldAssetReservationTotals Totals => SumTotals(reservations.Values);

    public void Validate()
    {
        policy.Validate();
        foreach (var reservation in reservations.Values)
        {
            ValidateReservation(reservation);
        }

        var totals = Totals;
        if (BudgetDiagnostics(totals).Count > 0)
        {
            throw new InvalidDataException("The world asset reservation ledger exceeds its configured policy.");
        }
    }

    private static void ValidateReservation(WorldAssetReservation reservation)
    {
        ContentPackageRules.ValidatePackageId(reservation.PackageId);
        AssetRules.ValidateCanonicalAssetId(reservation.AssetId);
        ContentPackageRules.ValidateDigest(reservation.NormalizedDigest, nameof(reservation.NormalizedDigest));
        ArgumentException.ThrowIfNullOrWhiteSpace(reservation.DecodeProfile);
        if (reservation.DecodeProfile != reservation.DecodeProfile.Trim() ||
            reservation.DecodeProfile.Any(char.IsWhiteSpace) ||
            reservation.DecodeProfile.Length > 64)
        {
            throw new InvalidDataException("A world asset reservation contains a non-canonical decode profile.");
        }
        if (reservation.DurableStorageBytes <= 0 || reservation.DecodedCacheBytes <= 0 ||
            reservation.GpuBytes <= 0 || reservation.RenderUnits is < 1 or > 4)
        {
            throw new InvalidDataException("A world asset reservation contains an invalid charge.");
        }
    }

    private static WorldAssetReservationTotals SumTotals(IEnumerable<WorldAssetReservation> values)
    {
        var ordered = values.ToArray();
        if (ordered.GroupBy(item => item.NormalizedDigest, StringComparer.Ordinal)
                .Any(group => group.Select(item => item.DurableStorageBytes).Distinct().Skip(1).Any()) ||
            ordered.GroupBy(item => (item.NormalizedDigest, item.DecodeProfile))
                .Any(group => group.Select(item => (item.DecodedCacheBytes, item.GpuBytes)).Distinct().Skip(1).Any()))
        {
            throw new InvalidDataException("Shared asset charges conflict.");
        }
        var durable = ordered
            .GroupBy(item => item.NormalizedDigest, StringComparer.Ordinal)
            .Sum(group => group.First().DurableStorageBytes);
        var cache = ordered
            .GroupBy(item => $"{item.NormalizedDigest}|{item.DecodeProfile}", StringComparer.Ordinal)
            .Sum(group => group.First().DecodedCacheBytes);
        var gpu = ordered
            .GroupBy(item => $"{item.NormalizedDigest}|{item.DecodeProfile}", StringComparer.Ordinal)
            .Sum(group => group.First().GpuBytes);
        return new WorldAssetReservationTotals(
            ordered.Select(item => item.NormalizedDigest).Distinct(StringComparer.Ordinal).Count(),
            durable,
            cache,
            gpu,
            checked(ordered.Sum(item => item.RenderUnits)));
    }

    private List<AssetDiagnostic> BudgetDiagnostics(WorldAssetReservationTotals totals)
    {
        var diagnostics = new List<AssetDiagnostic>();
        AddBudget(diagnostics, "world_storage_breach", "durable_storage", totals.DurableStorageBytes, policy.MaxDurableStorageBytes);
        AddBudget(diagnostics, "world_cache_breach", "decoded_cache", totals.DecodedCacheBytes, policy.MaxDecodedCacheBytes);
        AddBudget(diagnostics, "world_gpu_breach", "gpu", totals.GpuBytes, policy.MaxGpuBytes);
        AddBudget(diagnostics, "world_render_breach", "render_units", totals.RenderUnits, policy.MaxRenderUnits);
        return diagnostics;
    }

    private static void AddBudget(
        List<AssetDiagnostic> diagnostics,
        string code,
        string field,
        long observed,
        long limit)
    {
        if (observed > limit)
        {
            diagnostics.Add(Diagnostic(
                code,
                field,
                observed.ToString(CultureInfo.InvariantCulture),
                limit.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static AssetDiagnostic Diagnostic(
        string code,
        string field,
        string message,
        string? limit = null) => new(
        AssetDiagnosticSeverity.Error,
        code,
        field,
        message,
        message,
        limit);

    private static string ReservationKey(WorldAssetReservation reservation) =>
        $"{reservation.PackageId}|{reservation.AssetId}|{reservation.DecodeProfile}";

    private static int CompareReservations(WorldAssetReservation left, WorldAssetReservation right)
    {
        var result = string.CompareOrdinal(left.PackageId, right.PackageId);
        if (result == 0)
        {
            result = string.CompareOrdinal(left.AssetId, right.AssetId);
        }
        return result != 0 ? result : string.CompareOrdinal(left.DecodeProfile, right.DecodeProfile);
    }

    private static string FormatTotals(WorldAssetReservationTotals totals) =>
        string.Join(',',
            $"assets={totals.DistinctAssetCount.ToString(CultureInfo.InvariantCulture)}",
            $"durable={totals.DurableStorageBytes.ToString(CultureInfo.InvariantCulture)}",
            $"cache={totals.DecodedCacheBytes.ToString(CultureInfo.InvariantCulture)}",
            $"gpu={totals.GpuBytes.ToString(CultureInfo.InvariantCulture)}",
            $"render={totals.RenderUnits.ToString(CultureInfo.InvariantCulture)}");

    private void AppendEvent(long worldTick, string packageId, string kind, string detail) =>
        events.Add(new WorldAssetReservationEvent(nextEventId++, worldTick, packageId, kind, detail));
}

internal static class WorldAssetReservationRequestExtensions
{
    public static void ValidateForLedger(this WorldAssetReservationRequest request)
    {
        AssetRules.ValidateCanonicalAssetId(request.AssetId);
        ContentPackageRules.ValidateDigest(request.NormalizedDigest, nameof(request.NormalizedDigest));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DecodeProfile);
        if (request.DurableStorageBytes <= 0 || request.DecodedCacheBytes <= 0 ||
            request.GpuBytes <= 0 || request.RenderUnits is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
    }
}
