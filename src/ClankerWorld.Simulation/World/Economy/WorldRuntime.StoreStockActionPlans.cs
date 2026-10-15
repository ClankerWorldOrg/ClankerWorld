using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy StoreStockPolicy => new(ActionLifeStages.Adults, AllowUrgentFood: false,
        AllowActiveOrder: true);

    private int StoreStockLotQuantity(string actor, InventoryLot lot, IReadOnlySet<string> protectedTools) => Math.Max(0,
        (lot.OwnerId == actor ? SpareCarriedQuantity(actor, lot) : AvailableLotQuantity(lot)) -
        (protectedTools.Contains(lot.Id) ? 1 : 0));

    private int StoreStockLoadLimit(string actor, string household, PlacedBuilding store, InventoryLot lot,
        IReadOnlyList<InventoryLot> sources, IReadOnlySet<string> protectedTools,
        Dictionary<(string OwnerId, string ItemKind), int>? foodSurpluses = null)
    {
        var surplus = StoreStockLotQuantity(actor, lot, protectedTools);
        if (IsEdibleFood(lot.ItemKind))
        {
            var key = (lot.OwnerId, lot.ItemKind);
            if (foodSurpluses is null || !foodSurpluses.TryGetValue(key, out var foodSurplus))
            {
                var reserve = lot.OwnerId == actor ? 2 : 2 * society.Checkpoint.GetHousehold(household)
                    .MemberIds.Count(id => agents.ContainsKey(id));
                foodSurplus = Math.Max(0, sources.Where(source => source.OwnerId == lot.OwnerId && source.ItemKind == lot.ItemKind &&
                    StoreStockTripIsReachable(actor, store, source)).Sum(source => StoreStockLotQuantity(actor, source, protectedTools)) - reserve);
                foodSurpluses?.Add(key, foodSurplus);
            }
            surplus = Math.Min(surplus, foodSurplus);
        }
        var quantity = Math.Min(Math.Min(surplus, StoreShelfTarget - StoreShelfQuantity(store.InstanceId, lot.ItemKind)),
            Math.Min(HouseHaulLoadQuantity, DestinationRoom(store.InstanceId)));
        return lot.OwnerId == actor ? quantity : Math.Min(quantity, PickupCarryCapacity(actor, lot, store.Position));
    }

    private bool StoreStockTripIsReachable(string actor, PlacedBuilding store, InventoryLot lot)
    {
        var source = lot.OwnerId == actor ? agents[actor].Position : HouseholdStockPosition(lot);
        var range = lot.OwnerId == actor ? 0 : HouseholdStockInteractionRange(lot);
        return CanReach(actor, source, ReachPurpose.StoreStock, range, 0, SwimmingPolicy.WhenCapable) &&
            CanReachLeg(actor, source, store.Position, ReachPurpose.StoreStock, 0, 0, SwimmingPolicy.WhenCapable);
    }

    private StoreStockActionPlan? PlanStoreStock(string actor)
    {
        if (!ActorCapabilities.Check(CapabilitiesFor(actor), "shop_stock_store", StoreStockPolicy).Allowed ||
            NextStoreLoad(actor) is not { } load) return null;
        var direct = load.Goods.OwnerId == actor;
        var target = direct ? load.Store.Position : HouseholdStockPosition(load.Goods);
        var range = direct ? 0 : HouseholdStockInteractionRange(load.Goods);
        return new(actor, load.Store.HouseholdId!, load.Store.InstanceId, load.Store.Position, load.Goods.Id,
            load.Goods.OwnerId, load.Goods.ItemKind, load.Quantity, direct, target, range,
            Reach(actor, target, ReachPurpose.StoreStock, range, 0, SwimmingPolicy.WhenCapable),
            direct ? Array.AsReadOnly<GridPoint>([]) : ReachLeg(actor, target, load.Store.Position, ReachPurpose.StoreStock, 0, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckStoreStockPlan(StoreStockActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId, policy ?? StoreStockPolicy);
        if (!capability.Allowed) return capability;
        if (HouseholdFor(plan.Actor) != plan.HouseholdId || HouseholdBuildingWithTag(plan.HouseholdId, "store") is not { } store ||
            store.InstanceId != plan.StoreId || store.Position != plan.Destination || CarriedHouseDelivery(plan.Actor) is not null ||
            agents[plan.Actor].Project is { Stage: not ("completed" or "cancelled") })
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their Store or current work has changed");
        var request = new GoodsRequest(GoodsUse.Sell, plan.Actor, GoodsOwners.One(plan.OwnerId), GoodsKinds.One(plan.ItemKind));
        if (RecheckGoods(request, plan.StockId) is not { } match || !MayUseStoreStock(plan.Actor, plan.HouseholdId, plan.StoreId, match.Lot) ||
            plan.DirectDelivery != (match.Lot.OwnerId == plan.Actor) ||
            !plan.DirectDelivery && (HouseholdStockPosition(match.Lot) != plan.Target || HouseholdStockInteractionRange(match.Lot) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Store stock is no longer available");
        if (DestinationRoom(plan.StoreId) < plan.Quantity || StoreShelfTarget - StoreShelfQuantity(plan.StoreId, plan.ItemKind) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the Store has no room for the selected load");
        var sources = StoreStockGoods(plan.Actor, plan.HouseholdId, plan.StoreId);
        var protectedTools = BestUsableToolIds(society.Checkpoint.Inventory, plan.Actor);
        if (StoreStockLoadLimit(plan.Actor, plan.HouseholdId, store, match.Lot, sources, protectedTools) < plan.Quantity)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected load would use supplies that must stay");
        return StoreStockTripIsReachable(plan.Actor, store, match.Lot) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Store trip is out of reach now");
    }

    private void ApplyStoreStockPlan(StoreStockActionPlan? plan)
    {
        if (plan is null) return;
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedStoreStockPlan(plan);
    }

    private void ApplyCheckedStoreStockPlan(StoreStockActionPlan plan)
    {
        var state = agents[plan.Actor];
        if (!IsWithinInteractionRange(state.Position, plan.Target, plan.InteractionRange))
        {
            MoveToward(plan.Actor, state, plan.Target, "store_stock", plan.InteractionRange);
            return;
        }
        ApplyInventoryTransition(inventory => plan.DirectDelivery
            ? InventoryAuthority.Transfer(inventory, $"store-stock:{WorldTick}:{plan.Actor}", plan.Actor, plan.HouseholdId, plan.StockId, plan.Quantity,
                "store_stock_delivered", destinationStorageBuildingId: plan.StoreId, holdAccess: InventoryHoldRules.DeliveryFor(plan.Actor, plan.StoreId))
            : InventoryAuthority.Transfer(inventory, $"store-pickup:{WorldTick}:{plan.Actor}", plan.HouseholdId, plan.Actor, plan.StockId, plan.Quantity,
                "store_stock_collected", destinationDeliveryBuildingId: plan.StoreId, holdAccess: InventoryHoldRules.BorrowedBy(plan.Actor)));
        AppendEvent(plan.DirectDelivery ? "store_stock_delivered" : "store_stock_collected", $"{plan.Actor}:{plan.StockId}:{plan.Quantity}:{plan.StoreId}");
    }
}
