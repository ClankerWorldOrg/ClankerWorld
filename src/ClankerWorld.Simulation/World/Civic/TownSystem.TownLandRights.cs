using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;
using System.Globalization;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal HouseholdLandUseRequestResult RequestHouseholdLandUse(ITownWorld world, string requestId,
            string requestedByAgentId, string townId, IReadOnlyList<GridPoint> requestedTiles,
            long? agreedEndTick = null)
    {
        var town = State.Towns.SingleOrDefault(t => t.Id == townId);
        if (town?.Council is not { } council)
            return RejectedLandRequest("The Town needs a Council before a use request can be filed.");
        var (result, updated) = FileHouseholdLandUse(world, requestId, requestedByAgentId, townId,
            requestedTiles, agreedEndTick, council);
        if (result.Applied && !result.IsDuplicate)
        {
            SaveTownCouncil(world, town, updated);
            world.MaintainTownProjects();
        }
        return result;
    }

    internal (HouseholdLandUseRequestResult Result, TownCouncilState State) FileHouseholdLandUse(
    ITownWorld world, string requestId, string requestedByAgentId, string townId, IReadOnlyList<GridPoint> requestedTiles,
            long? agreedEndTick, TownCouncilState council)
    {
        var priorNoticeCount = council.Notices.Count;
        if (HouseholdLandFilingFailure(world, requestId, requestedByAgentId, townId, requestedTiles, agreedEndTick) is { } failure)
            return (RejectedLandRequest(failure), council);
        var householdId = world.Society.GetAgent(requestedByAgentId).HouseholdId!;
        var tiles = TownLandRightsRules.OrderTiles(requestedTiles);

        var existingId = State.HouseholdLandUseRequests.SingleOrDefault(item => item.Id == requestId);
        var proposed = new HouseholdLandUseRequest(requestId, townId, householdId,
            requestedByAgentId, tiles, world.WorldTick, agreedEndTick);
        if (existingId is not null)
        {
            if (SameLandRequest(existingId, proposed))
                return (ExistingLandRequestResult(world, existingId, isDuplicate: true), council);
            return (RejectedLandRequest("That request ID has already been used for a different request."), council);
        }

        var duplicate = State.HouseholdLandUseRequests.FirstOrDefault(item =>
            item.Status == "pending" && item.TownId == proposed.TownId && item.HouseholdId == proposed.HouseholdId &&
            item.AgreedEndTick == proposed.AgreedEndTick &&
            item.Tiles.SequenceEqual(proposed.Tiles));
        if (duplicate is not null)
            return (ExistingLandRequestResult(world, duplicate, isDuplicate: true), council);

        try
        {
            council = OpenLandUseProposal(world, State.Towns.Single(t => t.Id == townId), council, ref proposed);
        }
        catch (InvalidOperationException exception) { return (RejectedLandRequest(exception.Message), council); }
        council = TownCouncilRules.LandUseNotice(council, proposed.Id,
            $"{requestedByAgentId} requests household use of {TownLandClaimRules.DescribeTiles(tiles)}. " +
            "Every current adult in the household must explicitly accept; filing and Council votes supply no acceptance.", world.WorldTick);
        council = TownCouncilRules.LearnNotices(council, requestedByAgentId,
            council.Notices.Skip(priorNoticeCount).Select(n => n.Id), world.WorldTick);
        RestoreHouseholdLandUseRequests([.. State.HouseholdLandUseRequests, proposed]);
        RestoreHouseholdLandUseRequests(State.HouseholdLandUseRequests.OrderBy(item => item.Id, StringComparer.Ordinal).ToList());
        world.AppendEvent(new LandUseRequestedEventPayload(null, requestId, townId, householdId, tiles.Length));
        return (ExistingLandRequestResult(world, proposed, isDuplicate: false), council);
    }

    internal string? HouseholdLandFilingFailure(ITownWorld world, string requestId, string requestedByAgentId,
        string townId, IReadOnlyList<GridPoint> requestedTiles, long? agreedEndTick)
    {
        if (!IdentityRules.Reference(requestId, 128, requireTrimmed: true) || !IdentityRules.Reference(requestedByAgentId, int.MaxValue, requireTrimmed: true) ||
            !IdentityRules.Reference(townId, 128, requireTrimmed: true) || requestedTiles is not { Count: > 0 and <= CognitionDecisionResponse.MaximumCivicLandTiles } ||
            requestedTiles.Distinct().Count() != requestedTiles.Count ||
            requestedTiles.Any(tile => !world.Map.IsLand(tile)))
            return "Choose a request ID, an adult Town resident, and one connected land plot.";

        if (State.FounderSetup is not { Started: true } ||
            !world.Society.Agents.Any(person => person.Id == requestedByAgentId &&
                person.Status == SocietyAgentStatus.Active &&
                LifeStageRules.IsAdult(person.AgeBand) && person.HouseholdId is not null) ||
            !State.Towns.Any(town => town.Id == townId &&
                town.ResidentIds.Contains(requestedByAgentId, StringComparer.Ordinal)))
            return "Only an adult household member of this Town can file a use request.";

        var householdId = world.Society.GetAgent(requestedByAgentId).HouseholdId!;
        var tiles = TownLandRightsRules.OrderTiles(requestedTiles);
        if (TownLandRightsRules.ConnectedPlots(world.Map, tiles).Count != 1 ||
            tiles.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, townId, State.TownLandTitles)))
            return "The requested plot must be connected land covered by this Town's title.";
        if (agreedEndTick is { } endTick && endTick < world.WorldTick)
            return "The optional end date cannot be earlier than today.";
        if (State.HouseholdLandUseRights.Any(right => right.TownId == townId && right.HouseholdId == householdId &&
                tiles.Any(right.Tiles.Contains)))
            return "This household already has a recorded use right for part of that plot.";
        // Another household's recorded right is contested as a dispute; a building or running expansion without one is not free land.
        var foreignBuildings = BuildingFootprintTiles(world, building => building.HouseholdId != householdId);
        foreignBuildings.UnionWith(ExpansionWorkTiles(world, building => building?.HouseholdId != householdId));
        foreignBuildings.UnionWith(world.MarketSiteTiles());
        if (tiles.Any(tile => foreignBuildings.Contains(tile) && !State.HouseholdLandUseRights.Any(right => right.Tiles.Contains(tile))))
            return "That plot includes another household's or the Town's building or expansion work.";
        if (IncludesForeignFieldWithoutUseRight(world, householdId, tiles))
            return "That plot includes another household's field without a recorded use right.";

        return null;
    }

    internal HouseholdLandUseRequestResult ExistingLandRequestResult(ITownWorld world, HouseholdLandUseRequest request,
            bool isDuplicate) => new(true, request,
            request.Tiles.Any(tile => TownLandRightsRules.IsDisputed(tile, State.HouseholdLandUseRights, State.HouseholdLandUseRequests)),
            isDuplicate, null);

    internal static HouseholdLandUseRequestResult RejectedLandRequest(string failure) =>
            new(false, null, false, false, failure);

    internal static bool SameLandRequest(HouseholdLandUseRequest left, HouseholdLandUseRequest right) =>
            left.TownId == right.TownId && left.HouseholdId == right.HouseholdId &&
            left.RequestedByAgentId == right.RequestedByAgentId && left.AgreedEndTick == right.AgreedEndTick &&
            left.Tiles.SequenceEqual(right.Tiles);
    internal TownCouncilState SubmitTownLandClaim(ITownWorld world, TownRuntimeState town, TownCouncilState state,
            string actor, IReadOnlyList<CognitionLandTile>? requested, TownLandClaimActionPlan? plan = null)
    {
        var tiles = plan?.SelectedTiles?.ToArray() ?? requested?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray();
        if (!TownLandClaimRules.CanClaim(world.Map, town, tiles, State.TownLandTitles))
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
            if (!TownLandClaimRules.CanClaim(world.Map, town, proposal.LandClaimTiles, State.TownLandTitles))
            {
                updated = TownCouncilRules.CancelLandClaim(updated, proposal, world.WorldTick);
                continue;
            }
            if (proposal.Status != "passed") continue;
            var tiles = proposal.LandClaimTiles!;
            RestoreTownLandTitles([.. State.TownLandTitles, new(TownLandClaimRules.TitleId(proposal.Id), town.Id, tiles.ToArray(), world.WorldTick)]);
            RestoreTownLandTitles(State.TownLandTitles.OrderBy(title => title.Id, StringComparer.Ordinal).ToList());
            town = town with { BorderTiles = TownLandRightsRules.OrderTiles(town.BorderTiles.Concat(tiles).Distinct()) };
            world.AppendEvent(new TownLandClaimedEventPayload(null, town.Id, proposal.Id, TownLandClaimRules.DescribeTiles(tiles)), tiles[0]);
        }
        return (town, updated);
    }
    internal static TownLandClaimActionPlan? PlanTownLandClaim(ITownWorld world, string actor, TownLandClaimChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town || !world.Agents.TryGetValue(actor, out var person)) return null;
        return new(actor, choice, TownLandClaimRules.ClaimableNear(world.Map, town, world.TownLandTitles, person.Position, 6));
    }

    internal static TownLandClaimActionPlan BindTownLandClaim(TownLandClaimActionPlan plan, IReadOnlyList<CognitionLandTile>? tiles) =>
        plan.SelectedTiles is not null ? plan : plan with { SelectedTiles = tiles?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray() ?? [] };

    internal static ActionCheck CheckTownLandClaim(ITownWorld world, TownLandClaimActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            (plan.SelectedTiles is { } selected ? !TownLandClaimRules.CanClaim(world.Map, town, selected, world.TownLandTitles) :
                TownLandClaimRules.ClaimableNear(world.Map, town, world.TownLandTitles, world.Agents[plan.Actor].Position, 1).Length == 0))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected plot is no longer connected unclaimed Town land");
        return new();
    }

    private static void OfferTownLandClaim(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town, string siteRecovery)
    {
        if (PlanTownLandClaim(world, actor, new(town.Id)) is not { } plan || !CheckTownLandClaim(world, plan).Allowed) return;
        var here = world.Agents[actor].Position;
        candidates.Add(new(plan.CandidateId, siteRecovery + $"Ask {town.Name}'s council to claim a connected plot of adjoining unclaimed land; include its exact coordinates in civic_land_tiles. " +
            FormattableString.Invariant($"You stand at ({here.X}, {here.Y}); unclaimed tiles beside the Town's land nearest you: {string.Join("; ", plan.OfferedTiles.Select(tile => FormattableString.Invariant($"({tile.X}, {tile.Y})")))}. ") +
            "Existing titles, household rights, buildings and goods stay with their holders.", 190)
        { ActionPlan = plan });
    }
    internal TownCouncilState OpenLandUseProposal(ITownWorld world, TownRuntimeState town, TownCouncilState state,
            ref HouseholdLandUseRequest request)
    {
        if (request.CouncilProposalId is not null || HasOpenLandHearingPlot(world, town.Id, request.Tiles) ||
            !HouseholdLandGrantRules.IsAvailable(request, State.HouseholdLandUseRights, State.HouseholdLandUseRequests) ||
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
        foreach (var saved in State.HouseholdLandUseRequests.Where(r => r.TownId == town.Id && r.Status == "pending").ToArray())
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
                HouseholdLandGrantRules.IsAvailable(request, State.HouseholdLandUseRights, State.HouseholdLandUseRequests) &&
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
                    RestoreHouseholdLandUseRights([.. State.HouseholdLandUseRights, new(HouseholdLandGrantRules.RightId(request.Id), town.Id, request.HouseholdId,
                        request.Tiles, world.WorldTick, HouseholdLandGrantRules.GrantSource(request.Id), request.AgreedEndTick)]);
                    RestoreHouseholdLandUseRights(State.HouseholdLandUseRights.OrderBy(r => r.Id, StringComparer.Ordinal).ToList());
                    state = TownCouncilRules.LandUseNotice(state, request.Id,
                        "Household land use granted for " + TownLandClaimRules.DescribeTiles(request.Tiles) + ".", world.WorldTick);
                    world.AppendEvent(new LandUseGrantedEventPayload(null, request.Id, town.Id, request.HouseholdId, request.Tiles.Count));
                }
            }
            if (request != saved)
                RestoreHouseholdLandUseRequests(State.HouseholdLandUseRequests.Select(r => r.Id == request.Id ? request : r).ToList());
        }
        return state;
    }

    internal HashSet<GridPoint> HouseholdLandHeldByOthers(ITownWorld world, string? household) => State.HouseholdLandUseRights
            .Where(right => right.HouseholdId != household).SelectMany(right => right.Tiles)
            .Concat(State.HouseholdLandUseRequests.Where(request => request.Status == "pending" && request.HouseholdId != household)
                .SelectMany(TownLandRightsRules.UnresolvedRequestTiles))
            .ToHashSet();

    internal bool IncludesForeignFieldWithoutUseRight(ITownWorld world, string household, IReadOnlyList<GridPoint> tiles) =>
            world.Fields.Any(field => field.HouseholdId != household && tiles.Contains(field.Position) &&
                !State.HouseholdLandUseRights.Any(right => right.Tiles.Contains(field.Position)));

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
        return State.TownLandTitles.Where(title => title.TownId == town.Id).SelectMany(title => title.Tiles)
            .Where(tile => world.Map.IsLand(tile) && !taken.Contains(tile)).Distinct()
            .OrderBy(tile => world.Map.FootDistance(from, tile)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
            .Take(count).ToArray();
    }

    internal static string LandUseTerms(ITownWorld world, HouseholdLandUseRequest request) =>
            "Exact tiles: " + TownLandClaimRules.DescribeTiles(request.Tiles) + ". " +
            (request.AgreedEndTick is { } end ? $"Agreed end: world day {CivicDayNumber(world, end)}. " : "No agreed end date. ");

    internal void AddHouseholdLandCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        foreach (var building in world.WorldSimulation.Buildings.Where(building => building.TownId == town.Id && building.HouseholdId == world.HouseholdFor(actor)))
        {
            if (ExpansionLandRequestTiles(world, actor, building) is not { } extra) continue;
            var id = CivicAction(world, town.Id, "request_expansion_land", building.InstanceId, TownLandClaimRules.DescribeTiles(extra));
            var choice = HouseholdLandFilingChoice.Decode(id)!;
            var plan = new HouseholdLandFilingActionPlan(actor, choice, world.HouseholdFor(actor), extra, extra, building);
            if (CheckHouseholdLandFiling(world, plan).Allowed)
                candidates.Add(new(id, "Request household use of the extra land needed to expand your House. Council approval and every current adult's acceptance are required. Exact tiles: " +
                    TownLandClaimRules.DescribeTiles(extra) + ".", 190)
                { ActionPlan = plan });
        }
        var history = CivicHistory(town);
        foreach (var request in State.HouseholdLandUseRequests.Where(request => request.TownId == town.Id && request.Status == "pending" &&
                     history.Knows(actor, "land_use", request.Id)))
        {
            var terms = request.HearingResolutions.Count == 0 ? LandUseTerms(world, request) :
                "Remaining unheard requested tiles: " + TownLandClaimRules.DescribeTiles(TownLandRightsRules.UnresolvedRequestTiles(request)) +
                ". Completed hearing decisions remain. A new ordinary grant requires a separate request for its exact plot. ";
            OfferHouseholdLandResponse(world, candidates, actor, town, request, HouseholdLandResponseKind.Withdraw,
                "Withdraw your pending household land request. " + terms, 190);
            OfferHouseholdLandResponse(world, candidates, actor, town, request, HouseholdLandResponseKind.Accept,
                "Personally accept the requested use right for your household. Council approval and every current adult's acceptance are also required. " + terms, 165);
            OfferHouseholdLandResponse(world, candidates, actor, town, request, HouseholdLandResponseKind.Decline,
                "Personally decline the requested use right for your household. " + terms, 166);
        }
    }

    internal GridPoint[]? ExpansionLandRequestTiles(ITownWorld world, string actor, PlacedBuilding building)
    {
        if (building.HouseholdId is null || !world.MayExpandBuilding(actor, building, out _) ||
            world.ExpansionShapes(building).Any(shape => world.CanFitExpansion(building, shape.Position, shape.Footprint, out _))) return null;
        var definition = world.WorldContent.Buildings.Single(d => d.CanonicalId == building.DefinitionId);
        var pending = State.HouseholdLandUseRequests.Where(r => r.Status == "pending" && r.HouseholdId == building.HouseholdId)
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
                .Where(tile => !State.HouseholdLandUseRights.Any(right => right.HouseholdId == building.HouseholdId && right.Tiles.Contains(tile))));
            if (extra.Length == 0 || extra.Any(tile => !TownLandRightsRules.IsCoveredByTownTitle(tile, building.TownId!, State.TownLandTitles))) continue;
            // One expansion asks the Council once: a pending request for any of its shapes waits to be decided.
            if (extra.Any(pending.Contains)) return null;
            if (extra.Any(proposedHalls.Contains)) continue;
            if (extra.Any(heldByOthers.Contains)) disputed ??= extra;
            else offered ??= extra;
        }
        return offered ?? disputed;
    }

    internal TownCouncilState DecideHouseholdLandUse(ICivicLandConsentWorld world, TownCouncilState state, string actor, string requestId, string action)
    {
        var request = State.HouseholdLandUseRequests.Single(r => r.Id == requestId && r.Status == "pending");
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
        RestoreHouseholdLandUseRequests(State.HouseholdLandUseRequests.Select(r => r.Id == request.Id ? request : r).ToList());
        return state;
    }

    internal TownCouncilState SubmitPersonalLandUseRequest(ITownWorld world, TownRuntimeState town, TownCouncilState state,
            string actor, IReadOnlyList<CognitionLandTile>? landTiles, HouseholdLandFilingActionPlan? plan = null)
    {
        var selectedTiles = plan?.SelectedTiles ?? landTiles?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray();
        if (selectedTiles is not { Count: > 0 and <= CognitionDecisionResponse.MaximumCivicLandTiles })
            throw new InvalidOperationException("Choose exact land tiles for the household request.");
        var (result, updated) = FileHouseholdLandUse(world, TownRecordIds.LandRequest(town.Id, TownCouncilRules.NextNoticeSequence(state)),
            actor, town.Id, selectedTiles, null, state);
        if (!result.Applied) throw new InvalidOperationException(result.Failure);
        return updated;
    }
    internal HouseholdLandFilingActionPlan? PlanHouseholdLandFiling(ITownWorld world, string actor, HouseholdLandFilingChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            !world.Agents.TryGetValue(actor, out var person) || world.Society.GetAgent(actor).HouseholdId is not { } household) return null;
        if (choice.BuildingId is null) return new(actor, choice, household, RequestableLandNear(world, town, person.Position, 6), null, null);
        var building = world.WorldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == choice.BuildingId);
        return building is not null && ExpansionLandRequestTiles(world, actor, building) is { } tiles
            ? new(actor, choice, household, tiles, tiles, building) : null;
    }

    internal static HouseholdLandFilingActionPlan BindHouseholdLandFiling(HouseholdLandFilingActionPlan plan, IReadOnlyList<CognitionLandTile>? tiles) =>
        plan.SelectedTiles is not null ? plan : plan with { SelectedTiles = tiles?.Select(tile => new GridPoint(tile.X, tile.Y)).ToArray() ?? [] };

    internal ActionCheck CheckHouseholdLandFiling(ITownWorld world, HouseholdLandFilingActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            world.Society.GetAgent(plan.Actor).HouseholdId != plan.Household || !TownAdults(world, town).Contains(plan.Actor, StringComparer.Ordinal) ||
            plan.SelectedTiles is null && plan.OfferedTiles.Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household or requestable plot has changed");
        if (plan.Building is { } selected && (!world.WorldSimulation.Buildings.Contains(selected) ||
                selected.HouseholdId != plan.Household || !SelectedExpansionPlotAvailable(world, plan, selected) ||
                plan.Choice.PlotToken != CivicAgentToken(TownLandClaimRules.DescribeTiles(plan.SelectedTiles!))))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected House or expansion plot has changed");
        var tiles = plan.SelectedTiles;
        if (tiles is null) return new();
        var failure = HouseholdLandFilingFailure(world, TownRecordIds.LandRequest(town.Id, TownCouncilRules.NextNoticeSequence(town.Council)),
            plan.Actor, town.Id, tiles, null);
        return failure is null ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, failure);
    }

    private bool SelectedExpansionPlotAvailable(ITownWorld world, HouseholdLandFilingActionPlan plan, PlacedBuilding building)
    {
        if (plan.SelectedTiles is not { Count: > 0 } selected || ExpansionLandRequestTiles(world, plan.Actor, building) is null ||
            selected.Any(world.PendingTownProjectSiteTiles().Contains)) return false;
        var definition = world.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        var matching = false;
        foreach (var shape in world.ExpansionShapes(building))
        {
            if (!world.CanFitExpansion(building, shape.Position, shape.Footprint, out _, requireLandRights: false)) continue;
            var extra = TownLandRightsRules.OrderTiles(WorldContentSimulationRules.Footprint(
                BuildingStorageRules.WithSize(definition, shape.Footprint.Width, shape.Footprint.Height), shape.Position)
                .Except(WorldContentSimulationRules.Footprint(definition, building))
                .Where(tile => !State.HouseholdLandUseRights.Any(right => right.HouseholdId == plan.Household && right.Tiles.Contains(tile))));
            if (extra.SequenceEqual(selected)) matching = true;
        }
        return matching;
    }

    private void OfferPersonalLandFiling(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town, string siteRecovery)
    {
        if (PlanHouseholdLandFiling(world, actor, new(town.Id, null, "")) is not { } plan || !CheckHouseholdLandFiling(world, plan).Allowed) return;
        var here = world.Agents[actor].Position;
        candidates.Add(new(plan.CandidateId, siteRecovery + $"Ask {town.Name}'s council for household use of a connected plot of Town-titled land; include exact coordinates in civic_land_tiles. " +
            FormattableString.Invariant($"You stand at ({here.X}, {here.Y}); free Town land nearest you: {string.Join("; ", plan.OfferedTiles.Select(tile => FormattableString.Invariant($"({tile.X}, {tile.Y})")))}. ") +
            "Filing grants nothing and supplies no household acceptance.", 190)
        { ActionPlan = plan });
    }
    internal static HouseholdLandResponseActionPlan? PlanHouseholdLandResponse(ITownWorld world, string actor, HouseholdLandResponseChoice choice) =>
        world.HouseholdLandUseRequests.SingleOrDefault(request => request.Id == choice.RequestId) is { } request
            ? new(actor, choice, request with
            {
                Tiles = request.Tiles.ToArray(),
                Consents = request.Consents.ToArray(),
                GrantAdults = request.GrantAdults.ToArray(),
                HearingResolutions = request.HearingResolutions.ToArray()
            }) : null;

    internal static ActionCheck CheckHouseholdLandResponse(ITownWorld world, HouseholdLandResponseActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? GovernanceCapabilities);
        if (!capability.Allowed) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            world.HouseholdLandUseRequests.SingleOrDefault(request => request.Id == plan.Request.Id) is not { Status: "pending" } current ||
            current.TownId != town.Id || current with
            {
                CouncilProposalId = plan.Request.CouncilProposalId,
                Tiles = plan.Request.Tiles,
                Consents = plan.Request.Consents,
                GrantAdults = plan.Request.GrantAdults,
                HearingResolutions = plan.Request.HearingResolutions
            } != plan.Request ||
            !current.Tiles.SequenceEqual(plan.Request.Tiles) || !current.GrantAdults.SequenceEqual(plan.Request.GrantAdults) ||
            !current.HearingResolutions.SequenceEqual(plan.Request.HearingResolutions) || !CivicHistory(town).Knows(plan.Actor, "land_use", current.Id))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected household land request or exact terms have changed");
        var allowed = plan.Choice.Kind == HouseholdLandResponseKind.Withdraw ? current.RequestedByAgentId == plan.Actor :
            world.HouseholdAdults(current.HouseholdId).Contains(plan.Actor, StringComparer.Ordinal) &&
            !current.Consents.Any(consent => consent.AgentId == plan.Actor) &&
            (plan.Choice.Kind != HouseholdLandResponseKind.Accept || current.HearingResolutions.Count == 0);
        return allowed ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that personal land response is final or no longer eligible");
    }

    private static void OfferHouseholdLandResponse(ITownWorld world, List<CognitionCandidate> candidates, string actor,
        TownRuntimeState town, HouseholdLandUseRequest request, HouseholdLandResponseKind kind, string text, int priority)
    {
        if (PlanHouseholdLandResponse(world, actor, new(town.Id, kind, request.Id)) is { } plan && CheckHouseholdLandResponse(world, plan).Allowed)
            candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }

}
