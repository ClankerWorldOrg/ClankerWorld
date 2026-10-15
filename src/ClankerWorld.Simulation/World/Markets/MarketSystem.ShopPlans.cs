using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal static ActorCapabilityPolicy ShopCapabilities => new(ActionLifeStages.Adults, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true);
    internal static bool IsLooseShopLot(InventoryLot lot, bool includeGround = false) => lot.ContainerLotId is null &&
        !InventoryContainerRules.IsContainer(lot.ItemKind) && lot.DeliveryBuildingId is null && (includeGround || lot.GroundPosition is null) &&
        lot.ConditionBasisPoints > 0 && lot.FreshnessBasisPoints > 0;

    internal static HashSet<string> BestUsableToolIds(InventoryCheckpoint inventory, string actor) =>
        ToolProgressionRules.All.Select(tool => tool.Family).Distinct()
            .Select(family => ToolProgressionRules.BestUsableTool(inventory, actor, family)?.Id)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

    internal static bool MayRespondAtShop(IMarketShopPlanningWorld world, string actor, ShopTradeState trade) => world.IsAdult(actor) &&
        world.HouseholdFor(actor) == trade.SellerHouseholdId &&
        world.Buildings.Any(building => building.InstanceId == trade.BuildingInstanceId &&
            building.HouseholdId == trade.SellerHouseholdId && building.Position == trade.Position);

    internal static bool ShopBuyerWants(IMarketShopPlanningWorld world, string actor, InventoryLot lot) =>
        world.ShopBuyerWantsWithoutRestaurant(actor, lot) || world.RestaurantInputDemand(actor, lot.ItemKind) > 0;

    internal static bool ShopPaymentUseful(IMarketShopPlanningWorld world, PlacedBuilding building, InventoryLot payment) =>
        FoodRules.IsEdible(payment.ItemKind) || world.Content.Recipes.Any(recipe =>
            recipe.WorkstationBuildingId == building.DefinitionId &&
            recipe.Inputs.Any(input => input.ResourceId == payment.ItemKind)) ||
        world.Content.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
            .Tags.Contains("blacksmith", StringComparer.Ordinal) && OrnamentContent.IsOrnament(payment.ItemKind) ||
        world.Content.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)
            .Tags.Contains("store", StringComparer.Ordinal);

    internal static (int Goods, int Payment) ShopQuoteAmounts(IMarketShopPlanningWorld world, PlacedBuilding building, InventoryLot goods, InventoryLot payment)
    {
        // Trial barter terms follow an on-site recipe when the offered payment
        // is one of its inputs. Fresh Farmhouse produce is offered in pairs.
        var recipe = world.Content.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.DefinitionId &&
                recipe.Outputs.Any(output => output.ResourceId == goods.ItemKind) &&
                recipe.Inputs.Any(input => input.ResourceId == payment.ItemKind))
            .OrderBy(recipe => recipe.CanonicalId, StringComparer.Ordinal).FirstOrDefault();
        return recipe is not null
            ? (recipe.Outputs.Single(output => output.ResourceId == goods.ItemKind).Amount,
                recipe.Inputs.Single(input => input.ResourceId == payment.ItemKind).Amount)
            : (ShopRules.KindOf(world.Content.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId)) == "farmhouse" &&
                FoodRules.IsEdible(goods.ItemKind) ? 2 : 1, 1);
    }

    internal bool ShopReceivingSpace(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building, int goods, int payment)
    {
        var inventory = world.Society.Inventory;
        var equipment = world.PhysicalAgents.Living[buyer].Equipment;
        return PersonalEquipmentRules.CarriedQuantity(inventory, buyer, equipment) - payment + goods +
                ReservedShopCarrySpace(world.Shops, buyer) <= PersonalEquipmentRules.Capacity(inventory, buyer, equipment) + world.HorseCargoCapacity(buyer) &&
            payment <= world.DestinationRoom(inventory, building.InstanceId, outgoingQuantity: goods);
    }

    internal static IEnumerable<InventoryLot> ShopPaymentLots(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building)
    {
        var inventory = world.Society.Inventory;
        var protectedToolIds = BestUsableToolIds(inventory, buyer);
        return world.SpendingGoods(buyer, GoodsUse.Spend, [buyer], inventory: inventory).Select(match => match.Lot)
            .Where(lot => PersonalEquipmentRules.IsCarried(lot, buyer) && IsLooseShopLot(lot) &&
                !protectedToolIds.Contains(lot.Id) && !ShopBuyerWants(world, buyer, lot) &&
                ShopPaymentUseful(world, building, lot) &&
                !PersonalEquipmentRules.IsSelected(world.PhysicalAgents.Living[buyer].Equipment, lot.Id) &&
                !AgentKnowledgeRules.IsArtifactKind(lot.ItemKind));
    }

    internal static bool MayVisitShop(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building)
    {
        if (!world.IsAdult(buyer) || world.NeedsUrgentWarmth(world.PhysicalAgents.Living[buyer]) ||
            building.HouseholdId is not { } seller || seller == world.HouseholdFor(buyer) ||
            !world.Society.Households.Any(household => household.Id == seller) ||
            !world.PhysicalAgents.Living.Keys.Any(actor => world.IsAdult(actor) && world.HouseholdFor(actor) == seller) ||
            world.IsWithinInteractionRange(world.PhysicalAgents.Living[buyer].Position, building.Position, world.ResourceInteractionRange) ||
            world.Society.Inventory.Offers.Any(offer => offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == buyer || offer.SecondPartyId == buyer))) return false;
        return true;
    }

    internal static bool RestaurantIngredientShopTrip(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building)
    {
        if (!MayVisitShop(world, buyer, building) || world.HouseholdFor(buyer) is not { } householdId) return false;
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) is not { } kind) return false;
        // A Town shop's location and kind can motivate a visit. Its private
        // stock and exact terms are inspected only after the buyer arrives.
        var inputs = world.Buildings.Where(site => site.HouseholdId == householdId &&
                world.Content.Buildings.Any(item => item.CanonicalId == site.DefinitionId &&
                    item.Tags.Contains("restaurant", StringComparer.Ordinal)))
            .SelectMany(site => world.Content.Recipes.Where(recipe => recipe.WorkstationBuildingId == site.DefinitionId)
                .SelectMany(recipe => recipe.Inputs)).Select(input => input.ResourceId).Distinct(StringComparer.Ordinal);
        return inputs.Any(itemKind => ShopRules.MaySell(kind, itemKind) && world.RestaurantInputDemand(buyer, itemKind) > 0) &&
            ShopPaymentLots(world, buyer, building).Any(payment => world.RestaurantInputDemand(buyer, payment.ItemKind, 1) ==
                world.RestaurantInputDemand(buyer, payment.ItemKind)) &&
            world.Reach(buyer, building.Position, ReachPurpose.ShopBuy, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0;
    }

    internal static bool RestaurantMealShopTrip(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building)
    {
        if (!MayVisitShop(world, buyer, building)) return false;
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) != "restaurant") return false;
        // The menu describes possible meals, never whether the private shelf
        // holds them. Exact available goods and payment are checked on site.
        var mealKinds = world.Content.Recipes.Where(recipe => recipe.WorkstationBuildingId == building.DefinitionId)
            .SelectMany(recipe => recipe.Outputs).Select(output => output.ResourceId).Distinct(StringComparer.Ordinal);
        return mealKinds.Any(kind => ShopRules.MaySell("restaurant", kind) && world.WantsTradeFoodKind(buyer, kind)) &&
            ShopPaymentLots(world, buyer, building).Any(payment => world.RestaurantInputDemand(buyer, payment.ItemKind, 1) ==
                world.RestaurantInputDemand(buyer, payment.ItemKind)) &&
            world.Reach(buyer, building.Position, ReachPurpose.ShopBuy, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0;
    }

    internal static bool ShopQuoteContext(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building) =>
        ActorCapabilities.Check(world.CapabilitiesFor(buyer), "shop_buy:",
            new(ActionLifeStages.Adults, AllowBoat: true, AllowUrgentFood: true, AllowUrgentWarmth: false, AllowActiveOrder: true)).Allowed &&
        building.HouseholdId is { } seller && seller != world.HouseholdFor(buyer) &&
        world.Society.Households.Any(household => household.Id == seller) &&
        world.PhysicalAgents.Living.Keys.Any(actor => world.IsAdult(actor) && world.HouseholdFor(actor) == seller) &&
        world.IsWithinInteractionRange(world.PhysicalAgents.Living[buyer].Position, building.Position, world.ResourceInteractionRange) &&
        !world.Society.Inventory.Offers.Any(offer => offer.State == DirectBarterState.Open &&
            (offer.FirstPartyId == buyer || offer.SecondPartyId == buyer));

    internal ActionCheck CheckSelectedShopQuote(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building, PlannedShopQuote quote, bool requestedGoods)
    {
        if (!ShopQuoteContext(world, buyer, building))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shop exchange is no longer available");
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) is not { } kind || !ShopRules.MaySell(kind, quote.Goods.ItemKind) ||
            !IsLooseShopLot(quote.Goods) || quote.Goods.ItemKind == quote.Payment.ItemKind ||
            !world.SpendingGoods(buyer, GoodsUse.Sell, [building.HouseholdId!], building: building.InstanceId)
                .Any(match => match.Lot == quote.Goods && match.Quantity >= quote.GoodsQuantity) ||
            !ShopPaymentLots(world, buyer, building).Any(lot => lot == quote.Payment) ||
            world.AvailableLotQuantity(quote.Payment) < quote.PaymentQuantity ||
            ShopQuoteAmounts(world, building, quote.Goods, quote.Payment) != (quote.GoodsQuantity, quote.PaymentQuantity) ||
            !requestedGoods && (!ShopBuyerWants(world, buyer, quote.Goods) ||
                !world.ShopBuyerWantsWithoutRestaurant(buyer, quote.Goods) && quote.GoodsQuantity > world.RestaurantInputDemand(buyer, quote.Goods.ItemKind)) ||
            world.RestaurantInputDemand(buyer, quote.Payment.ItemKind, quote.PaymentQuantity) > world.RestaurantInputDemand(buyer, quote.Payment.ItemKind))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected goods or payment are no longer available");
        return ShopReceivingSpace(world, buyer, building, quote.GoodsQuantity, quote.PaymentQuantity)
            ? new() : ActionCheck.Refuse(ActionRefusalReason.Capacity, "there is no room for the selected shop exchange");
    }

    internal ShopQuote? ShopOpportunity(IMarketShopPlanningWorld world, string buyer, PlacedBuilding building, string? exactGoodsLotId = null, bool requestedGoods = false)
    {
        if (!ShopQuoteContext(world, buyer, building)) return null;
        var definition = world.Content.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
        if (ShopRules.KindOf(definition) is not { } kind) return null;
        foreach (var goods in world.SpendingGoods(buyer, GoodsUse.Sell, [building.HouseholdId!], building: building.InstanceId)
                     .Select(match => match.Lot).Where(lot => IsLooseShopLot(lot) && ShopRules.MaySell(kind, lot.ItemKind) &&
                     (exactGoodsLotId is null || lot.Id == exactGoodsLotId) && (requestedGoods || ShopBuyerWants(world, buyer, lot)))
                     .OrderBy(lot => lot.Id, StringComparer.Ordinal))
        {
            // Payment comes from the buyer's own goods, never borrowed household ones.
            foreach (var payment in ShopPaymentLots(world, buyer, building).Where(lot => lot.ItemKind != goods.ItemKind)
                .OrderBy(lot => lot.Id, StringComparer.Ordinal))
            {
                var amounts = ShopQuoteAmounts(world, building, goods, payment);
                if (CheckSelectedShopQuote(world, buyer, building, new(goods, amounts.Goods, payment, amounts.Payment), requestedGoods).Allowed)
                    return new(goods, amounts.Goods, payment, amounts.Payment);
            }
        }
        return null;
    }

    internal ShopBuyActionPlan? PlanShopBuy(IMarketShopPlanningWorld world, string actor, PlacedBuilding building, ShopQuote? selected = null,
        ShopBuyKind? selectedKind = null)
    {
        var quote = selected ?? (selectedKind is null ? ShopOpportunity(world, actor, building) : null);
        var kind = quote is not null ? ShopBuyKind.Quote : selectedKind ??
            (RestaurantIngredientShopTrip(world, actor, building) ? ShopBuyKind.IngredientsTrip :
                RestaurantMealShopTrip(world, actor, building) ? ShopBuyKind.MealTrip : (ShopBuyKind?)null);
        return kind is null || building.HouseholdId is null ? null : new(actor, building.InstanceId, building.HouseholdId,
            building.DefinitionId, building.Position, kind.Value,
            quote is null ? null : new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity),
            world.Reach(actor, building.Position, ReachPurpose.ShopBuy, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal ActionCheck CheckShopBuy(IMarketShopPlanningWorld world, ShopBuyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ShopCapabilities);
        if (!capability.Allowed) return capability;
        if (world.Buildings.SingleOrDefault(building => building.InstanceId == plan.BuildingId) is not { } shop ||
            shop.HouseholdId != plan.SellerHouseholdId || shop.DefinitionId != plan.DefinitionId || shop.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shop, owner or site has changed");
        if (plan.Kind != ShopBuyKind.Quote)
            return (plan.Kind == ShopBuyKind.IngredientsTrip ? RestaurantIngredientShopTrip(world, plan.Actor, shop) : RestaurantMealShopTrip(world, plan.Actor, shop))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shop trip has ended or is no longer needed");
        var quote = CurrentSelectedShopQuote(world, plan);
        return quote is null ? ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained shop goods or payment are unavailable") :
            CheckSelectedShopQuote(world, plan.Actor, shop, quote, requestedGoods: false);
    }

    internal static PlannedShopQuote? CurrentSelectedShopQuote(IMarketShopPlanningWorld world, ShopBuyActionPlan plan)
    {
        if (plan.Quote is not { } quote || world.RecheckGoods(new(GoodsUse.Sell, plan.Actor, GoodsOwners.One(plan.SellerHouseholdId),
                GoodsKinds.One(quote.Goods.ItemKind), AtBuilding: plan.BuildingId), quote.Goods.Id) is not { Lot: var goods } ||
            world.RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(quote.Payment.ItemKind)),
                quote.Payment.Id) is not { Lot: var payment }) return null;
        return new(goods, quote.GoodsQuantity, payment, quote.PaymentQuantity);
    }

    internal void ApplyCheckedShopBuy(IMarketShopPlanningWorld world, ShopBuyActionPlan plan)
    {
        if (plan.Kind != ShopBuyKind.Quote) { world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.Destination, "shop_buy", world.ResourceInteractionRange); return; }
        var shop = world.Buildings.Single(item => item.InstanceId == plan.BuildingId);
        var quote = CurrentSelectedShopQuote(world, plan)!;
        _ = OpenShopQuote(world.Shops, plan.Actor, shop, new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity));
    }

    internal ActionCheck CheckShopResponse(IMarketShopPlanningWorld world, ShopResponseActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ShopCapabilities);
        if (!capability.Allowed) return capability;
        if (shopTrades.SingleOrDefault(trade => trade.OfferId == plan.Offer.Id) != plan.Trade ||
            plan.Actor != plan.Trade.BuyerId && !MayRespondAtShop(world, plan.Actor, plan.Trade) || !ShopResponseOfferIsCurrent(world.Shops, plan))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the original shop offer or their personal response authority has changed");
        return new();
    }

    internal void ApplyCheckedShopResponse(IMarketShopPlanningWorld world, ShopResponseActionPlan plan)
    {
        ApplyRetainedShopResponse(world.Shops, plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan);
    }
}
