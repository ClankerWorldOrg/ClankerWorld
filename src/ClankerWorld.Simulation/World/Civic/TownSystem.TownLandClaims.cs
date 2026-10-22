using System.Globalization;
using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal TownCouncilState SubmitTownLandClaim(ITownWorld world, TownRuntimeState town, TownCouncilState state,
            string actor, IReadOnlyList<CognitionLandTile>? requested, TownLandClaimActionPlan? plan = null)
    {
        var tiles = plan?.SelectedTiles?.ToArray() ?? requested?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray();
        if (!TownLandClaimRules.CanClaim(world.Map, town, tiles, townLandTitles))
            throw new InvalidOperationException("A land claim needs connected unclaimed land adjoining this Town's title.");
        var first = TownLandRightsRules.OrderTiles(tiles!)[0];
        var text = string.Create(CultureInfo.InvariantCulture,
            $"Claim {tiles!.Length} land tiles adjoining {town.Name}, starting at ({first.X}, {first.Y}).");
        return TownCouncilRules.SubmitProposal(state, town.Id, actor, "land_claim", null, text,
            "council:" + state.Revision, TownAdults(world, town), world.WorldTick, CivicDay(world), landClaimTiles: tiles);
    }

    internal (TownRuntimeState Town, TownCouncilState Council) ApplyApprovedTownLandClaims(
    ITownWorld world, TownRuntimeState town, TownCouncilState updated)
    {
        foreach (var proposal in updated.Proposals.Where(p => p.Kind == "land_claim" &&
                     (p.Status == "pending" || p.Status == "passed" &&
                         !town.Council!.Proposals.Any(old => old.Id == p.Id && old.Status == "passed"))).ToArray())
        {
            if (!TownLandClaimRules.CanClaim(world.Map, town, proposal.LandClaimTiles, townLandTitles))
            {
                updated = TownCouncilRules.CancelLandClaim(updated, proposal, world.WorldTick);
                continue;
            }
            if (proposal.Status != "passed") continue;
            var tiles = proposal.LandClaimTiles!;
            townLandTitles = [.. townLandTitles, new(TownLandClaimRules.TitleId(proposal.Id), town.Id, tiles.ToArray(), world.WorldTick)];
            townLandTitles = townLandTitles.OrderBy(title => title.Id, StringComparer.Ordinal).ToList();
            town = town with { BorderTiles = TownLandRightsRules.OrderTiles(town.BorderTiles.Concat(tiles).Distinct()) };
            world.AppendEvent(new TownLandClaimedEventPayload(null, town.Id, proposal.Id, TownLandClaimRules.DescribeTiles(tiles)), tiles[0]);
        }
        return (town, updated);
    }

}
