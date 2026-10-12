using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

public static class TownLandClaimRules
{
    public static string DescribeTiles(IEnumerable<GridPoint> tiles) => string.Join("; ",
        TownLandRightsRules.OrderTiles(tiles).Select(tile => FormattableString.Invariant($"({tile.X}, {tile.Y})")));

    public static string RequestKey(IEnumerable<GridPoint> tiles) => "land_claim:" +
        DerivedId.Hash(DescribeTiles(tiles));

    public static string TitleId(string proposalId) => DerivedId.Hash(proposalId, "town-title:claim:");

    public static bool CanClaim(WorldMap map, TownRuntimeState town, IReadOnlyList<GridPoint>? tiles,
        IReadOnlyList<TownLandTitleRecord> titles)
    {
        if (town.OriginSite is null || tiles is not { Count: > 0 and <= CognitionDecisionResponse.MaximumCivicLandTiles } ||
            tiles.Any(tile => !map.IsLand(tile)) || tiles.Distinct().Count() != tiles.Count ||
            TownLandRightsRules.ConnectedPlots(map, tiles).Count != 1)
            return false;
        var claimed = titles.SelectMany(title => title.Tiles).ToHashSet();
        if (tiles.Any(claimed.Contains)) return false;
        var own = titles.Where(title => title.TownId == town.Id).SelectMany(title => title.Tiles).ToHashSet();
        return tiles.Any(tile => new[] { new GridPoint(tile.X - 1, tile.Y), new GridPoint(tile.X + 1, tile.Y),
            new GridPoint(tile.X, tile.Y - 1), new GridPoint(tile.X, tile.Y + 1) }
            .Any(neighbor => own.Contains(map.WrapColumn(neighbor))));
    }

    /// <summary>Unclaimed land tiles beside this Town's title, nearest first; each one alone is a claimable plot.</summary>
    public static GridPoint[] ClaimableNear(WorldMap map, TownRuntimeState town, IReadOnlyList<TownLandTitleRecord> titles,
        GridPoint from, int count)
    {
        if (town.OriginSite is null) return [];
        var claimed = titles.SelectMany(title => title.Tiles).ToHashSet();
        return titles.Where(title => title.TownId == town.Id).SelectMany(title => title.Tiles)
            .SelectMany(tile => new[] { new GridPoint(tile.X - 1, tile.Y), new GridPoint(tile.X + 1, tile.Y),
                new GridPoint(tile.X, tile.Y - 1), new GridPoint(tile.X, tile.Y + 1) })
            .Select(map.WrapColumn).Where(tile => map.IsLand(tile) && !claimed.Contains(tile)).Distinct()
            .OrderBy(tile => map.FootDistance(from, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
            .Take(count).ToArray();
    }

    public static void Validate(WorldMap map, long tick, IReadOnlyList<TownRuntimeState> towns,
        IReadOnlyList<TownLandTitleRecord> titles)
    {
        var approvals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var town in towns)
            foreach (var proposal in town.Governance?.Proposals.Where(p => p.Kind == "land_claim") ?? [])
            {
                if (proposal.LandClaimTiles is not { Count: <= CognitionDecisionResponse.MaximumCivicLandTiles } tiles ||
                    !TownLandRightsRules.IsValidPlot(map, tiles, tick, proposal.OpenedTick) ||
                    proposal.RequestKey != RequestKey(tiles))
                    throw new InvalidDataException("A saved Council land claim must name one exact connected land plot.");
                var id = TitleId(proposal.Id);
                var title = titles.SingleOrDefault(title => title.Id == id);
                if (proposal.Status == "passed")
                {
                    if (title is null || title.TownId != town.Id || title.RecordedTick != proposal.SettledTick ||
                        !title.Tiles.SequenceEqual(tiles))
                        throw new InvalidDataException("A passed Council land claim must match its recorded Town title.");
                    approvals.Add(id);
                }
                else if (title is not null)
                    throw new InvalidDataException("An unfinished or refused Council land claim cannot hold title.");
            }
        if (titles.Any(title => title.Id.StartsWith("town-title:claim:", StringComparison.Ordinal) && !approvals.Contains(title.Id)))
            throw new InvalidDataException("A Council land title is missing its approval.");
    }
}

public sealed partial class WorldRuntime
{
    private TownGovernanceState SubmitTownLandClaim(TownRuntimeState town, TownGovernanceState state,
        string actor, IReadOnlyList<CognitionLandTile>? requested) => civicSystem.SubmitTownLandClaim(civicWorld, town, state, actor, requested);
    private (TownRuntimeState Town, TownGovernanceState Governance) ApplyApprovedTownLandClaims(
        TownRuntimeState town, TownGovernanceState updated) => civicSystem.ApplyApprovedTownLandClaims(civicWorld, town, updated);
}
