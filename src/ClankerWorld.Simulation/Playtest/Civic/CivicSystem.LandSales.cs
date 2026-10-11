using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Content;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class CivicSystem
{
    internal static bool LandSaleKind(string kind) => TownLandHearingRules.ValidText(kind, ContentPackageRules.MaximumContentIdLength) &&
            !kind.Any(char.IsWhiteSpace) && !InventoryContainerRules.IsContainer(kind) && kind != "handcart";

    internal static InventoryLot[] LandSaleCarriedLots(ICivicTownWorld world, string actor, TownLandSalePrice price) =>
        world.SpendingGoods(actor, GoodsUse.Spend, [actor], price.ItemKind).Select(match => match.Lot)
            .Where(lot => PersonalEquipmentRules.IsCarried(lot, actor) &&
                !PersonalEquipmentRules.IsSelected(world.Inhabitants[actor].Equipment, lot.Id))
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).ToArray();

    internal static int LandSaleCarriedQuantity(ICivicTownWorld world, string actor, TownLandSalePrice price)
    {
        var available = LandSaleCarriedLots(world, actor, price).Sum(world.AvailableLotQuantity);
        // Keep the food reserve once across all payment stacks, rather than
        // letting each stack count the others as the same retained reserve.
        var reserve = PrivateWorldRuntime.CivicSharedRules.IsEdibleFood(price.ItemKind)
            ? Math.Max(2, world.CaregiverFoodCarryRequirement(actor, world.Inhabitants[actor])) : 0;
        return Math.Max(0, available - reserve);
    }

    internal InventoryLot? LandSaleStock(ICivicTownWorld world, string actor, TownRuntimeState town, TownLandSalePrice price) =>
            CivicBoard(world, town) is { } board && world.SharedItem(price.ItemKind, actor, board, PrivateWorldRuntime.CivicSharedRules.ResourceInteractionRange) is { } lot &&
            lot.OwnerId == world.Society.GetInhabitant(actor).HouseholdId && lot.DeliveryBuildingId is null &&
            lot.ItemKind != "tool" && ToolProgressionRules.Find(lot.ItemKind) is null && world.MarketSurplus(actor, lot) > 0 &&
            (lot.ItemKind != "food" || MayCollectSharedFood(world, actor)) ? lot : null;

    internal static int LandSalePickupCapacity(ICivicTownWorld world, string actor, TownRuntimeState town, InventoryLot stock) =>
            CivicBoard(world, town) is { } board ? world.PickupCarryCapacity(actor, stock, board, PrivateWorldRuntime.CivicSharedRules.ResourceInteractionRange) : 0;

    internal void AddTownLandSaleCandidates(ICivicTownWorld world, List<CognitionCandidate> candidates, string actor, TownRuntimeState town,
            TownLandTransferRequest request, IReadOnlyList<TownLandTransferParty> parties, string token)
    {
        if (request.Price is not { } price || town.Governance is not { } council ||
            !TownLandTransferRules.HasAllConsents(request, parties, council.Knowledge, world.WorldTick)) return;
        var household = world.Society.GetInhabitant(actor).HouseholdId;
        if (household == request.TargetHouseholdId)
        {
            var carried = LandSaleCarriedQuantity(world, actor, price);
            if (carried < price.Quantity && LandSaleStock(world, actor, town, price) is { } stock && LandSalePickupCapacity(world, actor, town, stock) > 0)
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_collect_payment", token),
                    "Collect available household goods you may carry for this accepted land-use sale. " + LandTransferTerms(world, request), 175));
            if (carried >= price.Quantity && CanWalkToCivicBoard(world, actor, town))
                candidates.Add(new(CivicAction(world, town.Id, "land_transfer_pay", token),
                    "Bring the exact personally owned goods to the notice place; pay the seller household only when an accepted seller adult is present, and transfer the recorded permission together. " + LandTransferTerms(world, request), 180));
        }
        else if (household == price.SellerHouseholdId && !NearCivicBoard(world, actor, town) && CanWalkToCivicBoard(world, actor, town))
            candidates.Add(new(CivicAction(world, town.Id, "land_transfer_meet", token),
                "Meet the buyer at the notice place for the accepted goods sale. Walking supplies no payment or permission change. " + LandTransferTerms(world, request), 175));
    }

    internal void ContinueTownLandSaleWalk(ICivicTownWorld world, string actor, string candidate)
    {
        var parts = candidate.Split('|');
        if (parts.Length != 5 || world.NeedsUrgentWarmth(world.Inhabitants[actor])) return;
        var town = towns.SingleOrDefault(item => item.Id == parts[1]);
        if (town is null) return;
        var candidates = new List<CognitionCandidate>();
        AddTownLandTransferCandidates(world, candidates, actor, town);
        if (!candidates.Any(item => item.Id == candidate)) return;
        var request = town.LandHearings.Transfers.Single(item => item.Status == "pending" && LandTransferActionToken(item) == parts[3]);
        var lot = parts[2] == "land_transfer_collect_payment" ? LandSaleStock(world, actor, town, request.Price!) : null;
        var destination = lot is not null ? world.HouseholdStockPosition(lot) : CivicBoard(world, town);
        var range = lot is not null ? PrivateWorldRuntime.CivicSharedRules.HouseholdStockInteractionRange(lot) : PrivateWorldRuntime.CivicSharedRules.ResourceInteractionRange;
        if (destination is { } position && !world.IsWithinInteractionRange(world.Inhabitants[actor].Position, position, range))
            world.MoveToward(actor, world.Inhabitants[actor], position, "land_sale", range);
    }

    internal void CollectLandSalePayment(ICivicTownWorld world, string actor, TownRuntimeState town, TownLandTransferRequest request)
    {
        var price = request.Price!;
        var missing = price.Quantity - LandSaleCarriedQuantity(world, actor, price);
        if (missing <= 0 || LandSaleStock(world, actor, town, price) is not { } lot) return;
        var position = world.HouseholdStockPosition(lot);
        var range = PrivateWorldRuntime.CivicSharedRules.HouseholdStockInteractionRange(lot);
        if (!world.IsWithinInteractionRange(world.Inhabitants[actor].Position, position, range))
        {
            world.MoveToward(actor, world.Inhabitants[actor], position, "land_sale_payment", range);
            return;
        }
        var quantity = Math.Min(missing, Math.Min(LandSalePickupCapacity(world, actor, town, lot), world.MarketSurplus(actor, lot)));
        if (lot.ItemKind == "food") quantity = Math.Min(quantity, SharedFoodCollectionAllowance(world, actor));
        if (quantity <= 0) return;
        world.ApplyInventoryTransition(inventory => InventoryFixture.Transfer(inventory, $"land-sale-collect:{world.WorldTick}:{actor}:{request.Id}",
            lot.OwnerId, actor, lot.Id, quantity, "land_sale_payment_collected"));
    }

    internal (TownGovernanceState Council, TownLandHearingState LandHearings) SettleLandSale(ICivicTownWorld world, TownRuntimeState town,
            TownGovernanceState council, TownLandTransferRequest request, string buyer)
    {
        var board = CivicBoard(world, town) ?? throw new InvalidOperationException("The sale needs its actual public notice place.");
        if (!NearCivicBoard(world, buyer, town))
        {
            world.MoveToward(buyer, world.Inhabitants[buyer], board, "land_sale_payment", PrivateWorldRuntime.CivicSharedRules.ResourceInteractionRange);
            return (council, town.LandHearings);
        }
        var price = request.Price!;
        var parties = LandTransferParties(world, request);
        var seller = parties.Single(party => party.HouseholdId == price.SellerHouseholdId).AdultIds
            .FirstOrDefault(actor => NearCivicBoard(world, actor, town));
        if (seller is null) return (council, town.LandHearings);
        if (LandSaleCarriedQuantity(world, buyer, price) < price.Quantity)
            throw new InvalidOperationException("The exact personally carried surplus goods price is unavailable.");
        var inventory = world.Society.Inventory;
        var remaining = price.Quantity;
        var paymentLots = new List<TownLandSalePaymentLot>();
        foreach (var lot in LandSaleCarriedLots(world, buyer, price))
        {
            var quantity = Math.Min(remaining, PrivateWorldRuntime.CivicSharedRules.AvailableLotQuantity(inventory, lot));
            inventory = InventoryFixture.Transfer(inventory, TownLandTransferRules.PaymentTransferId(request, buyer, paymentLots.Count),
                buyer, price.SellerHouseholdId, lot.Id, quantity, "land_use_right_payment",
                destinationGroundPosition: new(board.X, board.Y));
            paymentLots.Add(new(lot.Id, quantity, inventory.Events[^1].EventId));
            remaining -= quantity;
            if (remaining == 0) break;
        }
        if (remaining != 0) throw new InvalidOperationException("The exact personally carried goods price is unavailable.");
        var payment = new TownLandSalePayment(buyer, seller, board, paymentLots.ToArray());
        var (hearings, rights) = TownLandTransferRules.Advance(town.LandHearings, world.Map, householdLandUseRights,
            town.LandHearings.Transfers.Where(item => item.Status == "pending").ToDictionary(item => item.Id, item => LandTransferParties(world, item), StringComparer.Ordinal),
            householdLandUseRequests, council.Knowledge, world.WorldTick, new Dictionary<string, TownLandSalePayment> { [request.Id] = payment });
        if (hearings.Transfers.Single(item => item.Id == request.Id).Status != "transferred")
            throw new InvalidOperationException("The accepted sale no longer has valid permission terms.");
        // Both immutable plans are complete before publishing either side of the exchange.
        world.ApplyInventoryTransition(_ => inventory);
        householdLandUseRights = rights.ToList();
        council = PublishLandTransferResults(world, town, town.LandHearings, hearings, council, request.Id, buyer);
        return (council, hearings);
    }

}
