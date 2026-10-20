namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ShopBuyActionPlan? PlanShopBuy(string actor, PlacedBuilding building, ShopQuote? selected = null,
        ShopBuyKind? selectedKind = null)
    {
        var quote = selected ?? (selectedKind is null ? ShopOpportunity(actor, building) : null);
        var kind = quote is not null ? ShopBuyKind.Quote : selectedKind ??
            (RestaurantIngredientShopTrip(actor, building) ? ShopBuyKind.IngredientsTrip :
                RestaurantMealShopTrip(actor, building) ? ShopBuyKind.MealTrip : (ShopBuyKind?)null);
        return kind is null || building.HouseholdId is null ? null : new(actor, building.InstanceId, building.HouseholdId,
            building.DefinitionId, building.Position, kind.Value,
            quote is null ? null : new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity),
            Reach(actor, building.Position, ReachPurpose.ShopBuy, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckShopBuy(ShopBuyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? ShopCapabilities);
        if (!capability.Allowed) return capability;
        if (worldSimulation.Buildings.SingleOrDefault(building => building.InstanceId == plan.BuildingId) is not { } shop ||
            shop.HouseholdId != plan.SellerHouseholdId || shop.DefinitionId != plan.DefinitionId || shop.Position != plan.Destination)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shop, owner or site has changed");
        if (plan.Kind != ShopBuyKind.Quote)
            return (plan.Kind == ShopBuyKind.IngredientsTrip ? RestaurantIngredientShopTrip(plan.Actor, shop) : RestaurantMealShopTrip(plan.Actor, shop))
                ? new() : ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected shop trip has ended or is no longer needed");
        var quote = CurrentSelectedShopQuote(plan);
        return quote is null ? ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained shop goods or payment are unavailable") :
            CheckSelectedShopQuote(plan.Actor, shop, quote, requestedGoods: false);
    }

    private PlannedShopQuote? CurrentSelectedShopQuote(ShopBuyActionPlan plan)
    {
        if (plan.Quote is not { } quote || RecheckGoods(new(GoodsUse.Sell, plan.Actor, GoodsOwners.One(plan.SellerHouseholdId),
                GoodsKinds.One(quote.Goods.ItemKind), AtBuilding: plan.BuildingId), quote.Goods.Id) is not { Lot: var goods } ||
            RecheckGoods(new(GoodsUse.Spend, plan.Actor, GoodsOwners.One(plan.Actor), GoodsKinds.One(quote.Payment.ItemKind)),
                quote.Payment.Id) is not { Lot: var payment }) return null;
        return new(goods, quote.GoodsQuantity, payment, quote.PaymentQuantity);
    }

    private void ApplyShopBuy(ShopBuyActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedShopBuy(plan);
    }

    private void ApplyCheckedShopBuy(ShopBuyActionPlan plan)
    {
        if (plan.Kind != ShopBuyKind.Quote) { MoveToward(plan.Actor, agents[plan.Actor], plan.Destination, "shop_buy", ResourceInteractionRange); return; }
        var shop = worldSimulation.Buildings.Single(item => item.InstanceId == plan.BuildingId);
        var quote = CurrentSelectedShopQuote(plan)!;
        _ = OpenShopQuote(plan.Actor, shop, new(quote.Goods, quote.GoodsQuantity, quote.Payment, quote.PaymentQuantity));
    }
}
