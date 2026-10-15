using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private const string ShopTradePrefix = "shop-trade:";
    private List<ShopTradeState> shopTrades = [];

    public IReadOnlyList<ShopTradeState> ShopTrades => shopTrades
        .OrderBy(trade => (string?)trade.OfferId, StringComparer.Ordinal).ToArray();

    private IEnumerable<(ShopTradeState Trade, DirectBarterOffer Offer)> OpenShopTrades(InventoryCheckpoint? inventory = null) =>
        shopTrades.Select(trade => (Trade: trade, Offer: (inventory ?? society.Checkpoint.Inventory).Offers
                .Single(offer => offer.Id == trade.OfferId)))
            .Where(pair => pair.Offer.State == DirectBarterState.Open);

    private int ReservedShopCarrySpace(string actor, InventoryCheckpoint? inventory = null) => OpenShopTrades(inventory)
        .Where(pair => pair.Trade.BuyerId == actor)
        .Sum(pair => Math.Max(0, pair.Offer.FirstQuantity - pair.Offer.SecondQuantity));

    private int ReservedShopStorageSpace(string buildingId, InventoryCheckpoint? inventory = null) => OpenShopTrades(inventory)
        .Where(pair => pair.Trade.BuildingInstanceId == buildingId)
        .Sum(pair => Math.Max(0, pair.Offer.SecondQuantity - pair.Offer.FirstQuantity));

    private static bool IsLooseShopLot(InventoryLot lot, bool includeGround = false) => lot.ContainerLotId is null &&
        !InventoryContainerRules.IsContainer(lot.ItemKind) && lot.DeliveryBuildingId is null && (includeGround || lot.GroundPosition is null) &&
        lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0;

    private bool MayRespondAtShop(string actor, ShopTradeState trade) => IsAdult(actor) &&
        HouseholdFor(actor) == trade.SellerHouseholdId &&
        worldSimulation.Buildings.Any(building => building.InstanceId == trade.BuildingInstanceId &&
            building.HouseholdId == trade.SellerHouseholdId && building.Position == trade.Position);

    private bool ShopBuyerWants(string actor, InventoryLot lot) =>
        ShopBuyerWantsWithoutRestaurant(actor, lot) || RestaurantInputDemand(actor, lot.ItemKind) > 0;

    private int RestaurantInputDemand(string actor, string itemKind, int spentPersonalQuantity = 0)
    {
        if (HouseholdFor(actor) is not { } householdId) return 0;
        var inventory = society.Checkpoint.Inventory;
        var restaurants = worldSimulation.Buildings.Where(building => building.HouseholdId == householdId &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                definition.Tags.Contains("restaurant", StringComparer.Ordinal))).ToArray();
        var missing = restaurants.Sum(building => Math.Max(0, WorkstationInputTarget(building, itemKind) -
            WorkstationOnsiteQuantity(inventory, building, itemKind) -
            WorkstationIncomingQuantity(inventory, building, itemKind)));
        if (missing == 0) return 0;

        // Personal stock is usable only by its actual owner; other members'
        // uncommitted possessions are not a household promise.
        var carried = inventory.Lots.Where(lot => lot.OwnerId == actor && lot.ItemKind == itemKind &&
                lot.DeliveryBuildingId is null && PersonalEquipmentRules.IsPhysicallyCarried(inventory, lot, actor))
            .Where(lot => restaurants.Any(building => WorkstationInputTarget(building, itemKind) >
                    WorkstationOnsiteQuantity(inventory, building, itemKind) +
                    WorkstationIncomingQuantity(inventory, building, itemKind) &&
                DestinationRoom(inventory, building.InstanceId) > 0 &&
                (lot.ContainerLotId is not { } containerId ||
                 !HasActiveContainerReservation(inventory, containerId) &&
                 !UnusableDeliveryStock(inventory, inventory.GetLot(containerId)) &&
                 ContainerFamilyQuantity(inventory, containerId) <= DestinationRoom(inventory, building.InstanceId))))
            .Sum(lot => UsableWorkstationQuantity(inventory, lot));
        var householdStock = restaurants.Where(building => WorkstationInputTarget(building, itemKind) >
                WorkstationOnsiteQuantity(inventory, building, itemKind) + WorkstationIncomingQuantity(inventory, building, itemKind))
            .SelectMany(building => FindGoods(DeliveryGoodsRequest(actor, householdId, itemKind, building.InstanceId)).Matches
                .Where(match => WorkstationPickupQuantity(actor, inventory, match.Root, building.InstanceId, int.MaxValue) > 0))
            .Select(match => match.Lot).DistinctBy(lot => lot.Id)
            .Where(lot => lot.CarrierId is null &&
                (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                    building.InstanceId == lot.StorageBuildingId && worldContent.Buildings.Any(definition =>
                        definition.CanonicalId == building.DefinitionId &&
                        definition.Tags.Any(tag => tag is "house" or "silo" or "farmhouse" or "restaurant")))))
            .GroupBy(lot => lot.StorageBuildingId)
            // Reserve the source's target once across all of its lots.
            .Sum(group => Math.Min(group.Sum(lot => UsableWorkstationQuantity(inventory, lot)),
                WorkstationSourceSurplus(inventory, group.First())));
        var purchases = OpenShopTrades(inventory).Where(pair => pair.Offer.ExpiryTick >= WorldTick &&
                pair.Trade.GoodsKind == itemKind &&
                society.Checkpoint.Agents.Any(person => person.Id == pair.Trade.BuyerId &&
                    person.HouseholdId == householdId && person.Status == SocietyAgentStatus.Active) &&
                pair.Offer.AcceptedBy.Contains(pair.Trade.BuyerId, StringComparer.Ordinal))
            .Where(pair => inventory.Lots.Any(lot => lot.Id == pair.Offer.FirstLotId &&
                    lot.OwnerId == pair.Trade.SellerHouseholdId &&
                    lot.StorageBuildingId == pair.Trade.BuildingInstanceId &&
                    lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0 &&
                    lot.Quantity >= pair.Offer.FirstQuantity) &&
                inventory.Reservations.Any(reservation => reservation.Id == DerivedId.Create(pair.Offer.Id + ":first") &&
                    reservation.State == InventoryReservationState.Reserved &&
                    reservation.Quantity >= pair.Offer.FirstQuantity))
            .Sum(pair => pair.Offer.FirstQuantity);
        return Math.Max(0, missing - Math.Max(0, carried - spentPersonalQuantity) - householdStock - purchases);
    }

    private bool ShopBuyerWantsWithoutRestaurant(string actor, InventoryLot lot)
    {
        if (MedicalSupplyWanted(actor, lot.ItemKind)) return true;
        if (WantsFieldPlantingStock(actor, lot.ItemKind)) return true;
        if (WantsOrnamentInput(actor, lot.ItemKind)) return true;
        var inventory = society.Checkpoint.Inventory;
        if (ToolProgressionRules.Find(lot.ItemKind) is { } offeredTool)
        {
            var carriedTool = ToolProgressionRules.BestUsableTool(inventory, actor, offeredTool.Family);
            return carriedTool is null ||
                ToolProgressionRules.Find(carriedTool.ItemKind)!.Tier < offeredTool.Tier;
        }
        if (PersonalEquipmentRules.IsGarment(lot.ItemKind))
        {
            var weather = WeatherAt(agents[actor].Position);
            var bestProtection = inventory.Lots.Where(item =>
                    ToolProgressionRules.IsTopLevelCarriedLot(item, actor) &&
                    PersonalEquipmentRules.IsGarment(item.ItemKind) && AvailableLotQuantity(item) > 0)
                .Select(item => PersonalEquipmentRules.Protection(item, weather)).DefaultIfEmpty(0).Max();
            return PersonalEquipmentRules.Protection(lot, weather) > bestProtection;
        }
        if (WantsTradeItem(actor, lot)) return true;
        if (lot.ItemKind == "iron")
        {
            var household = HouseholdFor(actor);
            // Trial reserve for real iron-tool work, not a tool to equip.
            return HouseholdBuildingWithTag(household, "blacksmith") is not null &&
                HoldingsTotal(new[] { actor, household }.OfType<string>(), GoodsKinds.One("iron")) < 2;
        }
        if (PersonalEquipmentRules.IsCarryAid(lot.ItemKind))
            return PersonalEquipmentRules.Capacity(society.Checkpoint.Inventory, actor, agents[actor].Equipment) <
                (lot.ItemKind == "leather_sack" ? 32 : lot.ItemKind == "sack" ? PersonalEquipmentRules.SackCapacity : PersonalEquipmentRules.BasketCapacity);
        return false;
    }

    private bool ShopPaymentUseful(PlacedBuilding building, InventoryLot payment) =>
        IsEdibleFood(payment.ItemKind) || worldContent.Recipes.Any(recipe =>
            recipe.WorkstationBuildingId == building.DefinitionId &&
            recipe.Inputs.Any(input => input.ResourceId == payment.ItemKind)) ||
        worldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
            .Tags.Contains("blacksmith", StringComparer.Ordinal) && OrnamentContent.IsOrnament(payment.ItemKind) ||
        worldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
            .Tags.Contains("store", StringComparer.Ordinal);

    private sealed record ShopQuote(InventoryLot Goods, int GoodsQuantity, InventoryLot Payment, int PaymentQuantity);

    private (int Goods, int Payment) ShopQuoteAmounts(PlacedBuilding building, InventoryLot goods, InventoryLot payment)
    {
        // Trial barter terms follow an on-site recipe when the offered payment
        // is one of its inputs. Fresh Farmhouse produce is offered in pairs.
        var recipe = worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.DefinitionId &&
                recipe.Outputs.Any(output => output.ResourceId == goods.ItemKind) &&
                recipe.Inputs.Any(input => input.ResourceId == payment.ItemKind))
            .OrderBy(recipe => recipe.CanonicalId, StringComparer.Ordinal).FirstOrDefault();
        return recipe is not null
            ? (recipe.Outputs.Single(output => output.ResourceId == goods.ItemKind).Amount,
                recipe.Inputs.Single(input => input.ResourceId == payment.ItemKind).Amount)
            : (ShopRules.KindOf(worldContent.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)) == "farmhouse" &&
                IsEdibleFood(goods.ItemKind) ? 2 : 1, 1);
    }

    private bool ShopReceivingSpace(string buyer, PlacedBuilding building, int goods, int payment)
    {
        var inventory = society.Checkpoint.Inventory;
        var equipment = agents[buyer].Equipment;
        return PersonalEquipmentRules.CarriedQuantity(inventory, buyer, equipment) - payment + goods +
                ReservedShopCarrySpace(buyer) <= PersonalEquipmentRules.Capacity(inventory, buyer, equipment) + HorseCargoCapacity(buyer) &&
            payment <= DestinationRoom(inventory, building.InstanceId, outgoingQuantity: goods);
    }

    private static HashSet<string> BestUsableToolIds(InventoryCheckpoint inventory, string actor) =>
        ToolProgressionRules.All.Select(tool => tool.Family).Distinct()
            .Select(family => ToolProgressionRules.BestUsableTool(inventory, actor, family)?.Id)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

    private IEnumerable<InventoryLot> ShopPaymentLots(string buyer, PlacedBuilding building)
    {
        var inventory = society.Checkpoint.Inventory;
        var protectedToolIds = BestUsableToolIds(inventory, buyer);
        return SpendingGoods(buyer, GoodsUse.Spend, [buyer], inventory: inventory).Select(match => match.Lot)
            .Where(lot => PersonalEquipmentRules.IsCarried(lot, buyer) && IsLooseShopLot(lot) &&
                !protectedToolIds.Contains(lot.Id) && !ShopBuyerWants(buyer, lot) &&
                ShopPaymentUseful(building, lot) &&
                !PersonalEquipmentRules.IsSelected(agents[buyer].Equipment, lot.Id) &&
                !AgentKnowledgeRules.IsArtifactKind(lot.ItemKind));
    }

    private bool MayVisitShop(string buyer, PlacedBuilding building)
    {
        if (!IsAdult(buyer) || NeedsUrgentWarmth(agents[buyer]) ||
            building.HouseholdId is not { } seller || seller == HouseholdFor(buyer) ||
            !society.Checkpoint.Households.Any(household => household.Id == seller) ||
            !agents.Keys.Any(actor => IsAdult(actor) && HouseholdFor(actor) == seller) ||
            IsWithinInteractionRange(agents[buyer].Position, building.Position, ResourceInteractionRange) ||
            society.Checkpoint.Inventory.Offers.Any(offer => offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == buyer || offer.SecondPartyId == buyer))) return false;
        return true;
    }

    private bool RestaurantIngredientShopTrip(string buyer, PlacedBuilding building)
    {
        if (!MayVisitShop(buyer, building) || HouseholdFor(buyer) is not { } householdId) return false;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) is not { } kind) return false;
        // A Town shop's location and kind can motivate a visit. Its private
        // stock and exact terms are inspected only after the buyer arrives.
        var inputs = worldSimulation.Buildings.Where(site => site.HouseholdId == householdId &&
                worldContent.Buildings.Any(item => item.CanonicalId == site.DefinitionId &&
                    item.Tags.Contains("restaurant", StringComparer.Ordinal)))
            .SelectMany(site => worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == site.DefinitionId)
                .SelectMany(recipe => recipe.Inputs)).Select(input => input.ResourceId).Distinct(StringComparer.Ordinal);
        return inputs.Any(itemKind => ShopRules.MaySell(kind, itemKind) && RestaurantInputDemand(buyer, itemKind) > 0) &&
            ShopPaymentLots(buyer, building).Any(payment => RestaurantInputDemand(buyer, payment.ItemKind, 1) ==
                RestaurantInputDemand(buyer, payment.ItemKind)) &&
            FindUnoccupiedRoute(buyer, agents[buyer].Position, building.Position, ResourceInteractionRange).Count > 0;
    }

    private bool RestaurantMealShopTrip(string buyer, PlacedBuilding building)
    {
        if (!MayVisitShop(buyer, building)) return false;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) != "restaurant") return false;
        // The menu describes possible meals, never whether the private shelf
        // holds them. Exact available goods and payment are checked on site.
        var mealKinds = worldContent.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.DefinitionId)
            .SelectMany(recipe => recipe.Outputs).Select(output => output.ResourceId).Distinct(StringComparer.Ordinal);
        return mealKinds.Any(kind => ShopRules.MaySell("restaurant", kind) && WantsTradeFoodKind(buyer, kind)) &&
            ShopPaymentLots(buyer, building).Any(payment => RestaurantInputDemand(buyer, payment.ItemKind, 1) ==
                RestaurantInputDemand(buyer, payment.ItemKind)) &&
            FindUnoccupiedRoute(buyer, agents[buyer].Position, building.Position, ResourceInteractionRange).Count > 0;
    }

    private ShopQuote? ShopOpportunity(string buyer, PlacedBuilding building, string? exactGoodsLotId = null, bool requestedGoods = false)
    {
        var inventory = society.Checkpoint.Inventory;
        if (!IsAdult(buyer) || NeedsUrgentWarmth(agents[buyer]) ||
            building.HouseholdId is not { } seller || seller == HouseholdFor(buyer) ||
            !society.Checkpoint.Households.Any(household => household.Id == seller) ||
            !agents.Keys.Any(actor => IsAdult(actor) && HouseholdFor(actor) == seller) ||
            !IsWithinInteractionRange(agents[buyer].Position, building.Position, ResourceInteractionRange) ||
            inventory.Offers.Any(offer => offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == buyer || offer.SecondPartyId == buyer)))
            return null;
        var definition = worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) is not { } kind) return null;
        foreach (var goods in SpendingGoods(buyer, GoodsUse.Sell, [seller], building: building.InstanceId,
                     inventory: inventory).Select(match => match.Lot)
                     .Where(lot => IsLooseShopLot(lot) && ShopRules.MaySell(kind, lot.ItemKind) &&
                     (exactGoodsLotId is null || lot.Id == exactGoodsLotId) && (requestedGoods || ShopBuyerWants(buyer, lot))).OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            // Payment comes from the buyer's own goods, never borrowed household ones.
            foreach (var payment in ShopPaymentLots(buyer, building).Where(lot => lot.ItemKind != goods.ItemKind)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var amounts = ShopQuoteAmounts(building, goods, payment);
                if (!requestedGoods && !ShopBuyerWantsWithoutRestaurant(buyer, goods) &&
                    amounts.Goods > RestaurantInputDemand(buyer, goods.ItemKind)) continue;
                // A satisfied input reserve can become short after payment:
                // protect the exact quoted quantity, not just a desire flag.
                if (RestaurantInputDemand(buyer, payment.ItemKind, amounts.Payment) >
                    RestaurantInputDemand(buyer, payment.ItemKind)) continue;
                if (AvailableLotQuantity(goods) >= amounts.Goods && AvailableLotQuantity(payment) >= amounts.Payment &&
                    ShopReceivingSpace(buyer, building, amounts.Goods, amounts.Payment))
                    return new(goods, amounts.Goods, payment, amounts.Payment);
            }
        }
        return null;
    }

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

    private void AddShopCandidates(List<CognitionCandidate> candidates, string actor)
    {
        if (!IsAdult(actor) || NeedsUrgentWarmth(agents[actor])) return;
        if (!NeedsUrgentFood(agents[actor])) AddStoreStockCandidate(candidates, actor);
        foreach (var (trade, offer) in OpenShopTrades().OrderBy(pair => pair.Trade.OfferId.Value, StringComparer.Ordinal))
        {
            var buyer = trade.BuyerId == actor;
            if (!buyer && !MayRespondAtShop(actor, trade)) continue;
            var goods = society.Checkpoint.Inventory.GetLot(offer.FirstLotId);
            var payment = society.Checkpoint.Inventory.GetLot(offer.SecondLotId);
            candidates.Add(new("shop_continue:" + offer.Id, buyer
                ? $"Wait at the shop to receive {offer.FirstQuantity} {goods.ItemKind} for {offer.SecondQuantity} {payment.ItemKind}."
                : $"Meet the customer at the shop and accept {offer.SecondQuantity} {payment.ItemKind} for {offer.FirstQuantity} {goods.ItemKind}.",
                12, trade.BuildingInstanceId));
            candidates.Add(new("shop_cancel:" + offer.Id,
                "Cancel this exchange and release the goods and receiving space.", 65, trade.BuildingInstanceId));
        }
        foreach (var building in worldSimulation.Buildings.OrderBy(building => building.InstanceId, StringComparer.Ordinal))
            if (ShopOpportunity(actor, building) is { } choice)
                candidates.Add(new("shop_buy:" + building.InstanceId,
                    $"Offer {choice.PaymentQuantity} {choice.Payment.ItemKind} for {choice.GoodsQuantity} {choice.Goods.ItemKind} stocked at this shop; the household may refuse.",
                    14, building.InstanceId));
            else if (RestaurantIngredientShopTrip(actor, building))
                candidates.Add(new("shop_buy:" + building.InstanceId,
                    "Visit this Town shop to ask about missing Restaurant ingredients; any offer is checked there.",
                    14, building.InstanceId));
            else if (RestaurantMealShopTrip(actor, building))
                candidates.Add(new("shop_buy:" + building.InstanceId,
                    "Visit this Restaurant to ask about a meal; any offer is checked there.",
                    14, building.InstanceId));
    }

    private void ApplyShopCandidate(string actor, AgentState state, string candidate)
    {
        if (candidate == "shop_stock_store")
        {
            StockStore(actor, state);
            return;
        }
        if (candidate.StartsWith("shop_buy:", StringComparison.Ordinal))
        {
            var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == candidate["shop_buy:".Length..]);
            if (building is null) return;
            if (!IsWithinInteractionRange(state.Position, building.Position, ResourceInteractionRange))
            {
                if (RestaurantIngredientShopTrip(actor, building) || RestaurantMealShopTrip(actor, building))
                    MoveToward(actor, state, building.Position, "shop_buy", ResourceInteractionRange);
                return;
            }
            if (ShopOpportunity(actor, building) is not { } choice) return;
            _ = OpenShopQuote(actor, building, choice);
            return;
        }
        var cancel = candidate.StartsWith("shop_cancel:", StringComparison.Ordinal);
        var offerId = candidate[(cancel ? "shop_cancel:".Length : "shop_continue:".Length)..];
        var trade = shopTrades.SingleOrDefault(item => item.OfferId == offerId);
        var offer = society.Checkpoint.Inventory.Offers.SingleOrDefault(item => item.Id == offerId);
        if (trade is null || offer is not { State: DirectBarterState.Open } ||
            (actor != trade.BuyerId && !MayRespondAtShop(actor, trade))) return;
        if (cancel)
        {
            CancelShopTrade(trade, offer, "One of the traders cancelled the exchange.");
            return;
        }
        if (ShopTradeFailure(trade, offer) is { } failure)
        {
            CancelShopTrade(trade, offer, failure);
            return;
        }
        if (!IsWithinInteractionRange(state.Position, trade.Position, ResourceInteractionRange))
        {
            MoveToward(actor, state, trade.Position, "shop_trade", ResourceInteractionRange);
            return;
        }
        if (actor == trade.BuyerId || !IsWithinInteractionRange(agents[trade.BuyerId].Position,
                trade.Position, ResourceInteractionRange)) return;
        ApplyInventoryTransition(inventory =>
        {
            var payment = inventory.GetLot(offer.SecondLotId);
            var paymentId = payment.Quantity == offer.SecondQuantity ? payment.Id : DerivedId.Create($"{payment.Id}#barter:{offer.Id}");
            var settled = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer.Id, offer.Revision, trade.SellerHouseholdId);
            return settled with
            {
                Lots = settled.Lots.Select(lot => lot.Id == paymentId
                    ? lot with { StorageBuildingId = trade.BuildingInstanceId } : lot).ToArray(),
            };
        });
        ReplaceShopTrade(trade with { SellerActorId = actor });
        AppendEvent(new ShopTradeCompletedEventPayload(actor, offer.Id));
    }

    private string OpenShopQuote(string actor, PlacedBuilding building, ShopQuote choice)
    {
        var id = ShopTradeOfferId(WorldTick, actor, building.InstanceId);
        // Quotes name exact actual quantities; future goods are never reserved.
        ApplyInventoryTransition(inventory => InventoryAuthority.AcceptDirectBarterOffer(
            InventoryAuthority.CreateDirectBarterOffer(inventory, new(Identity.Parse(id), 1,
                building.HouseholdId!, actor, choice.Goods.Id, choice.GoodsQuantity,
                choice.Payment.Id, choice.PaymentQuantity, WorldTick + 120)),
            id, 1, actor));
        shopTrades.Add(new(Identity.Parse(id), building.InstanceId, building.HouseholdId!, actor, building.Position, WorldTick,
            choice.Goods.ItemKind, choice.Payment.ItemKind));
        AppendEvent(new ShopTradeOfferedEventPayload(actor, id));
        return id;
    }

    private string? ShopTradeFailure(ShopTradeState trade, DirectBarterOffer offer)
    {
        if (offer.ExpiryTick < WorldTick) return "The offer ran out of time.";
        if (!agents.TryGetValue(trade.BuyerId, out var buyerState) || !IsAdult(trade.BuyerId))
            return "The buyer is no longer available.";
        var building = worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == trade.BuildingInstanceId);
        if (building is null || building.HouseholdId != trade.SellerHouseholdId || building.Position != trade.Position ||
            !agents.Keys.Any(actor => MayRespondAtShop(actor, trade)))
            return "The selling household no longer has access to this shop.";
        var inventory = society.Checkpoint.Inventory;
        var goods = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.FirstLotId);
        var payment = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.SecondLotId);
        if (goods is null || payment is null || !IsLooseShopLot(goods) || !IsLooseShopLot(payment) ||
            goods.OwnerId != trade.SellerHouseholdId || goods.StorageBuildingId != trade.BuildingInstanceId ||
            goods.GroundPosition is not null || !PersonalEquipmentRules.IsCarried(payment, trade.BuyerId) ||
            goods.Quantity < offer.FirstQuantity || payment.Quantity < offer.SecondQuantity ||
            new[] { DerivedId.Create(offer.Id + ":first"), DerivedId.Create(offer.Id + ":second") }.Any(id => !inventory.Reservations.Any(reservation =>
                reservation.Id == id && reservation.State == InventoryReservationState.Reserved)))
            return "The goods set aside for this exchange are no longer at the agreed place.";
        var kind = ShopRules.KindOf(worldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId));
        if (kind is null || !ShopRules.MaySell(kind, goods.ItemKind))
            return "This shop cannot sell the offered goods.";
        var equipment = buyerState.Equipment;
        var otherCarryReservations = ReservedShopCarrySpace(trade.BuyerId) -
            Math.Max(0, offer.FirstQuantity - offer.SecondQuantity);
        var afterCarry = PersonalEquipmentRules.CarriedQuantity(inventory, trade.BuyerId, equipment) -
            offer.SecondQuantity + offer.FirstQuantity + otherCarryReservations;
        var storageRoom = DestinationRoom(inventory, trade.BuildingInstanceId, outgoingQuantity: offer.FirstQuantity,
            releasedShopReservation: Math.Max(0, offer.SecondQuantity - offer.FirstQuantity));
        if (afterCarry > PersonalEquipmentRules.Capacity(inventory, trade.BuyerId, equipment) + HorseCargoCapacity(trade.BuyerId) ||
            offer.SecondQuantity > storageRoom)
            return "There is no longer enough room to receive the goods.";
        if (!IsWithinInteractionRange(buyerState.Position, trade.Position, ResourceInteractionRange) &&
            FindUnoccupiedRoute(trade.BuyerId, buyerState.Position, trade.Position, ResourceInteractionRange).Count == 0)
            return "The buyer can no longer reach the shop.";
        return null;
    }

    private void ReplaceShopTrade(ShopTradeState trade) =>
        shopTrades[shopTrades.FindIndex(item => item.OfferId == trade.OfferId)] = trade;

    private void CancelShopTrade(ShopTradeState trade, DirectBarterOffer offer, string reason)
    {
        if (offer.State == DirectBarterState.Open)
            ApplyInventoryTransition(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory,
                offer.Id, offer.Revision, offer.FirstPartyId));
        ReplaceShopTrade(trade with { CancellationReason = reason });
        AppendEvent(new ShopTradeCancelledEventPayload(null, trade.BuyerId, trade.OfferId));
    }

    private void MaintainShopTrades()
    {
        foreach (var trade in shopTrades.ToArray())
        {
            var offer = society.Checkpoint.Inventory.GetOffer(trade.OfferId);
            if (offer.State == DirectBarterState.Open && ShopTradeFailure(trade, offer) is { } failure)
                CancelShopTrade(trade, offer, failure);
            else if (offer.State == DirectBarterState.Cancelled && trade.CancellationReason is null)
                CancelShopTrade(trade, offer, offer.ExpiryTick < WorldTick
                    ? "The offer ran out of time." : "The exchange was interrupted.");
        }
        var retired = shopTrades.Where(trade => society.Checkpoint.Inventory.GetOffer(trade.OfferId).State != DirectBarterState.Open)
            .OrderByDescending(trade => trade.ProposedTick).ThenBy(trade => trade.OfferId.Value, StringComparer.Ordinal)
            .Skip(32).Select(trade => trade.OfferId.Value).ToHashSet(StringComparer.Ordinal);
        if (retired.Count == 0) return;
        shopTrades.RemoveAll(trade => retired.Contains(trade.OfferId));
        ApplyInventoryTransition(inventory => inventory with
        {
            Offers = inventory.Offers.Where(offer => !retired.Contains(offer.Id)).ToArray(),
            Reservations = inventory.Reservations.Where(reservation => !retired.Any(id =>
                reservation.Id == DerivedId.Create(id + ":first") || reservation.Id == DerivedId.Create(id + ":second"))).ToArray(),
        });
    }

    private static void ValidateShopTrades(IReadOnlyList<ShopTradeState>? trades,
        SocietyCheckpoint societyState, WorldMap map, long worldTick)
    {
        var inventory = societyState.Inventory;
        if (trades is null || trades.Any(trade => trade is null) || !trades.Select(trade => (string?)trade.OfferId).SequenceEqual(
                trades.Select(trade => (string?)trade.OfferId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The checkpoint is missing canonical shop trade records.");
        foreach (var trade in trades)
        {
            var offer = inventory.Offers.SingleOrDefault(item => item.Id == trade.OfferId);
            if (offer is null || !offer.Id.Value.StartsWith(ShopTradePrefix, StringComparison.Ordinal) ||
                offer.Id != ShopTradeOfferId(trade.ProposedTick, trade.BuyerId, trade.BuildingInstanceId) ||
                trade.ProposedTick < 0 || trade.ProposedTick > worldTick ||
                !BoundedText.IsValid(trade.BuildingInstanceId, 256, requireTrimmed: true) ||
                !societyState.Households.Any(household => household.Id == trade.SellerHouseholdId) ||
                !societyState.Agents.Any(person => person.Id == trade.BuyerId) ||
                !IdentityRules.Reference(trade.GoodsKind, int.MaxValue, requireTrimmed: true) || !IdentityRules.Reference(trade.PaymentKind, int.MaxValue, requireTrimmed: true) ||
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
