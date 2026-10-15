using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal sealed record ShopQuote(InventoryLot Goods, int GoodsQuantity, InventoryLot Payment, int PaymentQuantity);
    internal IEnumerable<(ShopTradeState Trade, DirectBarterOffer Offer)> OpenShopTrades(IMarketShopWorld world, InventoryCheckpoint? inventory = null) =>
        shopTrades.Select(trade => (Trade: trade, Offer: (inventory ?? world.Society.Inventory).Offers
                .Single(offer => offer.Id == trade.OfferId)))
            .Where(pair => pair.Offer.State == DirectBarterState.Open);

    internal int ReservedShopCarrySpace(IMarketShopWorld world, string actor, InventoryCheckpoint? inventory = null) => OpenShopTrades(world, inventory)
        .Where(pair => pair.Trade.BuyerId == actor)
        .Sum(pair => Math.Max(0, pair.Offer.FirstQuantity - pair.Offer.SecondQuantity));

    internal int ReservedShopStorageSpace(IMarketShopWorld world, string buildingId, InventoryCheckpoint? inventory = null) => OpenShopTrades(world, inventory)
        .Where(pair => pair.Trade.BuildingInstanceId == buildingId)
        .Sum(pair => Math.Max(0, pair.Offer.SecondQuantity - pair.Offer.FirstQuantity));

    internal ShopResponseActionPlan? PlanShopResponse(IMarketShopWorld world, string actor, bool cancel, string offerId)
    {
        var trade = shopTrades.SingleOrDefault(item => item.OfferId == offerId);
        var offer = world.Society.Inventory.Offers.SingleOrDefault(item => item.Id == offerId);
        return trade is null || offer is null ? null : new(actor, cancel, trade, offer);
    }

    internal static bool ShopResponseOfferIsCurrent(IMarketShopWorld world, ShopResponseActionPlan plan)
    {
        var current = world.Society.Inventory.Offers.SingleOrDefault(offer => offer.Id == plan.Offer.Id);
        return current is { State: DirectBarterState.Open } && TownHearingProcedure.Digest(current) == TownHearingProcedure.Digest(plan.Offer);
    }

    internal void ApplyRetainedShopResponse(IMarketShopWorld world, string actor, AgentState state, ShopResponseActionPlan plan)
    {
        var cancel = plan.Cancel;
        var trade = plan.Trade;
        var offer = plan.Offer;
        if (trade is null || offer is not { State: DirectBarterState.Open } ||
            (actor != trade.BuyerId && !world.MayRespondAtShop(actor, trade))) return;
        if (cancel)
        {
            CancelShopTrade(world, trade, offer, "One of the traders cancelled the exchange.");
            return;
        }
        if (ShopTradeFailure(world, trade, offer) is { } failure)
        {
            CancelShopTrade(world, trade, offer, failure);
            return;
        }
        if (!world.IsWithinInteractionRange(state.Position, trade.Position, world.ResourceInteractionRange))
        {
            world.MoveToward(actor, state, trade.Position, "shop_trade", world.ResourceInteractionRange);
            return;
        }
        if (actor == trade.BuyerId || !world.IsWithinInteractionRange(world.PhysicalAgents.Living[trade.BuyerId].Position,
                trade.Position, world.ResourceInteractionRange)) return;
        world.AcceptShopTrade(trade, offer);
        RecordShopTrade(trade with { SellerActorId = actor });
        world.AppendEvent("shop_trade_completed", actor + ":" + offer.Id);
    }

    internal string OpenShopQuote(IMarketShopWorld world, string actor, PlacedBuilding building, ShopQuote choice)
    {
        var id = world.ShopTradeOfferId(world.WorldTick, actor, building.InstanceId);
        // Quotes name exact actual quantities; future goods are never reserved.
        world.OpenShopOffer(id, actor, building, choice);
        ProposeShopTrade(new(id, building.InstanceId, building.HouseholdId!, actor, building.Position, world.WorldTick,
            choice.Goods.ItemKind, choice.Payment.ItemKind));
        world.AppendEvent("shop_trade_offered", actor + ":" + id);
        return id;
    }

    internal string? ShopTradeFailure(IMarketShopWorld world, ShopTradeState trade, DirectBarterOffer offer)
    {
        if (offer.ExpiryTick < world.WorldTick) return "The offer ran out of time.";
        if (!world.PhysicalAgents.Living.TryGetValue(trade.BuyerId, out var buyerState) || !world.IsAdult(trade.BuyerId))
            return "The buyer is no longer available.";
        var building = world.Buildings.SingleOrDefault(item => item.InstanceId == trade.BuildingInstanceId);
        if (building is null || building.HouseholdId != trade.SellerHouseholdId || building.Position != trade.Position ||
            !world.PhysicalAgents.Living.Keys.Any(actor => world.MayRespondAtShop(actor, trade)))
            return "The selling household no longer has access to this shop.";
        var inventory = world.Society.Inventory;
        var goods = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.FirstLotId);
        var payment = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.SecondLotId);
        if (goods is null || payment is null || !IsLooseShopLot(goods) || !IsLooseShopLot(payment) ||
            goods.OwnerId != trade.SellerHouseholdId || goods.StorageBuildingId != trade.BuildingInstanceId ||
            goods.GroundPosition is not null || !PersonalEquipmentRules.IsCarried(payment, trade.BuyerId) ||
            goods.Quantity < offer.FirstQuantity || payment.Quantity < offer.SecondQuantity ||
            new[] { DerivedId.Create(offer.Id + ":first"), DerivedId.Create(offer.Id + ":second") }.Any(id => !inventory.Reservations.Any(reservation =>
                reservation.Id == id && reservation.State == InventoryReservationState.Reserved)))
            return "The goods set aside for this exchange are no longer at the agreed place.";
        var kind = ShopRules.KindOf(world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId));
        if (kind is null || !ShopRules.MaySell(kind, goods.ItemKind))
            return "This shop cannot sell the offered goods.";
        var equipment = buyerState.Equipment;
        var otherCarryReservations = ReservedShopCarrySpace(world, trade.BuyerId) -
            Math.Max(0, offer.FirstQuantity - offer.SecondQuantity);
        var afterCarry = PersonalEquipmentRules.CarriedQuantity(inventory, trade.BuyerId, equipment) -
            offer.SecondQuantity + offer.FirstQuantity + otherCarryReservations;
        var storageRoom = world.DestinationRoom(inventory, trade.BuildingInstanceId, outgoingQuantity: offer.FirstQuantity,
            releasedShopReservation: Math.Max(0, offer.SecondQuantity - offer.FirstQuantity));
        if (afterCarry > PersonalEquipmentRules.Capacity(inventory, trade.BuyerId, equipment) + world.HorseCargoCapacity(trade.BuyerId) ||
            offer.SecondQuantity > storageRoom)
            return "There is no longer enough room to receive the goods.";
        if (!world.IsWithinInteractionRange(buyerState.Position, trade.Position, world.ResourceInteractionRange) &&
            world.Reach(trade.BuyerId, trade.Position, ReachPurpose.ShopResponse, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return "The buyer can no longer reach the shop.";
        return null;
    }

    internal void CancelShopTrade(IMarketShopWorld world, ShopTradeState trade, DirectBarterOffer offer, string reason)
    {
        if (offer.State == DirectBarterState.Open)
            world.CancelShopOffer(offer);
        RecordShopTrade(trade with { CancellationReason = reason });
        world.AppendEvent("shop_trade_cancelled", trade.BuyerId + ":" + trade.OfferId);
    }

    internal void MaintainShopTrades(IMarketShopWorld world)
    {
        foreach (var trade in shopTrades.ToArray())
        {
            var offer = world.Society.Inventory.GetOffer(trade.OfferId);
            if (offer.State == DirectBarterState.Open && ShopTradeFailure(world, trade, offer) is { } failure)
                CancelShopTrade(world, trade, offer, failure);
            else if (offer.State == DirectBarterState.Cancelled && trade.CancellationReason is null)
                CancelShopTrade(world, trade, offer, offer.ExpiryTick < world.WorldTick
                    ? "The offer ran out of time." : "The exchange was interrupted.");
        }
        var retired = shopTrades.Where(trade => world.Society.Inventory.GetOffer(trade.OfferId).State != DirectBarterState.Open)
            .OrderByDescending(trade => trade.ProposedTick).ThenBy(trade => trade.OfferId, StringComparer.Ordinal)
            .Skip(32).Select(trade => trade.OfferId).ToHashSet(StringComparer.Ordinal);
        if (retired.Count == 0) return;
        RetireShopTrades(retired);
        world.RetireShopOffers(retired);
    }
}
