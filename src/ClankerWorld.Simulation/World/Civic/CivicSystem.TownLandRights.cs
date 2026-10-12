using System.Globalization;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class CivicSystem
{
    internal HouseholdLandUseRequestResult RequestHouseholdLandUse(ICivicTownWorld world, string requestId,
            string requestedByAgentId, string townId, IReadOnlyList<GridPoint> requestedTiles,
            long? agreedEndTick = null)
    {
        var town = towns.SingleOrDefault(t => t.Id == townId);
        if (town?.Governance is not { } governance)
            return RejectedLandRequest("The Town needs a Council before a use request can be filed.");
        var (result, updated) = FileHouseholdLandUse(world, requestId, requestedByAgentId, townId,
            requestedTiles, agreedEndTick, governance);
        if (result.Applied && !result.IsDuplicate)
        {
            SaveTownGovernance(world, town, updated);
            world.MaintainTownProjects();
        }
        return result;
    }

    internal (HouseholdLandUseRequestResult Result, TownGovernanceState State) FileHouseholdLandUse(
    ICivicTownWorld world, string requestId, string requestedByAgentId, string townId, IReadOnlyList<GridPoint> requestedTiles,
            long? agreedEndTick, TownGovernanceState governance)
    {
        var priorNoticeCount = governance.Notices.Count;
        if (!ValidLandRequestText(requestId, 128) || !TownHearingProcedure.Id(requestedByAgentId) ||
            !ValidLandRequestText(townId, 128) || requestedTiles is not { Count: > 0 and <= CognitionDecisionResponse.MaximumCivicLandTiles } ||
            requestedTiles.Distinct().Count() != requestedTiles.Count ||
            requestedTiles.Any(tile => !world.Map.IsLand(tile)))
            return (RejectedLandRequest("Choose a request ID, an adult Town resident, and one connected land plot."), governance);

        if (founderSetup is not { Started: true } ||
            !world.Society.Inhabitants.Any(person => person.Id == requestedByAgentId &&
                person.Status == SocietyInhabitantStatus.Active &&
                (person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder) && person.HouseholdId is not null) ||
            !towns.Any(town => town.Id == townId &&
                town.ResidentIds.Contains(requestedByAgentId, StringComparer.Ordinal)))
            return (RejectedLandRequest("Only an adult household member of this Town can file a use request."), governance);

        var householdId = world.Society.GetInhabitant(requestedByAgentId).HouseholdId!;
        var tiles = TownLandRightsRules.OrderTiles(requestedTiles);
        if (TownLandRightsRules.ConnectedPlots(world.Map, tiles).Count != 1 ||
            tiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, townId, townLandTitles)))
            return (RejectedLandRequest("The requested plot must be connected land covered by this Town's title."), governance);
        if (agreedEndTick is { } endTick && endTick < world.WorldTick)
            return (RejectedLandRequest("The optional end date cannot be earlier than today."), governance);
        if (householdLandUseRights.Any(right => right.TownId == townId && right.HouseholdId == householdId &&
                tiles.Any(right.Tiles.Contains)))
            return (RejectedLandRequest("This household already has a recorded use right for part of that plot."), governance);
        // Another household's recorded right is contested as a dispute; a building or running expansion without one is not free land.
        var foreignBuildings = BuildingFootprintTiles(world, building => building.HouseholdId != householdId);
        foreignBuildings.UnionWith(ExpansionWorkTiles(world, building => building?.HouseholdId != householdId));
        foreignBuildings.UnionWith(world.MarketSiteTiles());
        if (tiles.Any(tile => foreignBuildings.Contains(tile) && !householdLandUseRights.Any(right => right.Tiles.Contains(tile))))
            return (RejectedLandRequest("That plot includes another household's or the Town's building or expansion work."), governance);
        if (IncludesForeignFieldWithoutUseRight(world, householdId, tiles))
            return (RejectedLandRequest("That plot includes another household's field without a recorded use right."), governance);

        var existingId = householdLandUseRequests.SingleOrDefault(item => item.Id == requestId);
        var proposed = new HouseholdLandUseRequest(requestId, townId, householdId,
            requestedByAgentId, tiles, world.WorldTick, agreedEndTick);
        if (existingId is not null)
        {
            if (SameLandRequest(existingId, proposed))
                return (ExistingLandRequestResult(world, existingId, isDuplicate: true), governance);
            return (RejectedLandRequest("That request ID has already been used for a different request."), governance);
        }

        var duplicate = householdLandUseRequests.FirstOrDefault(item =>
            item.Status == "pending" && item.TownId == proposed.TownId && item.HouseholdId == proposed.HouseholdId &&
            item.AgreedEndTick == proposed.AgreedEndTick &&
            item.Tiles.SequenceEqual(proposed.Tiles));
        if (duplicate is not null)
            return (ExistingLandRequestResult(world, duplicate, isDuplicate: true), governance);

        try
        {
            governance = OpenLandUseProposal(world, towns.Single(t => t.Id == townId), governance, ref proposed);
        }
        catch (InvalidOperationException exception) { return (RejectedLandRequest(exception.Message), governance); }
        governance = TownGovernanceRules.LandUseNotice(governance, proposed.Id,
            $"{requestedByAgentId} requests household use of {TownLandClaimRules.DescribeTiles(tiles)}. " +
            "Every current adult in the household must explicitly accept; filing and Council votes supply no acceptance.", world.WorldTick);
        governance = TownGovernanceRules.LearnNotices(governance, requestedByAgentId,
            governance.Notices.Skip(priorNoticeCount).Select(n => n.Id), world.WorldTick);
        householdLandUseRequests = [.. householdLandUseRequests, proposed];
        householdLandUseRequests = householdLandUseRequests.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        world.AppendEvent("land_use_requested", $"{requestId}:{townId}:{householdId}:{tiles.Length}");
        return (ExistingLandRequestResult(world, proposed, isDuplicate: false), governance);
    }

    internal HouseholdLandUseRequestResult ExistingLandRequestResult(ICivicTownWorld world, HouseholdLandUseRequest request,
            bool isDuplicate) => new(true, request,
            request.Tiles.Any(tile => TownLandRightsRules.IsDisputed(tile, householdLandUseRights, householdLandUseRequests)),
            isDuplicate, null);

    internal static HouseholdLandUseRequestResult RejectedLandRequest(string failure) =>
            new(false, null, false, false, failure);

    internal static bool ValidLandRequestText(string? value, int maximumLength) =>
            !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= maximumLength &&
            !value.Any(char.IsControl);

    internal static bool SameLandRequest(HouseholdLandUseRequest left, HouseholdLandUseRequest right) =>
            left.TownId == right.TownId && left.HouseholdId == right.HouseholdId &&
            left.RequestedByAgentId == right.RequestedByAgentId && left.AgreedEndTick == right.AgreedEndTick &&
            left.Tiles.SequenceEqual(right.Tiles);

}
