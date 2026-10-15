using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal static partial class InventorySystem
{
    internal static Command CancelBarterOffer(string id, int revision, string actor) => new PreparedCommand(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory, id, revision, actor));

    internal static Command UpdateMarketStockHolds(IInventoryMarketsWorld commandContext) => new PreparedCommand(inventory => MarketStockHolds.Update(inventory, commandContext.towns));

    internal static Command PickUpMarketStock(IInventoryMarketsWorld commandContext, string actor, MarketSystem.MarketChoice choice) => new PreparedCommand(inventory => InventoryAuthority.Relocate(inventory, MarketTradeRules.Identity("market-pickup:", actor, choice.Id, commandContext.WorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture)), choice.Lot!.Id, choice.Lot.OwnerId, choice.Quantity, carrierId: actor, holdAccess: choice.Kind == "collect" && choice.Occupancy is { } loan ? [InventoryStore.CreateInventoryHold(InventoryHoldPurpose.MarketStall, loan.Id), InventoryStore.CreateInventoryHold(InventoryHoldPurpose.Borrowed, actor)] : InventoryHoldRules.BorrowedBy(actor)));

    internal static Command DepositMarketLoad(string actor, InventoryLot lot, int quantity, string operation, GridPoint position, long eventId, Action<long> recordEventId) => new PreparedCommand(inventory =>
{
    var next = InventoryAuthority.Relocate(inventory, operation, lot.Id, lot.OwnerId, quantity, groundPosition: new(position.X, position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor));
    eventId = RecordPreparedValue(next.Events[^1].EventId, recordEventId);
    return next;
});

    internal static Command OpenMarketOffer(IInventoryMarketsWorld commandContext, string id, string buyer, MarketSystem.MarketChoice choice) => new PreparedCommand(inventory => InventoryAuthority.AcceptDirectBarterOffer(InventoryAuthority.CreateDirectBarterOffer(inventory, InventoryStore.CreateDirectBarterProposal(id, 1, choice.Lot!.OwnerId, buyer, choice.Lot.Id, 1, choice.Payment!.Id, 1, commandContext.WorldTick + MarketTradeRules.OfferLifetimeTicks), holdAccess: [InventoryStore.CreateInventoryHold(InventoryHoldPurpose.MarketStall, choice.Occupancy!.Id)]), id, 1, buyer));

    internal static Command SettleMarketOffer(string actor, MarketTradeState trade, DirectBarterOffer offer, string receipt, string goods, string payment, long eventId, Action<long> recordEventId) => new PreparedCommand(inventory =>
{
    var settled = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer.Id, offer.Revision, trade.GoodsOwnerId);
    settled = InventoryAuthority.Relocate(settled, offer.Id + ":collect", goods, trade.BuyerId, offer.FirstQuantity, carrierId: trade.BuyerId, holdAccess: [InventoryStore.CreateInventoryHold(InventoryHoldPurpose.MarketStall, trade.OccupancyId)]);
    if (trade.GoodsOwnerId != trade.PaymentOwnerId)
        settled = InventoryAuthority.Transfer(settled, offer.Id + ":household-payment", trade.GoodsOwnerId, trade.PaymentOwnerId, payment, offer.SecondQuantity, "market_household_payment", holdAccess: InventoryHoldRules.BorrowedBy(actor));
    settled = InventoryAuthority.Relocate(settled, TownRecordIds.DepositOperation(receipt), payment, trade.PaymentOwnerId, offer.SecondQuantity, groundPosition: new(trade.Position.X, trade.Position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor));
    eventId = RecordPreparedValue(settled.Events[^1].EventId, recordEventId);
    return settled;
});

    internal static Command RetireMarketOffers(IReadOnlySet<string> offers, IReadOnlySet<string> claims) => new PreparedCommand(inventory => InventoryStore.PrepareBarterLedger(inventory, inventory.Offers.Where(offer => !offers.Contains(offer.Id)).ToArray(), inventory.Reservations.Where(claim => !claims.Contains(claim.Id)).ToArray()));

    internal static Command AcceptShopTrade(ShopTradeState trade, DirectBarterOffer offer) => new PreparedCommand(inventory =>
{
    var payment = inventory.GetLot(offer.SecondLotId);
    var paymentId = payment.Quantity == offer.SecondQuantity ? payment.Id : DerivedId.Create($"{payment.Id}#barter:{offer.Id}");
    var settled = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer.Id, offer.Revision, trade.SellerHouseholdId);
    return InventoryStore.PrepareLots(settled, settled.Lots.Select(lot => lot.Id == paymentId ? InventoryStore.PrepareStoredStock(lot, trade.BuildingInstanceId) : lot).ToArray());
});

    internal static Command OpenShopOffer(IInventoryMarketsWorld commandContext, string id, string actor, PlacedBuilding building, MarketSystem.ShopQuote quote) => new PreparedCommand(inventory => InventoryAuthority.AcceptDirectBarterOffer(InventoryAuthority.CreateDirectBarterOffer(inventory, InventoryStore.CreateDirectBarterProposal(id, 1, building.HouseholdId!, actor, quote.Goods.Id, quote.GoodsQuantity, quote.Payment.Id, quote.PaymentQuantity, commandContext.WorldTick + 120)), id, 1, actor));

    internal static Command CancelShopOffer(DirectBarterOffer offer) => new PreparedCommand(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory, offer.Id, offer.Revision, offer.FirstPartyId));

    internal static Command RetireShopOffers(IReadOnlySet<string> retired) => new PreparedCommand(inventory => InventoryStore.PrepareBarterLedger(inventory, inventory.Offers.Where(offer => !retired.Contains(offer.Id)).ToArray(), inventory.Reservations.Where(reservation => !retired.Any(id => reservation.Id == DerivedId.Create(id + ":first") || reservation.Id == DerivedId.Create(id + ":second"))).ToArray()));
}
