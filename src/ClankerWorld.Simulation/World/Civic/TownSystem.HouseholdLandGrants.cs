using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal TownCouncilState OpenLandUseProposal(ITownWorld world, TownRuntimeState town, TownCouncilState state,
            ref HouseholdLandUseRequest request)
    {
        if (request.CouncilProposalId is not null || HasOpenLandHearingPlot(world, town.Id, request.Tiles) ||
            !HouseholdLandGrantRules.IsAvailable(request, householdLandUseRights, householdLandUseRequests) ||
            !TownAdults(world, town).Contains(request.RequestedByAgentId, StringComparer.Ordinal)) return state;
        var text = $"Grant household use of {request.Tiles.Count} land tile(s); every current adult in the household must separately accept.";
        state = TownCouncilRules.SubmitProposal(state, town.Id, request.RequestedByAgentId, "land_use", request.Id,
            text, "council:" + state.Revision, TownAdults(world, town), world.WorldTick, CivicDay(world), landUseRequest: request);
        var key = HouseholdLandGrantRules.RequestKey(request);
        request = request with { CouncilProposalId = state.Proposals.Single(p => p.RequestKey == key && p.Status == "pending").Id };
        return state;
    }

    internal TownCouncilState ResolveHouseholdLandRequests(ITownWorld world, TownRuntimeState town, TownCouncilState state)
    {
        foreach (var saved in householdLandUseRequests.Where(r => r.TownId == town.Id && r.Status == "pending").ToArray())
        {
            var request = saved;
            var adults = world.HouseholdAdults(request.HouseholdId);
            var proposal = state.Proposals.SingleOrDefault(p => p.Id == request.CouncilProposalId);
            var refusal = HouseholdLandGrantRules.RefusalReason(request, adults, proposal, world.WorldTick);
            if (refusal is not null)
            {
                request = request with { Status = "rejected", SettledTick = world.WorldTick };
                if (request.CouncilProposalId is { } proposalId)
                    state = TownCouncilRules.CancelLandUseProposal(state, proposalId, world.WorldTick);
                state = TownCouncilRules.LandUseNotice(state, request.Id, "Household land request refused: " + refusal, world.WorldTick);
            }
            else if (!HasOpenLandHearingPlot(world, town.Id, request.Tiles) &&
                HouseholdLandGrantRules.IsAvailable(request, householdLandUseRights, householdLandUseRequests) &&
                !IncludesForeignFieldWithoutUseRight(world, request.HouseholdId, request.Tiles))
            {
                if (proposal is null)
                {
                    try { state = OpenLandUseProposal(world, town, state, ref request); }
                    catch (InvalidOperationException) { /* Ordinary Council retry rules still apply. */ }
                }
                else if (proposal.Status == "passed" && adults.All(id => request.Consents.Any(c => c.AgentId == id && c.Accepted)))
                {
                    request = request with { Status = "granted", SettledTick = world.WorldTick, GrantAdults = adults };
                    householdLandUseRights = [.. householdLandUseRights, new(HouseholdLandGrantRules.RightId(request.Id), town.Id, request.HouseholdId,
                        request.Tiles, world.WorldTick, HouseholdLandGrantRules.GrantSource(request.Id), request.AgreedEndTick)];
                    householdLandUseRights = householdLandUseRights.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
                    state = TownCouncilRules.LandUseNotice(state, request.Id,
                        "Household land use granted for " + TownLandClaimRules.DescribeTiles(request.Tiles) + ".", world.WorldTick);
                    world.AppendEvent(new LandUseGrantedEventPayload(null, request.Id, town.Id, request.HouseholdId, request.Tiles.Count));
                }
            }
            if (request != saved)
                householdLandUseRequests = householdLandUseRequests.Select(r => r.Id == request.Id ? request : r).ToList();
        }
        return state;
    }

    internal HashSet<GridPoint> HouseholdLandHeldByOthers(ITownWorld world, string? household) => householdLandUseRights
            .Where(right => right.HouseholdId != household).SelectMany(right => right.Tiles)
            .Concat(householdLandUseRequests.Where(request => request.Status == "pending" && request.HouseholdId != household)
                .SelectMany(TownLandRightsRules.UnresolvedRequestTiles))
            .ToHashSet();

    internal bool IncludesForeignFieldWithoutUseRight(ITownWorld world, string household, IReadOnlyList<GridPoint> tiles) =>
            world.Fields.Any(field => field.HouseholdId != household && tiles.Contains(field.Position) &&
                !householdLandUseRights.Any(right => right.Tiles.Contains(field.Position)));

    internal static HashSet<GridPoint> BuildingFootprintTiles(ITownWorld world, Func<PlacedBuilding, bool> include) =>
            world.Ground.KnownBuildingFootprints(include).ToHashSet();

    internal static HashSet<GridPoint> ExpansionWorkTiles(ITownWorld world, Func<PlacedBuilding?, bool> include) =>
            (world.WorldSimulation.BuildingExpansions ?? []).Where(job => job.State is WorldProductionJobState.Running or WorldProductionJobState.Paused &&
                    include(world.WorldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == job.BuildingInstanceId)))
                .SelectMany(WorldRuntime.CivicSharedRules.ExpansionTiles).ToHashSet();

    internal GridPoint[] RequestableLandNear(ITownWorld world, TownRuntimeState town, GridPoint from, int count)
    {
        var taken = HouseholdLandHeldByOthers(world, null);
        taken.UnionWith(BuildingFootprintTiles(world, _ => true));
        taken.UnionWith(ExpansionWorkTiles(world, _ => true));
        taken.UnionWith(world.RoadAndBridgeTiles());
        taken.UnionWith(world.Fields.Select(field => field.Position));
        taken.UnionWith(world.TownProjectProtectedSites());
        taken.UnionWith(world.PendingTownProjectSiteTiles());
        taken.UnionWith(world.MarketSiteTiles());
        return townLandTitles.Where(title => title.TownId == town.Id).SelectMany(title => title.Tiles)
            .Where(tile => world.Map.IsLand(tile) && !taken.Contains(tile)).Distinct()
            .OrderBy(tile => world.Map.FootDistance(from, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
            .Take(count).ToArray();
    }

    internal static string LandUseTerms(ITownWorld world, HouseholdLandUseRequest request) =>
            "Exact tiles: " + TownLandClaimRules.DescribeTiles(request.Tiles) + ". " +
            (request.AgreedEndTick is { } end ? $"Agreed end: world day {CivicDayNumber(world, end)}. " : "No agreed end date. ");

    internal void AddHouseholdLandCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (TownAdults(world, town).Contains(actor, StringComparer.Ordinal))
            foreach (var building in world.WorldSimulation.Buildings.Where(b => b.TownId == town.Id && b.HouseholdId == world.HouseholdFor(actor)))
                if (ExpansionLandRequestTiles(world, actor, building) is { Length: > 0 } extra)
                    candidates.Add(new(CivicAction(world, town.Id, "request_expansion_land", building.InstanceId, TownLandClaimRules.DescribeTiles(extra)),
                        "Request household use of the extra land needed to expand your House. Council approval and every current adult's acceptance are required. Exact tiles: " +
                        TownLandClaimRules.DescribeTiles(extra) + ".", 190));
        var history = CivicHistory(town);
        foreach (var request in householdLandUseRequests.Where(r => r.TownId == town.Id && r.Status == "pending" && history.Knows(actor, "land_use", r.Id)))
        {
            var terms = request.HearingResolutions.Count == 0 ? LandUseTerms(world, request) :
                "Remaining unheard requested tiles: " + TownLandClaimRules.DescribeTiles(TownLandRightsRules.UnresolvedRequestTiles(request)) +
                ". Completed hearing decisions remain. A new ordinary grant requires a separate request for its exact plot. ";
            if (request.RequestedByAgentId == actor)
                candidates.Add(new(CivicAction(world, town.Id, "withdraw_land_use", request.Id),
                    "Withdraw your pending household land request. " + terms, 190));
            if (!world.HouseholdAdults(request.HouseholdId).Contains(actor, StringComparer.Ordinal) || request.Consents.Any(c => c.AgentId == actor)) continue;
            if (request.HearingResolutions.Count == 0)
                candidates.Add(new(CivicAction(world, town.Id, "accept_land_use", request.Id),
                    "Personally accept the requested use right for your household. Council approval and every current adult's acceptance are also required. " + terms, 165));
            candidates.Add(new(CivicAction(world, town.Id, "decline_land_use", request.Id),
                "Personally decline the requested use right for your household. " + terms, 166));
        }
    }

    internal GridPoint[]? ExpansionLandRequestTiles(ITownWorld world, string actor, PlacedBuilding building)
    {
        if (building.HouseholdId is null || !world.MayExpandBuilding(actor, building, out _) ||
            world.ExpansionShapes(building).Any(shape => world.CanFitExpansion(building, shape.Position, shape.Footprint, out _))) return null;
        var definition = world.WorldContent.Buildings.Single(d => d.CanonicalId == building.DefinitionId);
        var pending = householdLandUseRequests.Where(r => r.Status == "pending" && r.HouseholdId == building.HouseholdId)
            .SelectMany(TownLandRightsRules.UnresolvedRequestTiles).ToHashSet();
        // A footprint whose extra land is free can be granted; one over land another household
        // holds or has asked for only opens a dispute, so it is offered when no free one fits.
        var heldByOthers = HouseholdLandHeldByOthers(world, building.HouseholdId);
        var proposedHalls = world.PendingTownProjectSiteTiles();
        GridPoint[]? offered = null;
        GridPoint[]? disputed = null;
        foreach (var shape in world.ExpansionShapes(building))
        {
            if (!world.CanFitExpansion(building, shape.Position, shape.Footprint, out _, requireLandRights: false)) continue;
            var extra = TownLandRightsRules.OrderTiles(WorldContentSimulationRules.Footprint(
                BuildingStorageRules.WithSize(definition, shape.Footprint.Width, shape.Footprint.Height), shape.Position)
                .Except(WorldContentSimulationRules.Footprint(definition, building))
                .Where(tile => !householdLandUseRights.Any(right => right.HouseholdId == building.HouseholdId && right.Tiles.Contains(tile))));
            if (extra.Length == 0 || extra.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, building.TownId!, townLandTitles))) continue;
            // One expansion asks the Council once: a pending request for any of its shapes waits to be decided.
            if (extra.Any(pending.Contains)) return null;
            if (extra.Any(proposedHalls.Contains)) continue;
            if (extra.Any(heldByOthers.Contains)) disputed ??= extra;
            else offered ??= extra;
        }
        return offered ?? disputed;
    }

    internal TownCouncilState SubmitExpansionLandRequest(ITownWorld world, TownRuntimeState town, TownCouncilState state, string actor, string buildingId)
    {
        var building = world.WorldSimulation.Buildings.Single(b => b.InstanceId == buildingId);
        var tiles = ExpansionLandRequestTiles(world, actor, building) ?? throw new InvalidOperationException("The expansion's land need has changed.");
        return SubmitPersonalLandUseRequest(world, town, state, actor, tiles.Select(t => new CognitionLandTile(t.X, t.Y)).ToArray());
    }

    internal TownCouncilState DecideHouseholdLandUse(ICivicLandConsentWorld world, TownCouncilState state, string actor, string requestId, string action)
    {
        var request = householdLandUseRequests.Single(r => r.Id == requestId && r.Status == "pending");
        if (action == "withdraw_land_use")
        {
            if (request.RequestedByAgentId != actor) throw new InvalidOperationException("Only the filer may withdraw this request.");
            request = request with { Status = "withdrawn", SettledTick = world.WorldTick };
            if (request.CouncilProposalId is { } proposalId)
                state = TownCouncilRules.CancelLandUseProposal(state, proposalId, world.WorldTick);
            state = TownCouncilRules.LandUseNotice(state, request.Id, "The household land request was withdrawn by its filer.", world.WorldTick);
        }
        else
        {
            if (!world.HouseholdAdults(request.HouseholdId).Contains(actor, StringComparer.Ordinal) || request.Consents.Any(c => c.AgentId == actor))
                throw new InvalidOperationException("Land acceptance requires an explicit choice from a current adult household member.");
            request = request with
            {
                Consents = request.Consents.Append(new(actor, action == "accept_land_use", world.WorldTick))
                .OrderBy(c => c.AgentId, StringComparer.Ordinal).ToArray()
            };
        }
        householdLandUseRequests = householdLandUseRequests.Select(r => r.Id == request.Id ? request : r).ToList();
        return state;
    }

    internal TownCouncilState SubmitPersonalLandUseRequest(ITownWorld world, TownRuntimeState town, TownCouncilState state,
            string actor, IReadOnlyList<CognitionLandTile>? landTiles)
    {
        if (landTiles is not { Count: > 0 and <= CognitionDecisionResponse.MaximumCivicLandTiles })
            throw new InvalidOperationException("Choose exact land tiles for the household request.");
        var (result, updated) = FileHouseholdLandUse(world, TownRecordIds.LandRequest(town.Id, TownCouncilRules.NextNoticeSequence(state)),
            actor, town.Id, landTiles.Select(t => new GridPoint(t.X, t.Y)).ToArray(), null, state);
        if (!result.Applied) throw new InvalidOperationException(result.Failure);
        return updated;
    }

}
