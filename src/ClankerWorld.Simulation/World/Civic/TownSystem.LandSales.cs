using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal static bool LandSaleKind(string kind) => BoundedText.IsValid(kind, ContentPackageRules.MaximumContentIdLength) &&
            !kind.Any(char.IsWhiteSpace) && !InventoryContainerRules.IsContainer(kind) && kind != "handcart";

    internal static InventoryLot[] LandSaleCarriedLots(ITownWorld world, string actor, TownLandSalePrice price) =>
        world.SpendingGoods(actor, GoodsUse.Spend, [actor], price.ItemKind).Select(match => match.Lot)
            .Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) &&
                !PersonalEquipmentRules.IsSelected(world.Agents[actor].Equipment, lot.Id))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray();

    internal static int LandSaleCarriedQuantity(ITownWorld world, string actor, TownLandSalePrice price)
    {
        var available = LandSaleCarriedLots(world, actor, price).Sum(world.AvailableLotQuantity);
        // Keep the food reserve once across all payment stacks, rather than
        // letting each stack count the others as the same retained reserve.
        var reserve = WorldRuntime.CivicSharedRules.IsEdibleFood(price.ItemKind)
            ? Math.Max(2, world.CaregiverFoodCarryRequirement(actor, world.Agents[actor])) : 0;
        return Math.Max(0, available - reserve);
    }

    internal InventoryLot? LandSaleStock(ITownWorld world, string actor, TownRuntimeState town, TownLandSalePrice price) =>
            CivicBoard(world, town) is { } board && world.SharedItem(price.ItemKind, actor, board, WorldRuntime.CivicSharedRules.ResourceInteractionRange) is { } lot &&
            lot.OwnerId == world.Society.GetAgent(actor).HouseholdId && lot.DeliveryBuildingId is null &&
            lot.ItemKind != "tool" && ToolProgressionRules.Find(lot.ItemKind) is null && world.MarketSurplus(actor, lot) > 0 &&
            (lot.ItemKind != "food" || MayCollectSharedFood(world, actor)) ? lot : null;

    internal static int LandSalePickupCapacity(ITownWorld world, string actor, TownRuntimeState town, InventoryLot stock) =>
            CivicBoard(world, town) is { } board ? world.PickupCarryCapacity(actor, stock, board, WorldRuntime.CivicSharedRules.ResourceInteractionRange) : 0;

    internal void AddTownLandSaleCandidates(ITownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
            TownLandTransferRequest request, IReadOnlyList<TownLandTransferParty> parties, string token)
    {
        if (request.Price is not { } price || town.Council is not { } council ||
            !TownLandTransferRules.HasAllConsents(request, parties, council.Knowledge, world.WorldTick)) return;
        var household = world.Society.GetAgent(actor).HouseholdId;
        if (household == request.TargetHouseholdId)
        {
            if (PlanLandSaleCollection(world, actor, new(town.Id, token)) is { } collection && CheckLandSaleCollection(world, collection).Allowed)
                candidates.Add(new(collection.CandidateId,
                    "Collect available household goods you may carry for this accepted land-use sale. " + LandTransferTerms(world, request), 175)
                { ActionPlan = collection });
            if (PlanLandSaleExchange(world, actor, new(town.Id, token, LandSaleExchangeKind.Pay)) is { } payment && CheckLandSaleExchange(world, payment).Allowed)
                candidates.Add(new(payment.CandidateId,
                    "Bring the exact personally owned goods to the notice place; pay the seller household only when an accepted seller adult is present, and transfer the recorded permission together. " + LandTransferTerms(world, request), 180)
                { ActionPlan = payment });
        }
        else if (PlanLandSaleExchange(world, actor, new(town.Id, token, LandSaleExchangeKind.Meet)) is { } meeting && CheckLandSaleExchange(world, meeting).Allowed)
            candidates.Add(new(meeting.CandidateId,
                "Meet the buyer at the notice place for the accepted goods sale. Walking supplies no payment or permission change. " + LandTransferTerms(world, request), 175)
            { ActionPlan = meeting });
    }

    /// <summary>Only the chosen physical trip continues locally; arrival needs a fresh personal choice.</summary>
    internal void ContinueTownLandSaleWalk(ITownWorld world, string actor, string candidate)
    {
        var parts = candidate.Split('|');
        if (parts.Length != 5 || world.NeedsUrgentWarmth(world.Agents[actor])) return;
        var town = State.Towns.SingleOrDefault(item => item.Id == parts[1]);
        if (town is null) return;
        var candidates = new List<CognitionCandidate>();
        AddTownLandTransferCandidates(world, candidates, actor, town);
        var offered = candidates.SingleOrDefault(item => item.Id == candidate);
        if (offered is null) return;
        if (offered.ActionPlan is LandSaleCollectionActionPlan collection)
        {
            if (!world.IsWithinInteractionRange(world.Agents[actor].Position, collection.Pickup.Position, collection.Pickup.InteractionRange))
                world.MoveToward(actor, world.Agents[actor], collection.Pickup.Position, "land_sale", collection.Pickup.InteractionRange);
            return;
        }
        if (offered.ActionPlan is LandSaleExchangeActionPlan exchange &&
            !world.IsWithinInteractionRange(world.Agents[actor].Position, exchange.NoticePlace, WorldRuntime.CivicSharedRules.ResourceInteractionRange))
            world.MoveToward(actor, world.Agents[actor], exchange.NoticePlace, "land_sale", WorldRuntime.CivicSharedRules.ResourceInteractionRange);
    }

    internal void CollectLandSalePayment(ITownWorld world, string actor, TownRuntimeState town, TownLandTransferRequest request,
        LandSaleCollectionActionPlan? plan = null)
    {
        plan ??= PlanLandSaleCollection(world, actor, new(town.Id, LandTransferActionToken(request)));
        if (plan is null) return;
        if (CheckLandSaleCollection(world, plan, town).Refusal is { } refusal) { world.RefuseChoice(plan, refusal); return; }
        var pickup = plan.Pickup;
        if (!world.IsWithinInteractionRange(world.Agents[actor].Position, pickup.Position, pickup.InteractionRange))
        {
            world.MoveToward(actor, world.Agents[actor], pickup.Position, "land_sale_payment", pickup.InteractionRange);
            return;
        }
        world.RunInventoryCommand(InventorySystem.CollectLandSalePayment(world, actor, request, pickup, plan));
    }

    internal (TownCouncilState Council, TownLandRecords LandRecords) SettleLandSale(ITownWorld world, TownRuntimeState town,
            TownCouncilState council, TownLandTransferRequest request, string buyer, LandSaleExchangeActionPlan? plan = null)
    {
        plan ??= PlanLandSaleExchange(world, buyer, new(town.Id, LandTransferActionToken(request), LandSaleExchangeKind.Pay));
        if (plan is null) return (council, town.LandRecords);
        if (CheckLandSaleExchange(world, plan, town).Refusal is { } refusal)
        { world.RefuseChoice(plan, refusal); return (council, town.LandRecords); }
        var board = plan.NoticePlace;
        if (plan.Stage == LandSaleExchangeStage.Approach)
        {
            world.MoveToward(buyer, world.Agents[buyer], board, "land_sale_payment", WorldRuntime.CivicSharedRules.ResourceInteractionRange);
            return (council, town.LandRecords);
        }
        if (plan.Stage == LandSaleExchangeStage.Wait) return (council, town.LandRecords);
        var price = request.Price!;
        var seller = plan.Seller!;
        var inventory = world.Society.Inventory;
        var paymentLots = new List<TownLandSalePaymentLot>();
        foreach (var goods in plan.Goods)
        {
            inventory = InventoryAuthority.Transfer(inventory, TownLandTransferRules.PaymentTransferId(request, buyer, paymentLots.Count),
                buyer, price.SellerHouseholdId, goods.LotId, goods.Quantity, "land_use_right_payment",
                destinationGroundPosition: new(board.X, board.Y));
            paymentLots.Add(new(goods.LotId, goods.Quantity, inventory.Events[^1].EventId));
        }
        var payment = new TownLandSalePayment(buyer, seller, board, paymentLots.ToArray());
        var (hearings, rights) = TownLandTransferRules.Advance(town.LandRecords, world.Map, State.HouseholdLandUseRights,
            town.LandRecords.Transfers.Where(item => item.Status == "pending").ToDictionary(item => item.Id, item => LandTransferParties(world, item), StringComparer.Ordinal),
            State.HouseholdLandUseRequests, council.Knowledge, world.WorldTick, new Dictionary<string, TownLandSalePayment> { [request.Id] = payment });
        if (hearings.Transfers.Single(item => item.Id == request.Id).Status != "transferred")
            throw new InvalidOperationException("The accepted sale no longer has valid permission terms.");
        // Both immutable plans are complete before publishing either side of the exchange.
        world.RunInventoryCommand(InventorySystem.SettleLandSale(inventory));
        RestoreHouseholdLandUseRights(rights.ToList());
        council = PublishLandTransferResults(world, town, town.LandRecords, hearings, council, request.Id, buyer);
        return (council, hearings);
    }

}
