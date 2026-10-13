using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Trial shelf target and load size. Goods above this target stay at their source.
    private const int StoreShelfTarget = 8;
    private sealed record StoreStockLoad(PlacedBuilding Store, InventoryLot Goods, int Quantity);

    private StoreStockLoad? NextStoreLoad(string actor, string? itemKind = null, string? buildingId = null,
        string? sourceLotId = null, bool allowOrdinaryProject = false)
    {
        if (!IsAdult(actor) || CarriedHouseDelivery(actor) is not null ||
            agents[actor].Project is { Stage: not ("completed" or "cancelled") } project &&
                (!allowOrdinaryProject || !OrdinaryProjectCanYieldToOrder(project)) ||
            society.Checkpoint.GetAgent(actor).HouseholdId is not { } householdId ||
            HouseholdBuildingWithTag(householdId, "store") is not { } store ||
            buildingId is not null && store.InstanceId != buildingId)
            return null;
        var inventory = society.Checkpoint.Inventory;
        var room = DestinationRoom(inventory, store.InstanceId);
        if (room == 0) return null;
        var protectedToolIds = BestUsableToolIds(inventory, actor);
        var sources = StoreStockGoods(actor, householdId, store.InstanceId, itemKind);
        var foodSurpluses = new Dictionary<(string OwnerId, string ItemKind), int>();
        foreach (var lot in sources)
        {
            if (sourceLotId is not null && lot.Id != sourceLotId) continue;
            var quantity = StoreStockLoadLimit(actor, householdId, store, lot, sources, protectedToolIds, foodSurpluses);
            if (quantity <= 0 || !CanReachStoreStock(actor, store, lot)) continue;
            return new(store, lot, quantity);
        }
        return null;
    }

    private void AddStoreStockCandidate(List<CognitionCandidate> candidates, string actor)
    {
        if (PlanStoreStock(actor) is { } plan && CheckActionPlan(plan).Allowed)
            candidates.Add(new("shop_stock_store",
                $"Carry {plan.Quantity} {plan.ItemKind} into the household Store before offering it for sale.",
                38, plan.StoreId)
            { ActionPlan = plan });
    }

    private void StockStore(string actor, AgentState state) => ApplyStoreStockPlan(PlanStoreStock(actor));

    private DeliveryOrderPlan? GetStoreOrderPlan(string actor, AgentState person,
        OrderTask order, int maximumQuantity)
    {
        if (NextStoreLoad(actor, order.TargetItemKind, order.TargetStorageBuildingId, order.DeliveryLotId,
                allowOrdinaryProject: true) is not { } load ||
            !DeliveryDestinationMatches(order, load.Store)) return null;
        var quantity = Math.Min(maximumQuantity, load.Quantity);
        return quantity <= 0 ? null : new DeliveryOrderPlan("store_stock", load.Store, load.Store.HouseholdId!,
            load.Goods, load.Goods, quantity, quantity, DirectDelivery: load.Goods.OwnerId == actor);
    }
}
