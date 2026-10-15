using ShopQuote = ClankerWorld.Simulation.World.MarketSystem.ShopQuote;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string ShopTradePrefix = "shop-trade:";
    private MarketSystem marketSystem => townSystem.Markets;
    private IReadOnlyList<ShopTradeState> shopTrades => marketSystem.Trades;

    public IReadOnlyList<ShopTradeState> ShopTrades => shopTrades
        .OrderBy(trade => trade.OfferId, StringComparer.Ordinal).ToArray();

    private IEnumerable<(ShopTradeState Trade, DirectBarterOffer Offer)> OpenShopTrades(InventoryCheckpoint? inventory = null) =>
        marketSystem.OpenShopTrades(MarketShopWorld, inventory);

    private int ReservedShopCarrySpace(string actor, InventoryCheckpoint? inventory = null) =>
        marketSystem.ReservedShopCarrySpace(MarketShopWorld, actor, inventory);

    private int ReservedShopStorageSpace(string buildingId, InventoryCheckpoint? inventory = null) =>
        marketSystem.ReservedShopStorageSpace(MarketShopWorld, buildingId, inventory);

    private static bool IsLooseShopLot(InventoryLot lot, bool includeGround = false) =>
        MarketSystem.IsLooseShopLot(lot, includeGround);

    private bool MayRespondAtShop(string actor, ShopTradeState trade) =>
        MarketSystem.MayRespondAtShop(MarketShopPlanningWorld, actor, trade);

    private bool ShopBuyerWants(string actor, InventoryLot lot) =>
        MarketSystem.ShopBuyerWants(MarketShopPlanningWorld, actor, lot);

    private int RestaurantInputDemand(string actor, string itemKind, int spentPersonalQuantity = 0) =>
        marketSystem.RestaurantInputDemand(MarketDemandWorld, actor, itemKind, spentPersonalQuantity);

    private bool ShopBuyerWantsWithoutRestaurant(string actor, InventoryLot lot) =>
        MarketSystem.ShopBuyerWantsWithoutRestaurant(MarketDemandWorld, actor, lot);

    private bool ShopPaymentUseful(PlacedBuilding building, InventoryLot payment) =>
        MarketSystem.ShopPaymentUseful(MarketShopPlanningWorld, building, payment);


    private (int Goods, int Payment) ShopQuoteAmounts(PlacedBuilding building, InventoryLot goods, InventoryLot payment) =>
        MarketSystem.ShopQuoteAmounts(MarketShopPlanningWorld, building, goods, payment);

    private bool ShopReceivingSpace(string buyer, PlacedBuilding building, int goods, int payment) =>
        marketSystem.ShopReceivingSpace(MarketShopPlanningWorld, buyer, building, goods, payment);

    private static HashSet<string> BestUsableToolIds(InventoryCheckpoint inventory, string actor) =>
        MarketSystem.BestUsableToolIds(inventory, actor);

    private IEnumerable<InventoryLot> ShopPaymentLots(string buyer, PlacedBuilding building) =>
        MarketSystem.ShopPaymentLots(MarketShopPlanningWorld, buyer, building);

    private bool MayVisitShop(string buyer, PlacedBuilding building) =>
        MarketSystem.MayVisitShop(MarketShopPlanningWorld, buyer, building);

    private bool RestaurantIngredientShopTrip(string buyer, PlacedBuilding building) =>
        MarketSystem.RestaurantIngredientShopTrip(MarketShopPlanningWorld, buyer, building);

    private bool RestaurantMealShopTrip(string buyer, PlacedBuilding building) =>
        MarketSystem.RestaurantMealShopTrip(MarketShopPlanningWorld, buyer, building);

    private bool ShopQuoteContext(string buyer, PlacedBuilding building) =>
        MarketSystem.ShopQuoteContext(MarketShopPlanningWorld, buyer, building);

    private ActionCheck CheckSelectedShopQuote(string buyer, PlacedBuilding building, PlannedShopQuote quote, bool requestedGoods) =>
        marketSystem.CheckSelectedShopQuote(MarketShopPlanningWorld, buyer, building, quote, requestedGoods);

    private ShopQuote? ShopOpportunity(string buyer, PlacedBuilding building, string? exactGoodsLotId = null, bool requestedGoods = false) =>
        marketSystem.ShopOpportunity(MarketShopPlanningWorld, buyer, building, exactGoodsLotId, requestedGoods);

    private bool IsShopFoodCandidate(string actor, string candidateId)
    {
        // Only the buyer's ready food can interrupt an order. Raw ingredients,
        // equipment and a seller's response still follow ordinary work rules.
        if (candidateId.StartsWith("shop_continue:", StringComparison.Ordinal))
            return OpenShopTrades().Any(pair => pair.Trade.BuyerId == actor &&
                candidateId == "shop_continue:" + pair.Offer.Id && IsEdibleFood(pair.Trade.GoodsKind));
        if (!candidateId.StartsWith("shop_buy:", StringComparison.Ordinal)) return false;
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == candidateId["shop_buy:".Length..]);
        if (building is null) return false;
        if (ShopOpportunity(actor, building) is { } quote)
            return IsEdibleFood(quote.Goods.ItemKind) && WantsTradeFoodKind(actor, quote.Goods.ItemKind);
        return RestaurantMealShopTrip(actor, building);
    }

    private ShopResponseActionPlan? PlanShopResponse(string actor, bool cancel, string offerId) =>
        marketSystem.PlanShopResponse(MarketShopWorld, actor, cancel, offerId);

    private bool ShopResponseOfferIsCurrent(ShopResponseActionPlan plan) =>
        MarketSystem.ShopResponseOfferIsCurrent(MarketShopWorld, plan);

    private void AddShopCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || NeedsUrgentWarmth(agents[actor])) return;
        if (!NeedsUrgentFood(agents[actor])) AddStoreStockCandidate(candidates, actor);
        foreach (var (trade, offer) in OpenShopTrades().OrderBy(pair => pair.Trade.OfferId, StringComparer.Ordinal))
        {
            var buyer = trade.BuyerId == actor;
            if (!buyer && !MayRespondAtShop(actor, trade)) continue;
            var goods = society.Checkpoint.Inventory.GetLot(offer.FirstLotId);
            var payment = society.Checkpoint.Inventory.GetLot(offer.SecondLotId);
            var continuePlan = new ShopResponseActionPlan(actor, false, trade, offer);
            var cancelPlan = new ShopResponseActionPlan(actor, true, trade, offer);
            if (CheckActionPlan(continuePlan).Allowed) candidates.Add(new(continuePlan.CandidateId, buyer
                ? $"Wait at the shop to receive {offer.FirstQuantity} {goods.ItemKind} for {offer.SecondQuantity} {payment.ItemKind}."
                : $"Meet the customer at the shop and accept {offer.SecondQuantity} {payment.ItemKind} for {offer.FirstQuantity} {goods.ItemKind}.",
                12, trade.BuildingInstanceId)
            { ActionPlan = continuePlan });
            if (CheckActionPlan(cancelPlan).Allowed) candidates.Add(new(cancelPlan.CandidateId,
                "Cancel this exchange and release the goods and receiving space.", 65, trade.BuildingInstanceId)
            { ActionPlan = cancelPlan });
        }
        foreach (var building in worldSimulation.Buildings.OrderBy(building => building.InstanceId, StringComparer.Ordinal))
        {
            var plan = PlanShopBuy(actor, building);
            if (plan is null || !CheckActionPlan(plan).Allowed) continue;
            var text = plan.Quote is { } choice
                ? $"Offer {choice.PaymentQuantity} {choice.Payment.ItemKind} for {choice.GoodsQuantity} {choice.Goods.ItemKind} stocked at this shop; the household may refuse."
                : plan.Kind == ShopBuyKind.IngredientsTrip
                    ? "Visit this Town shop to ask about missing Restaurant ingredients; any offer is checked there."
                    : "Visit this Restaurant to ask about a meal; any offer is checked there.";
            candidates.Add(new(plan.CandidateId, text, 14, building.InstanceId) { ActionPlan = plan });
        }
    }

    private void ApplyShopCandidate(string actor, AgentState state, string candidate, ActionPlan? originalPlan = null)
    {
        if (originalPlan is ShopBuyActionPlan buyPlan) { ApplyShopBuy(buyPlan); return; }
        if (originalPlan is ShopResponseActionPlan responsePlan) { ApplyShopResponse(responsePlan); return; }
        if (candidate == "shop_stock_store")
        {
            StockStore(actor, state);
            return;
        }
        if (candidate.StartsWith("shop_buy:", StringComparison.Ordinal))
        {
            var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == candidate["shop_buy:".Length..]);
            if (building is not null && PlanShopBuy(actor, building) is { } plan) ApplyShopBuy(plan);
            return;
        }
        var cancel = candidate.StartsWith("shop_cancel:", StringComparison.Ordinal);
        var offerId = candidate[(cancel ? "shop_cancel:".Length : "shop_continue:".Length)..];
        if (PlanShopResponse(actor, cancel, offerId) is { } response) ApplyShopResponse(response);
    }

    private void ApplyRetainedShopResponse(string actor, AgentState state, ShopResponseActionPlan plan) =>
        marketSystem.ApplyRetainedShopResponse(MarketShopWorld, actor, state, plan);

    private string OpenShopQuote(string actor, PlacedBuilding building, ShopQuote choice) =>
        marketSystem.OpenShopQuote(MarketShopWorld, actor, building, choice);

    private string? ShopTradeFailure(ShopTradeState trade, DirectBarterOffer offer) =>
        marketSystem.ShopTradeFailure(MarketShopWorld, trade, offer);

    private void ReplaceShopTrade(ShopTradeState trade) =>
        marketSystem.RecordShopTrade(trade);

    private void CancelShopTrade(ShopTradeState trade, DirectBarterOffer offer, string reason) =>
        marketSystem.CancelShopTrade(MarketShopWorld, trade, offer, reason);

    private void MaintainShopTrades() =>
        marketSystem.MaintainShopTrades(MarketShopWorld);

    private static void ValidateShopTrades(IReadOnlyList<ShopTradeState>? trades,
        SocietyCheckpoint societyState, WorldMap map, long worldTick)
    {
        var inventory = societyState.Inventory;
        if (trades is null || trades.Any(trade => trade is null) || !trades.Select(trade => trade.OfferId).SequenceEqual(
                trades.Select(trade => trade.OfferId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The checkpoint is missing canonical shop trade records.");
        MarketSystem.ValidateLocalShopTrades(trades);
        foreach (var trade in trades)
        {
            var offer = inventory.Offers.SingleOrDefault(item => item.Id == trade.OfferId);
            if (offer is null || !offer.Id.StartsWith(ShopTradePrefix, StringComparison.Ordinal) ||
                offer.Id != ShopTradeOfferId(trade.ProposedTick, trade.BuyerId, trade.BuildingInstanceId) ||
                trade.ProposedTick < 0 || trade.ProposedTick > worldTick ||
                !BoundedShopText(trade.BuildingInstanceId, 256) ||
                !societyState.Households.Any(household => household.Id == trade.SellerHouseholdId) ||
                !societyState.Agents.Any(person => person.Id == trade.BuyerId) ||
                !ShopItemKind(trade.GoodsKind) || !ShopItemKind(trade.PaymentKind) ||
                trade.Position.X < 0 || trade.Position.X >= map.Width || trade.Position.Y < 0 || trade.Position.Y >= map.Height ||
                inventory.Lots.Any(lot => lot.Id == offer.FirstLotId && lot.ItemKind != trade.GoodsKind) ||
                inventory.Lots.Any(lot => lot.Id == offer.SecondLotId && lot.ItemKind != trade.PaymentKind) ||
                offer.FirstPartyId != trade.SellerHouseholdId ||
                offer.SecondPartyId != trade.BuyerId || offer.FirstPartyId == offer.SecondPartyId ||
                offer.State == DirectBarterState.Open &&
                    !offer.AcceptedBy.SequenceEqual([trade.BuyerId], StringComparer.Ordinal) ||
                offer.State == DirectBarterState.Settled &&
                    !offer.AcceptedBy.SequenceEqual(new[] { offer.FirstPartyId, offer.SecondPartyId }
                        .Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
                offer.State == DirectBarterState.Settled && (trade.SellerActorId is null ||
                    !societyState.Agents.Any(person => person.Id == trade.SellerActorId)) ||
                trade.SellerActorId is not null && offer.State != DirectBarterState.Settled ||
                offer.State == DirectBarterState.Cancelled && !BoundedShopText(trade.CancellationReason, 160) ||
                trade.CancellationReason is not null && offer.State != DirectBarterState.Cancelled)
                throw new InvalidDataException("A shop trade does not match its authoritative inventory offer.");
        }
        if (inventory.Offers.Any(offer => offer.Id.StartsWith(ShopTradePrefix, StringComparison.Ordinal) &&
                !trades.Any(trade => trade.OfferId == offer.Id)))
            throw new InvalidDataException("An inventory shop offer has no physical shop binding.");
    }

    // Content quantities and inventory resource kinds have no separate trade-record length limit.
    private static bool ShopItemKind(string? text) => text is { Length: > 0 } &&
        text == text.Trim() && !text.Any(char.IsControl);

    internal static bool BoundedShopText(string? text, int limit) => text is { Length: > 0 } &&
        text.Length <= limit && text == text.Trim() && !text.Any(char.IsControl);
}
