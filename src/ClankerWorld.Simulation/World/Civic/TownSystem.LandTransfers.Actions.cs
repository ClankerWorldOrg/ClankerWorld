using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal (TownCouncilState Council, TownLandRecords LandRecords) ApplyTownLandTransferAction(
    ITownWorld world, TownRuntimeState town, string actor, string action, string subject, string choice,
            IReadOnlyList<CognitionLandTile>? landTiles, CognitionLandHearingChoice? hearingChoice, TownCouncilState council, LandTransferProposalActionPlan? proposalPlan = null, LandTransferResponseActionPlan? responsePlan = null, LandSaleCollectionActionPlan? collectionPlan = null)
    {
        (council, var hearings) = AdvanceTownLandTransfers(world, town with { Council = council }, council);
        var currentTown = town with { Council = council, LandRecords = hearings };
        // Maintenance is independent of the requested act. Its rights, receipts and notices
        // must stay together even when current-action validation rejects the stale request below.
        SetTown(world, currentTown);
        if (action is "land_transfer_propose" or "land_transfer_sell")
        {
            proposalPlan ??= LandTransferProposalChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } selected
                ? PlanLandTransferProposal(world, actor, selected) : null;
            if (proposalPlan is null) return (council, hearings);
            proposalPlan = BindLandTransferProposal(proposalPlan, landTiles, hearingChoice);
            if (CheckLandTransferProposal(world, proposalPlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(proposalPlan, refusal); return (council, hearings); }
            var target = proposalPlan.TargetHousehold!;
            var tiles = proposalPlan.Tiles!;
            var price = proposalPlan.Price;
            var before = hearings.Transfers.Count;
            hearings = TownLandTransferRules.Propose(hearings, world.Map, town.Id, actor, tiles, target, householdLandUseRights,
                TownLandTransferRules.PartiesFor(householdLandUseRights, tiles, target, LandTransferAdultHouseholds(world)),
                householdLandUseRequests, world.WorldTick, "notice:" + (council.Notices.Count + 1), price);
            if (hearings.Transfers.Count > before)
            {
                var proposed = hearings.Transfers[^1];
                council = TownCouncilRules.PostNotice(council, "land_transfer", TownLandTransferRules.TermsToken(proposed),
                    "Proposed voluntary household permission transfer. " + LandTransferTerms(world, proposed) +
                    " Every current adult in each affected household must personally agree; filing supplies no consent.", world.WorldTick);
                LandTransferEvent(world, "proposed", currentTown, proposed, actor, "pending");
            }
            return (council, hearings);
        }
        TownLandTransferRequest request;
        if (LandTransferResponseChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } responseChoice)
        {
            responsePlan ??= PlanLandTransferResponse(world, actor, responseChoice);
            if (responsePlan is null) return (council, hearings);
            if (CheckLandTransferResponse(world, responsePlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(responsePlan, refusal); return (council, hearings); }
            request = hearings.Transfers.Single(item => item.Id == responsePlan.Request.Id);
        }
        else if (LandSaleCollectionChoice.Decode($"civic|{town.Id}|{action}|{subject}|{choice}") is { } collectionChoice)
        {
            collectionPlan ??= PlanLandSaleCollection(world, actor, collectionChoice);
            if (collectionPlan is null) return (council, hearings);
            if (CheckLandSaleCollection(world, collectionPlan, currentTown).Refusal is { } refusal)
            { world.RefuseChoice(collectionPlan, refusal); return (council, hearings); }
            request = hearings.Transfers.Single(item => item.Id == collectionPlan.Request.Id);
        }
        else
        {
            var candidates = new List<CognitionCandidate>();
            AddTownLandTransferCandidates(world, candidates, actor, currentTown);
            if (!candidates.Any(candidate => candidate.Id == $"civic|{town.Id}|{action}|{subject}|{choice}"))
                throw new InvalidOperationException("The transfer choice no longer has current source terms or informed household eligibility.");
            request = hearings.Transfers.Single(item => LandTransferActionToken(item) == subject && item.Status == "pending");
        }
        switch (action)
        {
            case "land_transfer_collect_payment":
                CollectLandSalePayment(world, actor, currentTown, request, collectionPlan);
                break;
            case "land_transfer_meet":
                world.MoveToward(actor, world.Agents[actor], CivicBoard(world, currentTown)!.Value, "land_sale", WorldRuntime.CivicSharedRules.ResourceInteractionRange);
                break;
            case "land_transfer_pay":
                return SettleLandSale(world, currentTown, council, request, actor);
            case "land_transfer_read":
                council = TownCouncilRules.LearnNotice(council, actor, request.NoticeId, world.WorldTick);
                LandTransferEvent(world, "read", currentTown, request, actor, "learned");
                break;
            case "land_transfer_accept":
            case "land_transfer_decline":
                var parties = LandTransferParties(world, request);
                var party = parties.Single(item => item.HouseholdId == responsePlan!.PartyHousehold && item.AdultIds.Contains(actor, StringComparer.Ordinal));
                var response = action == "land_transfer_accept" ? "accept" : "decline";
                hearings = TownLandTransferRules.Respond(hearings, request.Id, TownLandTransferRules.TermsToken(request),
                    actor, party.HouseholdId, response, council.Knowledge, householdLandUseRights, parties, householdLandUseRequests, world.WorldTick);
                LandTransferEvent(world, "consent", currentTown, request, actor, response);
                if (response == "decline")
                    council = TownCouncilRules.PostNotice(council, "result", request.Id,
                        "Voluntary household permission transfer declined. Existing permissions remain. " + LandTransferTerms(world, request), world.WorldTick);
                break;
            case "land_transfer_withdraw":
                hearings = TownLandTransferRules.Withdraw(hearings, request.Id, actor, world.WorldTick);
                council = TownCouncilRules.PostNotice(council, "result", request.Id,
                    "Voluntary household permission transfer withdrawn by its proposer. Existing permissions remain. " + LandTransferTerms(world, request), world.WorldTick);
                LandTransferEvent(world, "withdrawn", currentTown, request, actor, "withdrawn");
                break;
            default: throw new InvalidOperationException("Unsupported household permission transfer action.");
        }
        return AdvanceTownLandTransfers(world, currentTown with { Council = council, LandRecords = hearings }, council);
    }

}
