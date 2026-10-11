using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class CivicSystem
{
    internal TownGovernanceState SubmitTownLandClaim(ICivicTownWorld world, TownRuntimeState town, TownGovernanceState state,
            string actor, IReadOnlyList<CognitionLandTile>? requested)
    {
        var tiles = requested?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray();
        if (!TownLandClaimRules.CanClaim(world.Map, town, tiles, townLandTitles))
            throw new InvalidOperationException("A land claim needs connected unclaimed land adjoining this Town's title.");
        var first = TownLandRightsRules.OrderTiles(tiles!)[0];
        var text = string.Create(CultureInfo.InvariantCulture,
            $"Claim {tiles!.Length} land tiles adjoining {town.Name}, starting at ({first.X}, {first.Y}).");
        return TownGovernanceRules.SubmitProposal(state, town.Id, actor, "land_claim", null, text,
            "council:" + state.Revision, TownAdults(world, town), world.WorldTick, CivicDay(world), landClaimTiles: tiles);
    }

    internal (TownRuntimeState Town, TownGovernanceState Governance) ApplyApprovedTownLandClaims(
    ICivicTownWorld world, TownRuntimeState town, TownGovernanceState updated)
    {
        foreach (var proposal in updated.Proposals.Where(p => p.Kind == "land_claim" &&
                     (p.Status == "pending" || p.Status == "passed" &&
                         !town.Governance!.Proposals.Any(old => old.Id == p.Id && old.Status == "passed"))).ToArray())
        {
            if (!TownLandClaimRules.CanClaim(world.Map, town, proposal.LandClaimTiles, townLandTitles))
            {
                updated = TownGovernanceRules.CancelLandClaim(updated, proposal, world.WorldTick);
                continue;
            }
            if (proposal.Status != "passed") continue;
            var tiles = proposal.LandClaimTiles!;
            townLandTitles = [.. townLandTitles, new(TownLandClaimRules.TitleId(proposal.Id), town.Id, tiles.ToArray(), world.WorldTick)];
            townLandTitles = townLandTitles.OrderBy(title => title.Id, StringComparer.Ordinal).ToList();
            town = town with { BorderTiles = TownLandRightsRules.OrderTiles(town.BorderTiles.Concat(tiles).Distinct()) };
            world.AppendEvent("town_land_claimed", $"{town.Id}|{proposal.Id}|{TownLandClaimRules.DescribeTiles(tiles)}", tiles[0]);
        }
        return (town, updated);
    }

}
