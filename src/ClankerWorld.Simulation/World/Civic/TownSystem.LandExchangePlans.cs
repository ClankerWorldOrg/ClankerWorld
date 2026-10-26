using ClankerWorld.Simulation.Cognition;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal LandSaleCollectionActionPlan? PlanLandSaleCollection(ITownWorld world, string actor, LandSaleCollectionChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => LandTransferActionToken(request) == choice.TermsToken) is not { Price: { } price } request ||
            CivicBoard(world, town) is not { } board || LandSaleStock(world, actor, town, price) is not { } stock) return null;
        var quantity = Math.Min(price.Quantity - LandSaleCarriedQuantity(world, actor, price),
            Math.Min(LandSalePickupCapacity(world, actor, town, stock), world.MarketSurplus(actor, stock)));
        if (stock.ItemKind == "food") quantity = Math.Min(quantity, SharedFoodCollectionAllowance(world, actor));
        if (quantity <= 0) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            world.HouseholdStockPosition(stock), WorldRuntime.CivicSharedRules.HouseholdStockInteractionRange(stock));
        return new(actor, choice, request, board, pickup, quantity, world.ReachLandSalePickup(actor, pickup),
            world.ReachLandSaleReturn(actor, pickup, board, quantity));
    }

    internal ActionCheck CheckLandSaleCollection(ITownWorld world, LandSaleCollectionActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var terms = CheckLandSaleTerms(world, plan, plan.Request, plan.Choice.TownId, plan.Choice.TermsToken, plan.NoticePlace,
            plan.Request.TargetHouseholdId, currentTown, out _, out var current, policy);
        if (!terms.Allowed) return terms;
        var price = current!.Price!;
        var pickup = plan.Pickup;
        var goods = world.SpendingGoods(plan.Actor, GoodsUse.Collect, [current.TargetHouseholdId], price.ItemKind)
            .SingleOrDefault(match => match.Lot.Id == pickup.StockId);
        if (plan.Quantity <= 0 || price.Quantity - LandSaleCarriedQuantity(world, plan.Actor, price) < plan.Quantity ||
            goods is not { Lot: { CarrierId: null, ContainerLotId: null, DeliveryBuildingId: null } stock } || goods.Quantity < plan.Quantity ||
            stock.OwnerId != pickup.OwnerId || stock.ItemKind != pickup.ItemKind || stock.ItemKind == "tool" || ToolProgressionRules.Find(stock.ItemKind) is not null ||
            world.HouseholdStockPosition(stock) != pickup.Position || WorldRuntime.CivicSharedRules.HouseholdStockInteractionRange(stock) != pickup.InteractionRange ||
            world.MarketSurplus(plan.Actor, stock) < plan.Quantity ||
            stock.ItemKind == "food" && (!MayCollectSharedFood(world, plan.Actor) || SharedFoodCollectionAllowance(world, plan.Actor) < plan.Quantity))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected household payment goods or required quantity are no longer available");
        if (world.PickupCarryCapacity(plan.Actor, stock, plan.NoticePlace, WorldRuntime.CivicSharedRules.ResourceInteractionRange) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot carry the selected payment goods back to the notice place");
        return world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange) ||
            world.ReachLandSalePickup(plan.Actor, pickup).Count > 0 ? new()
                : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected payment goods are out of reach now");
    }
    private ActionCheck CheckLandSaleTerms(ITownWorld world, ActionPlan plan, TownLandTransferRequest original,
        string townId, string token, GridPoint board, string household, TownRuntimeState? currentTown, out TownRuntimeState? town, out TownLandTransferRequest? current, ActorCapabilityPolicy? policy = null)
    {
        town = currentTown ?? world.Towns.SingleOrDefault(item => item.Id == townId);
        current = town?.LandRecords.Transfers.SingleOrDefault(request => request.Id == original.Id);
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if (original.TownId != townId || town is not { Council: not null } || town.Id != townId || current is not { Status: "pending", Price: not null } ||
            current.FilerId != original.FilerId || current.ProposedTick != original.ProposedTick || current.NoticeId != original.NoticeId ||
            LandTransferActionToken(current) != token || TownLandTransferRules.TermsToken(current) != TownLandTransferRules.TermsToken(original) ||
            world.Society.GetAgent(plan.Actor).HouseholdId != household ||
            !LandTransferParties(world, current).Any(party => party.HouseholdId == household && party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal)) ||
            CivicBoard(world, town) != board || !TownLandTransferRules.HasNotice(current, plan.Actor, world.WorldTick, town.Council.Knowledge) ||
            TownLandTransferRules.PendingFailure(town.LandRecords, current, State.HouseholdLandUseRights, State.HouseholdLandUseRequests, world.WorldTick) is not null ||
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
    internal LandTransferProposalActionPlan? PlanLandTransferProposal(ITownWorld world, string actor, LandTransferProposalChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            CivicBoard(world, town) is not { } board || world.Society.GetAgent(actor).HouseholdId is not { } household) return null;
        var own = State.HouseholdLandUseRights.Where(right => right.TownId == town.Id && right.HouseholdId == household &&
            (right.AgreedEndTick is null || right.AgreedEndTick > world.WorldTick)).ToArray();
        var adults = LandTransferAdultHouseholds(world);
        var options = LandTransferKnownHouseholds(world, actor, town).Where(target => target.Id != household).Select(target =>
            new LandTransferBeneficiary(target.Id, target.Name, TownLandRightsRules.OrderTiles(own.SelectMany(right => right.Tiles).Distinct()
                .Where(tile => TownLandTransferRules.CanPropose(town.LandRecords, world.Map, town.Id, actor, [tile], target.Id,
                    State.HouseholdLandUseRights, TownLandTransferRules.PartiesFor(State.HouseholdLandUseRights, [tile], target.Id, adults),
                    State.HouseholdLandUseRequests, world.WorldTick))))).Where(option => option.Tiles.Count > 0).ToArray();
        return new(actor, choice, board, options);
    }

    internal static LandTransferProposalActionPlan BindLandTransferProposal(LandTransferProposalActionPlan plan,
        IReadOnlyList<CognitionLandTile>? tiles, CognitionLandHearingChoice? terms)
    {
        if (plan.Bound) return plan;
        var invalid = terms?.AgreedEndTick is not null || (plan.Choice.Sale
            ? terms?.PaymentItemKind is not { } kind || terms.PaymentQuantity is not > 0 || !LandSaleKind(kind)
            : terms?.PaymentItemKind is not null || terms?.PaymentQuantity is not null);
        return plan with
        {
            Bound = true,
            TargetHousehold = terms?.HouseholdId,
            Tiles = TownLandRightsRules.OrderTiles(tiles?.Select(tile => new GridPoint(tile.X, tile.Y)) ?? []),
            InvalidTerms = invalid,
            Price = plan.Choice.Sale && !invalid ? new(plan.Choice.SourceHousehold, terms!.PaymentItemKind!, terms.PaymentQuantity!.Value) : null
        };
    }

    internal ActionCheck CheckLandTransferProposal(ITownWorld world, LandTransferProposalActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            world.Society.GetAgent(plan.Actor).HouseholdId != plan.Choice.SourceHousehold ||
            CivicBoard(world, town) != plan.NoticePlace || !NearCivicBoard(world, plan.Actor, town) ||
            CivicAgentToken(LandTransferSourceToken(world, town, plan.Choice.SourceHousehold)) != plan.Choice.SourceToken)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected source household, permission terms or notice place have changed");
        var known = LandTransferKnownHouseholds(world, plan.Actor, town).Select(household => household.Id).ToHashSet(StringComparer.Ordinal);
        var adults = LandTransferAdultHouseholds(world);
        bool Available(IReadOnlyList<GridPoint> plot, string target) => TownLandTransferRules.CanPropose(town.LandRecords, world.Map, town.Id, plan.Actor,
            plot, target, State.HouseholdLandUseRights, TownLandTransferRules.PartiesFor(State.HouseholdLandUseRights, plot, target, adults), State.HouseholdLandUseRequests, world.WorldTick);
        if (!plan.Bound)
            return plan.Beneficiaries.Any(option => known.Contains(option.HouseholdId) && option.Tiles.Any(tile => Available([tile], option.HouseholdId)))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "none of the selected beneficiary plots remains available");
        return !plan.InvalidTerms && plan.TargetHousehold is { } target && target != plan.Choice.SourceHousehold && known.Contains(target) &&
            plan.Tiles is { Count: > 0 } selected && selected.All(tile => TownLandRightsRules.IsCoveredByTownTitle(tile, town.Id, State.TownLandTitles) &&
                State.HouseholdLandUseRights.Any(right => right.TownId == town.Id && right.HouseholdId == plan.Choice.SourceHousehold && right.Tiles.Contains(tile))) &&
            Available(selected, target) ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected beneficiary, exact plot or goods price is unavailable");
    }
    internal static LandTransferResponseActionPlan? PlanLandTransferResponse(ITownWorld world, string actor, LandTransferResponseChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => LandTransferActionToken(request) == choice.TermsToken) is not { } request ||
            LandTransferParties(world, request).SingleOrDefault(party => party.AdultIds.Contains(actor, StringComparer.Ordinal)) is not { } party) return null;
        return new(actor, choice, request, party.HouseholdId, choice.Kind == LandTransferResponseKind.Read ? CivicBoard(world, town) : null);
    }

    internal ActionCheck CheckLandTransferResponse(ITownWorld world, LandTransferResponseActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        if (CheckGovernanceCapabilities(world, plan, policy) is { Allowed: false } capability) return capability;
        if ((currentTown ?? world.Towns.SingleOrDefault(town => town.Id == plan.Choice.TownId)) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => request.Id == plan.Request.Id) is not { Status: "pending" } current ||
            LandTransferActionToken(current) != plan.Choice.TermsToken || current.FilerId != plan.Request.FilerId ||
            current.ProposedTick != plan.Request.ProposedTick || current.NoticeId != plan.Request.NoticeId ||
            !TownLandTransferRules.TermsToken(current).Equals(TownLandTransferRules.TermsToken(plan.Request), StringComparison.Ordinal) ||
            LandTransferParties(world, current).SingleOrDefault(party => party.HouseholdId == plan.PartyHousehold &&
                party.AdultIds.Contains(plan.Actor, StringComparer.Ordinal)) is not { } party)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected permission request, terms or household party have changed");
        var knows = TownLandTransferRules.HasNotice(current, plan.Actor, world.WorldTick, town.Council.Knowledge);
        if (plan.Choice.Kind == LandTransferResponseKind.Read)
            return !knows && CivicBoard(world, town) == plan.NoticePlace && NearCivicBoard(world, plan.Actor, town)
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected transfer notice is unavailable or already learned");
        if (!knows) return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "they have not learned those selected permission terms");
        if (plan.Choice.Kind == LandTransferResponseKind.Withdraw)
            return current.FilerId == plan.Actor ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "only the actual filer can withdraw those terms");
        var accepted = TownLandTransferRules.AcceptedAdults(current, party, town.Council.Knowledge, world.WorldTick).Contains(plan.Actor, StringComparer.Ordinal);
        return plan.Choice.PartyToken == CivicAgentToken(party.HouseholdId) &&
            TownLandTransferRules.PendingFailure(town.LandRecords, current, State.HouseholdLandUseRights, State.HouseholdLandUseRequests, world.WorldTick) is null &&
            (plan.Choice.Kind == LandTransferResponseKind.Decline || !accepted)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "that selected personal permission response is no longer eligible");
    }

    private void OfferLandTransferResponse(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
        TownLandTransferRequest request, LandTransferResponseKind kind, string party, string text, int priority)
    {
        var id = CivicAction(world, town.Id, LandTransferResponseChoice.Verb(kind), LandTransferActionToken(request), party);
        if (LandTransferResponseChoice.Decode(id) is { } choice && PlanLandTransferResponse(world, actor, choice) is { } plan && CheckLandTransferResponse(world, plan).Allowed)
            candidates.Add(new(id, text, priority) { ActionPlan = plan });
    }
}
