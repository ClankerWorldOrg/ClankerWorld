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
        .OrderBy(trade => (string?)trade.OfferId, StringComparer.Ordinal).ToArray();

    private IEnumerable<(ShopTradeState Trade, DirectBarterOffer Offer)> OpenShopTrades(InventoryCheckpoint? inventory = null) =>
        marketSystem.OpenShopTrades(MarketShopWorld, inventory);

    private int ReservedShopCarrySpace(string actor, InventoryCheckpoint? inventory = null) =>
        marketSystem.ReservedShopCarrySpace(MarketShopWorld, actor, inventory);

    private int ReservedShopStorageSpace(string buildingId, InventoryCheckpoint? inventory = null) =>
        marketSystem.ReservedShopStorageSpace(MarketShopWorld, buildingId, inventory);

    private int RestaurantInputDemand(string actor, string itemKind, int spentPersonalQuantity = 0) =>
        marketSystem.RestaurantInputDemand(MarketDemandWorld, actor, itemKind, spentPersonalQuantity);

    private ActionCheck CheckSelectedShopQuote(string buyer, PlacedBuilding building, PlannedShopQuote quote, bool requestedGoods) =>
        marketSystem.CheckSelectedShopQuote(MarketShopPlanningWorld, buyer, building, quote, requestedGoods);

    private ShopQuote? ShopOpportunity(string buyer, PlacedBuilding building, string? exactGoodsLotId = null, bool requestedGoods = false) =>
        marketSystem.ShopOpportunity(MarketShopPlanningWorld, buyer, building, exactGoodsLotId, requestedGoods);

    private bool IsShopFoodCandidate(string actor, string candidateId, ShopBuyActionPlan? selectedPlan = null)
    {
        // Only the buyer's ready food can interrupt an order. Raw ingredients,
        // equipment and a seller's response still follow ordinary work rules.
        if (candidateId.StartsWith("shop_continue:", StringComparison.Ordinal))
            return OpenShopTrades().Any(pair => pair.Trade.BuyerId == actor &&
                candidateId == "shop_continue:" + pair.Offer.Id && IsEdibleFood(pair.Trade.GoodsKind));
        if (!candidateId.StartsWith("shop_buy:", StringComparison.Ordinal)) return false;
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == candidateId["shop_buy:".Length..]);
        if (building is null) return false;
        var kind = selectedPlan?.Quote?.Goods.ItemKind ??
            (selectedPlan is null ? ShopOpportunity(actor, building)?.Goods.ItemKind : null);
        if (kind is not null)
            return IsEdibleFood(kind) && MarketSystem.WantsTradeFoodKind(MarketBarterWorld,actor,kind);
        return MarketSystem.RestaurantMealShopTrip(MarketShopPlanningWorld,actor,building);
    }

    private ShopResponseActionPlan? PlanShopResponse(string actor, bool cancel, string offerId) =>
        marketSystem.PlanShopResponse(MarketShopWorld, actor, cancel, offerId);

    private void AddShopCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || NeedsUrgentWarmth(agents[actor])) return;
        if (!NeedsUrgentFood(agents[actor])) AddStoreStockCandidate(candidates, actor);
        foreach (var (trade, offer) in OpenShopTrades().OrderBy(pair => pair.Trade.OfferId.Value, StringComparer.Ordinal))
        {
            var buyer = trade.BuyerId == actor;
            if (!buyer && !MarketSystem.MayRespondAtShop(MarketShopPlanningWorld,actor,trade)) continue;
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

    private string OpenShopQuote(string actor, PlacedBuilding building, ShopQuote choice) =>
        marketSystem.OpenShopQuote(MarketShopWorld, actor, building, choice);

    private void CancelShopTrade(ShopTradeState trade, DirectBarterOffer offer, string reason) =>
        marketSystem.CancelShopTrade(MarketShopWorld, trade, offer, reason);

    private void MaintainShopTrades() =>
        marketSystem.MaintainShopTrades(MarketShopWorld);

    private static void ValidateShopTrades(IReadOnlyList<ShopTradeState>? trades,
        SocietyCheckpoint societyState, WorldMap map, long worldTick)
    {
        var inventory = societyState.Inventory;
        if (trades is null || trades.Any(trade => trade is null) || !trades.Select(trade => (string?)trade.OfferId).SequenceEqual(
                trades.Select(trade => (string?)trade.OfferId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The checkpoint is missing canonical shop trade records.");
        MarketSystem.ValidateLocalShopTrades(trades);
        foreach (var trade in trades)
        {
            var offer = inventory.Offers.SingleOrDefault(item => item.Id == trade.OfferId);
            if (offer is null || !offer.Id.Value.StartsWith(ShopTradePrefix, StringComparison.Ordinal) ||
                offer.Id != ShopTradeOfferId(trade.ProposedTick, trade.BuyerId, trade.BuildingInstanceId) ||
                trade.ProposedTick > worldTick ||
                !societyState.Households.Any(household => household.Id == trade.SellerHouseholdId) ||
                !societyState.Agents.Any(person => person.Id == trade.BuyerId) ||
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
                offer.State == DirectBarterState.Cancelled && !BoundedText.IsValid(trade.CancellationReason, 160, requireTrimmed: true) ||
                trade.CancellationReason is not null && offer.State != DirectBarterState.Cancelled)
                throw new InvalidDataException("A shop trade does not match its authoritative inventory offer.");
        }
        if (inventory.Offers.Any(offer => offer.Id.Value.StartsWith(ShopTradePrefix, StringComparison.Ordinal) &&
                !trades.Any(trade => trade.OfferId == offer.Id)))
            throw new InvalidDataException("An inventory shop offer has no physical shop binding.");
    }
}
