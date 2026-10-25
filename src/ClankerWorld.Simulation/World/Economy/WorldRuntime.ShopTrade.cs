using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
using ShopQuote = ClankerWorld.Simulation.World.MarketSystem.ShopQuote;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string ShopTradePrefix = "shop-trade:";
    private MarketSystem marketSystem => townSystem.Markets;
    private IReadOnlyList<ShopTradeState> shopTrades => marketSystem.Trades;

    public IReadOnlyList<ShopTradeState> ShopTrades => shopTrades
        .OrderBy(trade => (string?)trade.OfferId, StringComparer.Ordinal).ToArray();

    private ShopQuote? ShopOpportunity(string buyer, PlacedBuilding building, string? exactGoodsLotId = null, bool requestedGoods = false) =>
        marketSystem.ShopOpportunity(MarketShopPlanningWorld, buyer, building, exactGoodsLotId, requestedGoods);

    private bool IsShopFoodCandidate(string actor, string candidateId, ShopBuyActionPlan? selectedPlan = null)
    {
        // Only the buyer's ready food can interrupt an order. Raw ingredients,
        // equipment and a seller's response still follow ordinary work rules.
        if (candidateId.StartsWith("shop_continue:", StringComparison.Ordinal))
            return this.marketSystem.OpenShopTrades(this.MarketShopWorld, null).Any(pair => pair.Trade.BuyerId == actor &&
                candidateId == "shop_continue:" + pair.Offer.Id && IsEdibleFood(pair.Trade.GoodsKind));
        if (!candidateId.StartsWith("shop_buy:", StringComparison.Ordinal)) return false;
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == candidateId["shop_buy:".Length..]);
        if (building is null) return false;
        var kind = selectedPlan?.Quote?.Goods.ItemKind ??
            (selectedPlan is null ? ShopOpportunity(actor, building)?.Goods.ItemKind : null);
        if (kind is not null)
            return IsEdibleFood(kind) && MarketSystem.WantsTradeFoodKind(MarketBarterWorld, actor, kind);
        return MarketSystem.RestaurantMealShopTrip(MarketShopPlanningWorld, actor, building);
    }

    private void AddShopCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || NeedsUrgentWarmth(agents[actor])) return;
        if (!NeedsUrgentFood(agents[actor])) AddStoreStockCandidate(candidates, actor);
        foreach (var (trade, offer) in this.marketSystem.OpenShopTrades(this.MarketShopWorld, null).OrderBy(pair => pair.Trade.OfferId.Value, StringComparer.Ordinal))
        {
            var buyer = trade.BuyerId == actor;
            if (!buyer && !MarketSystem.MayRespondAtShop(MarketShopPlanningWorld, actor, trade)) continue;
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
            var plan = this.marketSystem.PlanShopBuy(this.MarketShopPlanningWorld, actor, building, null, null);
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
            if (building is not null && this.marketSystem.PlanShopBuy(this.MarketShopPlanningWorld, actor, building, null, null) is { } plan) ApplyShopBuy(plan);
            return;
        }
        var cancel = candidate.StartsWith("shop_cancel:", StringComparison.Ordinal);
        var offerId = candidate[(cancel ? "shop_cancel:".Length : "shop_continue:".Length)..];
        if (this.marketSystem.PlanShopResponse(this.MarketShopWorld, actor, cancel, offerId) is { } response) ApplyShopResponse(response);
    }

    private string OpenShopQuote(string actor, PlacedBuilding building, ShopQuote choice) =>
        marketSystem.OpenShopQuote(MarketShopWorld, actor, building, choice);

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

    private void ApplyShopBuy(ShopBuyActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        this.marketSystem.ApplyCheckedShopBuy(this.MarketShopPlanningWorld, plan);
    }

    private void ApplyShopResponse(ShopResponseActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        this.marketSystem.ApplyCheckedShopResponse(this.MarketShopPlanningWorld, plan);
    }

    private static ActorCapabilityPolicy ShopCapabilities => MarketSystem.ShopCapabilities;
    private static ActorCapabilityPolicy PersonalBarterCapabilities => MarketSystem.PersonalBarterCapabilities;
    private static ActorCapabilityPolicy MarketStockCapabilities(MarketStockActionPlan plan) => MarketSystem.MarketStockCapabilities(plan);

    private TPlan? PlanMarketRequest<TPlan>(string actor, MarketActionRequest request) where TPlan : ActionPlan
    {
        if (MarketSystem.MarketChoices(this.MarketStallWorld, actor).SingleOrDefault(item => item.Id == request.CandidateId) is not { } choice) return null;
        ActionPlan? plan = request.Family switch
        {
            MarketActionFamily.Occupancy => MarketSystem.PlanMarketOccupancy(MarketStallWorld, actor, choice),
            MarketActionFamily.Stock => MarketSystem.PlanMarketStock(MarketStallWorld, actor, choice),
            _ => MarketSystem.PlanMarketExchange(MarketStallWorld, actor, choice),
        };
        return plan as TPlan;
    }

    private static class MarketActions
    {
        internal static readonly ActionDefinition<ShopBuyChoice, ShopBuyActionPlan> ShopBuy = new(
            DecodeReferenceChoice(ShopBuyChoice.Decode), static choice => choice.Encode(), ShopCapabilities,
            static (world, actor, choice, _) => world.worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == choice.BuildingId) is { } shop
                ? world.marketSystem.PlanShopBuy(world.MarketShopPlanningWorld, actor, shop, null, null) : null,
            static (world, plan, policy) => world.marketSystem.CheckShopBuy(world.MarketShopPlanningWorld, plan, policy),
            static (world, plan, _, _) => { world.marketSystem.ApplyCheckedShopBuy(world.MarketShopPlanningWorld, plan); return true; });

        internal static readonly ActionDefinition<ShopResponseChoice, ShopResponseActionPlan> ShopResponse = new(
            DecodeReferenceChoice(ShopResponseChoice.Decode), static choice => choice.Encode(), ShopCapabilities,
            static (world, actor, choice, _) => world.marketSystem.PlanShopResponse(world.MarketShopWorld, actor, choice.Cancel, choice.OfferId),
            static (world, plan, policy) => world.marketSystem.CheckShopResponse(world.MarketShopPlanningWorld, plan, policy),
            static (world, plan, _, _) => { world.marketSystem.ApplyCheckedShopResponse(world.MarketShopPlanningWorld, plan); return true; });

        internal static readonly ActionDefinition<DirectBarterChoice, DirectBarterActionPlan> Barter = new(
            DecodeReferenceChoice(DirectBarterChoice.Decode), static choice => choice.Encode(), PersonalBarterCapabilities,
            static (world, actor, choice, _) => MarketSystem.PlanDirectBarter(world.MarketBarterWorld, actor, choice, null, null),
            static (world, plan, policy) => MarketSystem.CheckDirectBarter(world.MarketBarterWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedDirectBarter(world.MarketBarterWorld, plan); return true; });

        private static ChoiceDecoder<MarketActionRequest> DecodeFamily(MarketActionFamily family) =>
            DecodeReferenceChoice(candidate => MarketActionRequest.Decode(candidate) is { } request && request.Family == family ? request : null);

        internal static readonly ActionDefinition<MarketActionRequest, MarketOccupancyActionPlan> Occupancy = new(
            DecodeFamily(MarketActionFamily.Occupancy), static request => request.Encode(), MarketSystem.MarketOccupancyCapabilities(false),
            static (world, actor, request, _) => world.PlanMarketRequest<MarketOccupancyActionPlan>(actor, request),
            static (world, plan, policy) => MarketSystem.CheckMarketOccupancy(world.MarketStallWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedMarketOccupancy(world.MarketStallWorld, plan); return true; },
            PlanCapabilities: static plan => MarketSystem.MarketOccupancyCapabilities(plan.Choice.Leave));

        internal static readonly ActionDefinition<MarketActionRequest, MarketStockActionPlan> Stock = new(
            DecodeFamily(MarketActionFamily.Stock), static request => request.Encode(), PersonalBarterCapabilities,
            static (world, actor, request, _) => world.PlanMarketRequest<MarketStockActionPlan>(actor, request),
            static (world, plan, policy) => MarketSystem.CheckMarketStock(world.MarketStallWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedMarketStock(world.MarketStallWorld, plan); return true; },
            PlanCapabilities: MarketStockCapabilities);

        internal static readonly ActionDefinition<MarketActionRequest, MarketExchangeActionPlan> Exchange = new(
            DecodeFamily(MarketActionFamily.Exchange), static request => request.Encode(), MarketSystem.MarketExchangeCapabilities(MarketExchangeKind.Buy),
            static (world, actor, request, _) => world.PlanMarketRequest<MarketExchangeActionPlan>(actor, request),
            static (world, plan, policy) => MarketSystem.CheckMarketExchange(world.MarketStallWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedMarketExchange(world.MarketStallWorld, plan); return true; },
            PlanCapabilities: static plan => MarketSystem.MarketExchangeCapabilities(plan.Choice.Kind));
    }

    private void OfferDirectBarter(List<CognitionCandidate> candidates, DirectBarterActionPlan? plan, string text, int priority)
    {
        if (plan is not null && CheckActionPlan(plan).Allowed) candidates.Add(new(plan.CandidateId, text, priority) { ActionPlan = plan });
    }

    private void AddTradeCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (NeedsUrgentWarmth(agents[actor]))
        {
            return;
        }
        foreach (var offer in society.Checkpoint.Inventory.Offers.Where(offer => offer.State == DirectBarterState.Open &&
                     offer.Id.Value.StartsWith(global::ClankerWorld.Simulation.World.MarketSystem.DirectBarterPrefix, StringComparison.Ordinal) &&
                     offer.ExpiryTick >= WorldTick && (offer.FirstPartyId == actor || offer.SecondPartyId == actor)))
        {
            if (offer.AcceptedBy.Contains(actor, StringComparer.Ordinal))
            {
                OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Wait, offer.Id), null, offer),
                    "Meet the other trader and wait for their answer.", 12);
                OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Decline, offer.Id), null, offer),
                    "Withdraw the pending exchange and release both reserved items.", 110);
                continue;
            }
            var giveId = offer.FirstPartyId == actor ? offer.FirstLotId : offer.SecondLotId;
            var takeId = offer.FirstPartyId == actor ? offer.SecondLotId : offer.FirstLotId;
            var give = society.Checkpoint.Inventory.GetLot(giveId);
            var take = society.Checkpoint.Inventory.GetLot(takeId);
            var useful = MarketSystem.WantsTradeItem(MarketBarterWorld, actor, take) && (!IsEdibleFood(give.ItemKind) || agents[actor].FullnessBasisPoints >= NeedThresholds.TradeFoodReserveFullness);
            OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Accept, offer.Id), null, offer),
                $"Accept exchange: give one {give.ItemKind}, receive one {take.ItemKind}.", useful ? 12 : 60);
            OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Decline, offer.Id), null, offer),
                "Decline this exchange and release both reserved items.", useful ? 60 : 12);
        }
        foreach (var other in agents.Keys.Where(other => other != actor)
                     .OrderByDescending(other => CommunitySystem.TrustScore(this.CommunitySkillWorld, actor, other)).ThenBy(other => other, StringComparer.Ordinal))
        {
            if (MarketSystem.TradeOpportunity(MarketBarterWorld, actor, other, null, null) is { } trade)
            {
                OfferDirectBarter(candidates, MarketSystem.PlanDirectBarter(MarketBarterWorld, actor, new(DirectBarterKind.Propose, other), trade, null),
                    $"Offer {society.Checkpoint.GetAgent(other).Name} one {trade.Give.ItemKind} for one {trade.Take.ItemKind}; they may refuse.", 14);
            }
        }
    }
}
