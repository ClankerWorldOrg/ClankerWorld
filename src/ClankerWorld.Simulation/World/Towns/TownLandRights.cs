using System.Globalization;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>A saved Town's formal title to one connected group of land tiles.</summary>
public sealed record TownLandTitleRecord(
    string Id,
    string TownId,
    IReadOnlyList<GridPoint> Tiles,
    long RecordedTick);

/// <summary>A household's recorded permission to use one connected group of tiles.</summary>
public sealed record HouseholdLandUseRight(
    string Id,
    string TownId,
    string HouseholdId,
    IReadOnlyList<GridPoint> Tiles,
    long GrantedTick,
    string GrantSource,
    long? AgreedEndTick = null);

/// <summary>
/// A structured request for a household to use Town-titled land. Filing a
/// request does not grant a right or decide a disagreement.
/// </summary>
public sealed record HouseholdLandUseRequest(
    string Id,
    string TownId,
    string HouseholdId,
    string RequestedByAgentId,
    IReadOnlyList<GridPoint> Tiles,
    long RequestedTick,
    long? AgreedEndTick = null)
{
    public string Status { get; init; } = "pending";
    public long? SettledTick { get; init; }
    public string? CouncilProposalId { get; init; }
    public IReadOnlyList<HouseholdLandUseConsent> Consents { get; init; } = [];
    public IReadOnlyList<string> GrantAdults { get; init; } = [];
    [JsonRequired]
    public IReadOnlyList<TownLandRequestResolution> HearingResolutions { get; init; } = [];
}

public sealed record HouseholdLandUseConsent(string AgentId, bool Accepted, long Tick);

public sealed record HouseholdLandUseRequestResult(
    bool Applied,
    HouseholdLandUseRequest? Request,
    bool IsDisputed,
    bool IsDuplicate,
    string? Failure);

/// <summary>Canonical plot grouping and conflict checks for recorded land claims.</summary>
public static class TownLandRightsRules
{
    public const string StarterAllocationSource = "starter_allocation";

    public static IEnumerable<GridPoint> UnresolvedRequestTiles(HouseholdLandUseRequest request) =>
        request.Tiles.Where(tile => !request.HearingResolutions.Any(resolution => resolution.Tiles.Contains(tile)));

    public static GridPoint[] OrderTiles(IEnumerable<GridPoint> tiles) => tiles
        .OrderBy(point => point.Y).ThenBy(point => point.X).ToArray();

