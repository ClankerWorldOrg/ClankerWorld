using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static Dictionary<string, string?> LandTransferAdultHouseholds(ITownWorld world) => world.Society.Agents
            .Where(person => person.Status == SocietyAgentStatus.Active && person.AgeBand is SocietyAgeBand.Adult or SocietyAgeBand.Elder)
            .ToDictionary(person => person.Id, person => person.HouseholdId, StringComparer.Ordinal);

    internal static IReadOnlyList<TownLandTransferParty> LandTransferParties(ITownWorld world, TownLandTransferRequest request) =>
            TownLandTransferRules.PartiesFor(request.RightVersions.Select(version => version.Right), request.Tiles,
                request.TargetHouseholdId, LandTransferAdultHouseholds(world));

    internal static SocietyHousehold[] LandTransferKnownHouseholds(ITownWorld world, string actor, TownRuntimeState town)
    {
        var people = TownAdults(world, town).Concat(world.Agents.Values.Where(person =>
            world.IsWithinInteractionRange(person.Position, world.Agents[actor].Position, WorldRuntime.CivicSharedRules.ResourceInteractionRange))
            .Select(person => person.AgentId)).ToHashSet(StringComparer.Ordinal);
        var households = LandTransferAdultHouseholds(world).Where(pair => people.Contains(pair.Key) && pair.Value is not null)
            .Select(pair => pair.Value!).ToHashSet(StringComparer.Ordinal);
        return world.Society.Households.Where(household => households.Contains(household.Id))
            .OrderBy(household => household.Id, StringComparer.Ordinal).ToArray();
    }

    internal string LandTransferSourceToken(ITownWorld world, TownRuntimeState town, string household) => "rights:" +
            TownLandHearingRules.RecordVersion(householdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household)
                .OrderBy(right => right.Id, StringComparer.Ordinal).ToArray());

    internal static string LandTransferActionToken(TownLandTransferRequest request) => CivicAgentToken(TownLandTransferRules.TermsToken(request));

    internal static string LandTransferTerms(ITownWorld world, TownLandTransferRequest request) =>
            "Exact plot: " + TownLandClaimRules.DescribeTiles(request.Tiles) + ". Beneficiary household: " +
            world.Society.GetHousehold(request.TargetHouseholdId).Name + " (" + request.TargetHouseholdId + "). Source permissions: " +
            string.Join("; ", request.RightVersions.Select(version => world.Society.GetHousehold(version.Right.HouseholdId).Name +
                " (" + version.Right.HouseholdId + "), " + (version.Right.AgreedEndTick is { } end
                    ? "same agreed end on world day " + CivicDayNumber(world, end) : "no agreed end date"))) +
            (request.Price is { } price ? ". Goods price: " + price.Quantity + " " + price.ItemKind + " paid to " + price.SellerHouseholdId +
                " at the public notice place before permission changes. " : ". No payment. ") +
            "Title, household membership, private buildings, crops and other goods keep their current records and owners.";

    internal static void LandTransferEvent(ITownWorld world, string kind, TownRuntimeState town, TownLandTransferRequest request, string? actor, string status) =>
            world.AppendEvent("land_transfer_" + kind, $"{town.Id}|{request.Id}|1|{actor ?? ""}|{status}", CivicBoard(world, town));

    internal (TownCouncilState Council, TownLandRecords LandRecords) AdvanceTownLandTransfers(
    ITownWorld world, TownRuntimeState town, TownCouncilState council)
    {
        var before = town.LandRecords;
        var parties = before.Transfers.Where(request => request.Status == "pending")
            .ToDictionary(request => request.Id, item => LandTransferParties(world, item), StringComparer.Ordinal);
        var (hearings, rights) = TownLandTransferRules.Advance(before, world.Map, householdLandUseRights,
            parties, householdLandUseRequests, council.Knowledge, world.WorldTick);
        householdLandUseRights = rights.ToList();
        return (PublishLandTransferResults(world, town, before, hearings, council), hearings);
    }

    internal static TownCouncilState PublishLandTransferResults(ITownWorld world, TownRuntimeState town, TownLandRecords before,
            TownLandRecords after, TownCouncilState council, string? paidRequestId = null, string? buyer = null)
    {
        foreach (var request in after.Transfers.Where(request => request.Status != "pending" &&
                     before.Transfers.Any(old => old.Id == request.Id && old.Status == "pending")))
        {
            var paid = request.Id == paidRequestId;
            var text = paid ? "Goods paid and voluntary household permission transfer completed. " :
                request.Status == "transferred" ? "Voluntary household permission transfer completed. " :
                "Voluntary household permission transfer stopped: " + LandTransferFailureText(request.Reason) + ". ";
            council = TownCouncilRules.PostNotice(council, "result", request.Id, text + LandTransferTerms(world, request), world.WorldTick);
            LandTransferEvent(world, request.Status == "transferred" ? "settled" : "blocked", town, request, paid ? buyer : null, request.Status);
        }
        return council;
    }

    internal static string LandTransferFailureText(string? reason) => reason switch
    {
        "rights_changed" => "the source permission changed",
        "permission_expired" => "the agreed permission term ended",
        "plot_disputed" => "the plot is disputed",
        "hearing_open" => "the plot has an open hearing or reopening request",
        "adult_declined" => "an adult household member declined",
        "filer_withdrew" => "the proposer withdrew",
        _ => "the recorded agreement is no longer valid"
    };

    internal static bool MayVisitLandTransfer(ITownWorld world, string actor, TownRuntimeState town) => LandHearingAdult(world, actor) &&
            town.Council is { } council && CanWalkToCivicBoard(world, actor, town) &&
            town.LandRecords.Transfers.Any(request => request.Status == "pending" &&
                LandTransferParties(world, request).Any(party => party.AdultIds.Contains(actor, StringComparer.Ordinal)) &&
                TownLandTransferRules.HasNotice(request, actor, world.WorldTick, council.Knowledge));

    internal void AddTownLandTransferCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town)
    {
        if (!LandHearingAdult(world, actor) || town.Council is not { } council) return;
        var household = world.Society.GetAgent(actor).HouseholdId;
        var atBoard = NearCivicBoard(world, actor, town);
        if (!atBoard && MayVisitLandTransfer(world, actor, town) && !TownAdults(world, town).Contains(actor, StringComparer.Ordinal) &&
            !MayVisitAsNewcomer(world, actor, town) && !MayVisitLandHearing(world, actor, town))
            candidates.Add(new(CivicAction(world, town.Id, "visit"), "Walk to the public notice place to read the household permission transfer you actually learned concerns you. Visiting changes no property or membership.", 175));
        if (atBoard && household is not null)
        {
            var known = LandTransferKnownHouseholds(world, actor, town).Where(target => target.Id != household).ToArray();
            var own = householdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household &&
                (right.AgreedEndTick is null || right.AgreedEndTick > world.WorldTick)).OrderBy(right => right.Id, StringComparer.Ordinal).ToArray();
            var adultHouseholds = LandTransferAdultHouseholds(world);
            var eligible = known.Select(target => (Target: target, Tiles: TownLandRightsRules.OrderTiles(own.SelectMany(right => right.Tiles).Distinct()
                .Where(tile => TownLandTransferRules.CanPropose(town.LandRecords, world.Map, town.Id, actor, [tile], target.Id,
                    householdLandUseRights, TownLandTransferRules.PartiesFor(householdLandUseRights, [tile], target.Id, adultHouseholds),
                    householdLandUseRequests, world.WorldTick))))).Where(option => option.Tiles.Length > 0).ToArray();
            if (eligible.Length > 0)
            {
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_propose", household, LandTransferSourceToken(world, town, household)),
                    "Propose transferring only an exact connected plot of existing household permission. Supply exact civic_land_tiles and civic_land_hearing.household_id from these known beneficiary households: " +
                    string.Join("; ", eligible.Select(option => option.Target.Name + "=" + option.Target.Id)) +
                    ". Eligible source coordinates by beneficiary: " + string.Join("; ", eligible.Select(option => option.Target.Id + ": " + TownLandClaimRules.DescribeTiles(option.Tiles))) +
                    ". Existing terms remain; every current adult in each source and beneficiary household must separately learn and accept. Proposing gives no consent, title, building, goods or membership.", 185));
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_sell", household, LandTransferSourceToken(world, town, household)),
                    "Offer an exact existing connected permission plot for goods. Supply civic_land_tiles, civic_land_hearing.household_id, payment_item_kind and positive payment_quantity. Known buyers: " +
                    string.Join("; ", eligible.Select(option => option.Target.Name + "=" + option.Target.Id + ": " + TownLandClaimRules.DescribeTiles(option.Tiles))) +
                    ". Every affected adult must separately accept this exact goods price; physical payment at the notice place precedes the rights change. No money price or title sale.", 185));
            }
        }
        foreach (var request in town.LandRecords.Transfers.Where(request => request.Status == "pending"))
        {
            var parties = LandTransferParties(world, request);
            var party = parties.SingleOrDefault(item => item.AdultIds.Contains(actor, StringComparer.Ordinal));
            if (party is null) continue;
            var token = LandTransferActionToken(request);
            var knows = TownLandTransferRules.HasNotice(request, actor, world.WorldTick, council.Knowledge);
            if (atBoard && !knows)
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_read", token), "Read the actual published household transfer notice. " + LandTransferTerms(world, request), 155));
            if (!knows) continue;
            if (request.FilerId == actor)
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_withdraw", token), "Withdraw your pending permission transfer proposal. Existing rights remain. " + LandTransferTerms(world, request), 190));
            if (TownLandTransferRules.PendingFailure(town.LandRecords, request, householdLandUseRights, householdLandUseRequests, world.WorldTick) is not null) continue;
            AddTownLandSaleCandidates(world, candidates, actor, town, request, parties, token);
            var accepted = TownLandTransferRules.AcceptedAdults(request, party, council.Knowledge, world.WorldTick).Contains(actor, StringComparer.Ordinal);
            if (!accepted)
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_accept", token, party.HouseholdId), "Personally accept these exact permission terms for your current household; no other adult's consent is supplied. " + LandTransferTerms(world, request), 165));
            candidates.Add(new(CivicAction(world, town.Id, "land_transfer_decline", token, party.HouseholdId),
                (accepted ? "Revoke your acceptance and decline" : "Personally decline") + " this pending permission transfer. Silence and another member's choice give no agreement. " + LandTransferTerms(world, request), 166));
        }
    }

}
