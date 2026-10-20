namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    private ActionCheck CheckLandSaleTerms(ITownWorld world, ActionPlan plan, TownLandTransferRequest original,
        string townId, string token, GridPoint board, string household, TownRuntimeState? currentTown, out TownRuntimeState? town, out TownLandTransferRequest? current, ActorCapabilityPolicy? policy = null)
    {
        town = currentTown ?? world.Towns.SingleOrDefault(item => item.Id == townId);
        current = town?.LandRecords.Transfers.SingleOrDefault(request => request.Id == original.Id);
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? LandRightsCapabilities);
        if (!capability.Allowed) return capability;
        if (original.TownId != townId || town is not { Council: not null } || town.Id != townId || current is not { Status: "pending", Price: not null } ||
            current.FilerId != original.FilerId || current.ProposedTick != original.ProposedTick || current.NoticeId != original.NoticeId ||
            LandTransferActionToken(current) != token || TownLandTransferRules.TermsToken(current) != TownLandTransferRules.TermsToken(original) ||
            world.Society.GetAgent(plan.Actor).HouseholdId != household ||
            !LandTransferParties(world, current).Any(party => party.HouseholdId == household && party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal)) ||
            CivicBoard(world, town) != board || !TownLandTransferRules.HasNotice(current, plan.Actor, world.WorldTick, town.Council.Knowledge) ||
            TownLandTransferRules.PendingFailure(town.LandRecords, current, householdLandUseRights, householdLandUseRequests, world.WorldTick) is not null ||
            !TownLandTransferRules.HasAllConsents(current, LandTransferParties(world, current), town.Council.Knowledge, world.WorldTick))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected sale terms, household consent or notice place have changed");
        return new();
    }

    internal static LandSaleExchangeActionPlan? PlanLandSaleExchange(ITownWorld world, string actor, LandSaleExchangeChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => LandTransferActionToken(request) == choice.TermsToken) is not { Price: { } price } request ||
            CivicBoard(world, town) is not { } board) return null;
        var atBoard = NearCivicBoard(world, actor, town);
        var seller = choice.Kind == LandSaleExchangeKind.Pay && atBoard
            ? LandTransferParties(world, request).Single(party => party.HouseholdId == price.SellerHouseholdId).AdultIds
                .FirstOrDefault(person => NearCivicBoard(world, person, town)) : null;
        var goods = new List<LandSalePaymentStock>();
        if (choice.Kind == LandSaleExchangeKind.Pay)
        {
            var remaining = price.Quantity;
            foreach (var lot in LandSaleCarriedLots(world, actor, price))
            {
                var quantity = Math.Min(remaining, world.AvailableLotQuantity(lot));
                if (quantity > 0) goods.Add(new(lot.Id, quantity));
                remaining -= quantity;
                if (remaining == 0) break;
            }
        }
        return new(actor, choice, request, board, !atBoard ? LandSaleExchangeStage.Approach : seller is null
            ? LandSaleExchangeStage.Wait : LandSaleExchangeStage.Exchange, seller, goods.ToArray(), world.ReachLandSaleNoticePlace(actor, board));
    }

    internal ActionCheck CheckLandSaleExchange(ITownWorld world, LandSaleExchangeActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var terms = CheckLandSaleTerms(world, plan, plan.Request, plan.Choice.TownId, plan.Choice.TermsToken, plan.NoticePlace,
            plan.Choice.Kind == LandSaleExchangeKind.Meet ? plan.Request.Price!.SellerHouseholdId : plan.Request.TargetHouseholdId,
            currentTown, out var town, out var current, policy);
        if (!terms.Allowed) return terms;
        var atBoard = NearCivicBoard(world, plan.Actor, town!);
        if (world.ReachLandSaleNoticePlace(plan.Actor, plan.NoticePlace).Count == 0)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected notice place is out of reach now");
        if (plan.Choice.Kind == LandSaleExchangeKind.Meet)
            return !atBoard ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they are already at the selected meeting place");
        var price = current!.Price!;
        var carried = LandSaleCarriedLots(world, plan.Actor, price).ToDictionary(lot => lot.Id, StringComparer.Ordinal);
        if (plan.Goods.Count == 0 || plan.Goods.Select(goods => goods.LotId).Distinct(StringComparer.Ordinal).Count() != plan.Goods.Count ||
            plan.Goods.Sum(goods => goods.Quantity) != price.Quantity || LandSaleCarriedQuantity(world, plan.Actor, price) < price.Quantity ||
            plan.Goods.Any(goods => goods.Quantity <= 0 || !carried.TryGetValue(goods.LotId, out var lot) || world.AvailableLotQuantity(lot) < goods.Quantity))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected personally carried payment goods are no longer available");
        if (plan.Stage == LandSaleExchangeStage.Approach)
            return !atBoard ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they arrived and need a fresh personal payment choice");
        if (!atBoard) return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "they are no longer at the selected payment meeting");
        var sellers = LandTransferParties(world, current).Single(party => party.HouseholdId == price.SellerHouseholdId).AdultIds;
        if (plan.Stage == LandSaleExchangeStage.Wait)
            return !sellers.Any(person => NearCivicBoard(world, person, town!)) ? new()
                : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "a seller arrived and needs a fresh personal payment choice");
        return plan.Seller is { } seller && sellers.Contains(seller, StringComparer.Ordinal) && NearCivicBoard(world, seller, town!) ? new()
            : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected seller is no longer at the payment meeting");
    }
}