    public static IReadOnlyList<GridPoint[]> ConnectedPlots(WorldMap map, IEnumerable<GridPoint> tiles)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);
        var remaining = tiles.ToHashSet();
        if (remaining.Any(point => !map.IsLand(point)))
            throw new ArgumentException("A land plot must contain only land tiles on the map.", nameof(tiles));

        var plots = new List<GridPoint[]>();
        while (remaining.Count > 0)
        {
            var start = remaining.OrderBy(point => point.Y).ThenBy(point => point.X).First();
            var plot = new HashSet<GridPoint> { start };
            var pending = new Queue<GridPoint>();
            pending.Enqueue(start);
            remaining.Remove(start);
            while (pending.TryDequeue(out var current))
            {
                foreach (var next in CardinalNeighbors(map, current))
                {
                    if (remaining.Remove(next))
                    {
                        plot.Add(next);
                        pending.Enqueue(next);
                    }
                }
            }
            plots.Add(OrderTiles(plot));
        }

        return plots.OrderBy(plot => plot[0].Y).ThenBy(plot => plot[0].X).ToArray();
    }

    public static bool IsValidPlot(WorldMap map, IReadOnlyList<GridPoint>? tiles, long worldTick,
        long recordedTick, long? agreedEndTick = null)
    {
        if (!TownSystem.IsLocallyValidPlot(tiles, recordedTick, agreedEndTick) || tiles.Any(point => !map.IsLand(point)) ||
            ConnectedPlots(map, tiles).Count != 1 || recordedTick > worldTick)
            return false;
        return true;
    }

    public static IReadOnlyList<TownLandTitleRecord> InitialTitles(WorldMap map, TownRuntimeState town,
        long recordedTick)
    {
        ArgumentNullException.ThrowIfNull(town);
        return ConnectedPlots(map, town.BorderTiles).Select((plot, index) =>
            new TownLandTitleRecord(StableId("town-title", town.Id, index), town.Id, plot, recordedTick)).ToArray();
    }

    public static IReadOnlyList<HouseholdLandUseRight> InitialUseRights(WorldMap map, string townId,
        IEnumerable<(string HouseholdId, IEnumerable<GridPoint> Tiles)> householdPlots, long grantedTick)
    {
        ArgumentNullException.ThrowIfNull(householdPlots);
        var rights = new List<HouseholdLandUseRight>();
        foreach (var (householdId, tiles) in householdPlots
                     .OrderBy(item => item.HouseholdId, StringComparer.Ordinal))
        {
            var plots = ConnectedPlots(map, tiles);
            for (var index = 0; index < plots.Count; index++)
                rights.Add(new HouseholdLandUseRight(
                    StableId("household-use", townId + ":" + householdId, index),
                    townId,
                    householdId,
                    plots[index],
                    grantedTick,
                    StarterAllocationSource));
        }
        return rights.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    public static bool IsDisputed(GridPoint tile, IReadOnlyList<HouseholdLandUseRight> rights,
        IReadOnlyList<HouseholdLandUseRequest> requests) =>
        ClaimantsAt(tile, rights, requests).Count > 1;

    /// <summary>Partitions existing grants after an authorized building transfer; grants no new land.</summary>
    public static IReadOnlyList<HouseholdLandUseRight> ReassignFootprintRights(WorldMap map,
        IReadOnlyList<HouseholdLandUseRight> rights, IReadOnlySet<GridPoint> footprint,
        string targetHouseholdId, long worldTick)
    {
        var usedIds = rights.Select(right => right.Id).ToHashSet(StringComparer.Ordinal);
        var sequence = 0;
        string NextId()
        {
            string id;
            do
            {
                id = $"household-use:reassigned:{worldTick.ToString(CultureInfo.InvariantCulture)}:{(sequence++).ToString(CultureInfo.InvariantCulture)}";
            } while (!usedIds.Add(id));
            return id;
        }

        var result = new List<HouseholdLandUseRight>();
        foreach (var right in rights.OrderBy(right => right.Id, StringComparer.Ordinal))
        {
            var transferred = right.Tiles.Where(footprint.Contains).ToArray();
            if (transferred.Length == 0)
            {
                result.Add(right);
                continue;
            }
            // Removing a footprint can split a plot. Keep every remaining tile
            // with its owner and retain the original grant terms on each piece.
            var originalIdAvailable = true;
            foreach (var (tiles, owner) in ConnectedPlots(map, right.Tiles.Where(tile => !footprint.Contains(tile)))
                         .Select(plot => (plot, right.HouseholdId))
                         .Concat(ConnectedPlots(map, transferred).Select(plot => (plot, targetHouseholdId))))
            {
                result.Add(right with
                {
                    Id = originalIdAvailable ? right.Id : NextId(),
                    HouseholdId = owner,
                    Tiles = tiles,
                });
                originalIdAvailable = false;
            }
        }
        return result.OrderBy(right => right.Id, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> ClaimantsAt(GridPoint tile,
        IReadOnlyList<HouseholdLandUseRight> rights, IReadOnlyList<HouseholdLandUseRequest> requests) =>
        rights.Where(right => right.Tiles.Contains(tile)).Select(right => right.HouseholdId)
            .Concat(requests.Where(request => request.Status == "pending" && UnresolvedRequestTiles(request).Contains(tile)).Select(request => request.HouseholdId))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    public static bool IsCoveredByTownTitle(GridPoint tile, string townId,
        IReadOnlyList<TownLandTitleRecord> titles) =>
        titles.Any(title => title.TownId == townId && title.Tiles.Contains(tile));

    public static void ValidateRecords(WorldMap map, long worldTick,
        IReadOnlyList<TownRuntimeState>? towns,
        IReadOnlyList<TownLandTitleRecord>? titles,
        IReadOnlyList<HouseholdLandUseRight>? rights,
        IReadOnlyList<HouseholdLandUseRequest>? requests,
        SocietyCheckpoint society)
    {
        if (towns is null)
            throw new InvalidDataException("Saved Town land titles, use rights and requests must be present and cannot contain null entries.");
        TownSystem.ValidateLocalLandRecords(titles, rights, requests);

        var townsById = towns.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var households = society.Households.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var agents = society.Agents.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var titledTiles = new HashSet<GridPoint>();
        foreach (var title in titles)
        {
            if (!townsById.TryGetValue(title.TownId, out var town) || town.OriginSite is null ||
                !IsValidPlot(map, title.Tiles, worldTick, title.RecordedTick) ||
                title.Tiles.Any(tile => !town.BorderTiles.Contains(tile) || !titledTiles.Add(tile)))
                throw new InvalidDataException("A saved Town title is invalid or outside its recorded Town border.");
        }
        TownLandClaimRules.Validate(map, worldTick, towns, titles);

        if (titles.Count > 0 && towns.All(town => town.OriginSite is null))
            throw new InvalidDataException("Town title may only be created from an accepted first-Town layout.");

        var grantedUseTiles = new HashSet<GridPoint>();
        foreach (var right in rights)
        {
            if (!townsById.ContainsKey(right.TownId) || !households.Contains(right.HouseholdId) ||
                !IsValidPlot(map, right.Tiles, worldTick, right.GrantedTick, right.AgreedEndTick) ||
                right.Tiles.Any(tile => !IsCoveredByTownTitle(tile, right.TownId, titles) ||
                    !grantedUseTiles.Add(tile)))
                throw new InvalidDataException("A saved household land-use right is invalid or not covered by Town title.");
        }

        foreach (var request in requests)
        {
            if (!townsById.ContainsKey(request.TownId) || !households.Contains(request.HouseholdId) || !agents.Contains(request.RequestedByAgentId) ||
                !IsValidPlot(map, request.Tiles, worldTick, request.RequestedTick, request.AgreedEndTick) ||
                request.Tiles.Any(tile => !IsCoveredByTownTitle(tile, request.TownId, titles)))
                throw new InvalidDataException("A saved household land-use request is invalid or not covered by Town title.");
        }
        HouseholdLandGrantRules.Validate(worldTick, towns, rights, requests, agents);
    }

    private static IEnumerable<GridPoint> CardinalNeighbors(WorldMap map, GridPoint point)
    {
        foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
        {
            var neighbor = map.WrapColumn(new GridPoint(point.X + dx, point.Y + dy));
            if (map.Contains(neighbor))
                yield return neighbor;
        }
    }

    private static string StableId(string prefix, string scope, int index) =>
        DerivedId.Create($"{prefix}:{scope}:{index.ToString(CultureInfo.InvariantCulture)}", prefix == "household-use" ? "household-use:" : null);

    internal static bool ValidId(string? value) => ValidText(value, DerivedId.MaximumLength);

    internal static bool ValidText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= maximumLength &&
        !value.Any(char.IsControl);
}

public sealed partial class WorldRuntime
{
    /// <summary>
    /// Files a structured household use request for Council consideration.
    /// Filing supplies no household acceptance and cannot settle a dispute.
    /// </summary>
    public HouseholdLandUseRequestResult RequestHouseholdLandUse(string requestId,
        string requestedByAgentId, string townId, IReadOnlyList<GridPoint> requestedTiles,
        long? agreedEndTick = null)
    {
        gate.Wait();
        try { return townSystem.RequestHouseholdLandUse(civicWorld, requestId, requestedByAgentId, townId, requestedTiles, agreedEndTick); }
        finally { gate.Release(); }
    }

    private (HouseholdLandUseRequestResult Result, TownCouncilState State) FileHouseholdLandUse(
        string requestId, string requestedByAgentId, string townId, IReadOnlyList<GridPoint> requestedTiles,
        long? agreedEndTick, TownCouncilState council) => townSystem.FileHouseholdLandUse(civicWorld, requestId, requestedByAgentId, townId, requestedTiles, agreedEndTick, council);
    private HouseholdLandUseRequestResult ExistingLandRequestResult(HouseholdLandUseRequest request,
        bool isDuplicate) => townSystem.ExistingLandRequestResult(civicWorld, request, isDuplicate);
    private static HouseholdLandUseRequestResult RejectedLandRequest(string failure) => TownSystem.RejectedLandRequest(failure);
    private static bool ValidLandRequestText(string? value, int maximumLength) => TownSystem.ValidLandRequestText(value, maximumLength);
    private static bool SameLandRequest(HouseholdLandUseRequest left, HouseholdLandUseRequest right) => TownSystem.SameLandRequest(left, right);
}
