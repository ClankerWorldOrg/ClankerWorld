namespace ClankerWorld.Simulation.World;

internal sealed partial class TownSystem
{
    internal LandSaleCollectionActionPlan? PlanLandSaleCollection(ITownWorld world, string actor, LandSaleCollectionChoice choice)
    {
        if (world.Towns.SingleOrDefault(town => town.Id == choice.TownId) is not { Council: not null } town ||
            town.LandRecords.Transfers.SingleOrDefault(request => LandTransferActionToken(request) == choice.TermsToken) is not { Price: { } price } request ||
            CivicBoard(world, town) is not { } board || LandSaleStock(world, actor, town, price) is not { } stock) return null;
        var quantity = Math.Min(price.Quantity - LandSaleCarriedQuantity(world, actor, price),
            Math.Min(LandSalePickupCapacity(world, actor, town, stock), world.MarketSurplus(actor, stock)));
        if (stock.ItemKind == "food") quantity = Math.Min(quantity, SharedFoodCollectionAllowance(world, actor));
        if (quantity <= 0) return null;
        var pickup = new PlannedEquipmentPickup(stock.Id, stock.OwnerId, stock.ItemKind,
            world.HouseholdStockPosition(stock), WorldRuntime.CivicSharedRules.HouseholdStockInteractionRange(stock));
        return new(actor, choice, request, board, pickup, quantity, world.ReachLandSalePickup(actor, pickup),
            world.ReachLandSaleReturn(actor, pickup, board, quantity));
    }

    internal ActionCheck CheckLandSaleCollection(ITownWorld world, LandSaleCollectionActionPlan plan, TownRuntimeState? currentTown = null, ActorCapabilityPolicy? policy = null)
    {
        var terms = CheckLandSaleTerms(world, plan, plan.Request, plan.Choice.TownId, plan.Choice.TermsToken, plan.NoticePlace,
            plan.Request.TargetHouseholdId, currentTown, out _, out var current, policy);
        if (!terms.Allowed) return terms;
        var price = current!.Price!;
        var pickup = plan.Pickup;
        var goods = world.SpendingGoods(plan.Actor, GoodsUse.Collect, [current.TargetHouseholdId], price.ItemKind)
            .SingleOrDefault(match => match.Lot.Id == pickup.StockId);
        if (plan.Quantity <= 0 || price.Quantity - LandSaleCarriedQuantity(world, plan.Actor, price) < plan.Quantity ||
            goods is not { Lot: { CarrierId: null, ContainerLotId: null, DeliveryBuildingId: null } stock } || goods.Quantity < plan.Quantity ||
            stock.OwnerId != pickup.OwnerId || stock.ItemKind != pickup.ItemKind || stock.ItemKind == "tool" || ToolProgressionRules.Find(stock.ItemKind) is not null ||
            world.HouseholdStockPosition(stock) != pickup.Position || WorldRuntime.CivicSharedRules.HouseholdStockInteractionRange(stock) != pickup.InteractionRange ||
            world.MarketSurplus(plan.Actor, stock) < plan.Quantity ||
            stock.ItemKind == "food" && (!MayCollectSharedFood(world, plan.Actor) || SharedFoodCollectionAllowance(world, plan.Actor) < plan.Quantity))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected household payment goods or required quantity are no longer available");
        if (world.PickupCarryCapacity(plan.Actor, stock, plan.NoticePlace, WorldRuntime.CivicSharedRules.ResourceInteractionRange) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "they cannot carry the selected payment goods back to the notice place");
        return world.IsWithinInteractionRange(world.Agents[plan.Actor].Position, pickup.Position, pickup.InteractionRange) ||
            world.ReachLandSalePickup(plan.Actor, pickup).Count > 0 ? new()
                : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected payment goods are out of reach now");
    }
}
