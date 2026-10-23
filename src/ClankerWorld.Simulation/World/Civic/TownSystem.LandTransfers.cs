using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static Dictionary<string, string?> LandTransferAdultHouseholds(ITownWorld world) => world.Society.Agents
            .Where(person => person.Status == SocietyAgentStatus.Active && LifeStageRules.IsAdult(person.AgeBand))
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
            TownLandHearingRules.RecordVersion(State.HouseholdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household)
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
            world.AppendEvent(WorldEvents.LandTransfer(kind, actor, town.Id, request.Id, status), CivicBoard(world, town));

    internal (TownCouncilState Council, TownLandRecords LandRecords) AdvanceTownLandTransfers(
    ITownWorld world, TownRuntimeState town, TownCouncilState council)
    {
        var before = town.LandRecords;
        var parties = before.Transfers.Where(request => request.Status == "pending")
            .ToDictionary(request => request.Id, item => LandTransferParties(world, item), StringComparer.Ordinal);
        var (hearings, rights) = TownLandTransferRules.Advance(before, world.Map, State.HouseholdLandUseRights,
            parties, State.HouseholdLandUseRequests, council.Knowledge, world.WorldTick);
        RestoreHouseholdLandUseRights(rights.ToList());
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
            var sourceToken = CivicAgentToken(LandTransferSourceToken(world, town, household));
            var proposalChoice = new LandTransferProposalChoice(town.Id, household, sourceToken, false);
            if (PlanLandTransferProposal(world, actor, proposalChoice) is { } proposalPlan && CheckLandTransferProposal(world, proposalPlan).Allowed)
            {
                var eligible = proposalPlan.Beneficiaries;
                candidates.Add(new(proposalPlan.CandidateId,
                    "Propose transferring only an exact connected plot of existing household permission. Supply exact civic_land_tiles and civic_land_hearing.household_id from these known beneficiary households: " +
                    string.Join("; ", eligible.Select(option => option.Name + "=" + option.HouseholdId)) +
                    ". Eligible source coordinates by beneficiary: " + string.Join("; ", eligible.Select(option => option.HouseholdId + ": " + TownLandClaimRules.DescribeTiles(option.Tiles))) +
                    ". Existing terms remain; every current adult in each source and beneficiary household must separately learn and accept. Proposing gives no consent, title, building, goods or membership.", 185)
                { ActionPlan = proposalPlan });
                var salePlan = new LandTransferProposalActionPlan(actor, proposalChoice with { Sale = true }, proposalPlan.NoticePlace, proposalPlan.Beneficiaries);
                if (CheckLandTransferProposal(world, salePlan).Allowed)
                    candidates.Add(new(salePlan.CandidateId,
                        "Offer an exact existing connected permission plot for goods. Supply civic_land_tiles, civic_land_hearing.household_id, payment_item_kind and positive payment_quantity. Known buyers: " +
                        string.Join("; ", eligible.Select(option => option.Name + "=" + option.HouseholdId + ": " + TownLandClaimRules.DescribeTiles(option.Tiles))) +
                        ". Every affected adult must separately accept this exact goods price; physical payment at the notice place precedes the rights change. No money price or title sale.", 185)
                    { ActionPlan = salePlan });
            }
        }
        foreach (var request in town.LandRecords.Transfers.Where(request => request.Status == "pending"))
        {
            var parties = LandTransferParties(world, request);
            var party = parties.SingleOrDefault(item => item.AdultIds.Contains(actor, StringComparer.Ordinal));
            if (party is null) continue;
            var token = LandTransferActionToken(request);
            var knows = TownLandTransferRules.HasNotice(request, actor, world.WorldTick, council.Knowledge);
            OfferLandTransferResponse(world, candidates, actor, town, request, LandTransferResponseKind.Read, "",
                "Read the actual published household transfer notice. " + LandTransferTerms(world, request), 155);
            if (!knows) continue;
            OfferLandTransferResponse(world, candidates, actor, town, request, LandTransferResponseKind.Withdraw, "",
                "Withdraw your pending permission transfer proposal. Existing rights remain. " + LandTransferTerms(world, request), 190);
            if (TownLandTransferRules.PendingFailure(town.LandRecords, request, State.HouseholdLandUseRights, State.HouseholdLandUseRequests, world.WorldTick) is not null) continue;
            AddTownLandSaleCandidates(world, candidates, actor, town, request, parties, token);
            var accepted = TownLandTransferRules.AcceptedAdults(request, party, council.Knowledge, world.WorldTick).Contains(actor, StringComparer.Ordinal);
            OfferLandTransferResponse(world, candidates, actor, town, request, LandTransferResponseKind.Accept, party.HouseholdId,
                "Personally accept these exact permission terms for your current household; no other adult's consent is supplied. " + LandTransferTerms(world, request), 165);
            OfferLandTransferResponse(world, candidates, actor, town, request, LandTransferResponseKind.Decline, party.HouseholdId,
                (accepted ? "Revoke your acceptance and decline" : "Personally decline") + " this pending permission transfer. Silence and another member's choice give no agreement. " + LandTransferTerms(world, request), 166);
        }
    }

}
