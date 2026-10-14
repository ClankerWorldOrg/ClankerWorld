using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    /// <summary>Read-only, deterministic selection. Recheck immediately before applying an action.</summary>
    public GoodsAnswer FindGoods(GoodsRequest request) => FindGoods(society.Checkpoint.Inventory, request);

    public GoodsMatch? RecheckGoods(GoodsRequest request, string lotId) =>
        RecheckGoods(society.Checkpoint.Inventory, request, lotId);

    private GoodsMatch? RecheckGoods(InventoryCheckpoint inventory, GoodsRequest request, string lotId)
    {
        GoodsInventoryQuery.Validate(request);
        var index = InventoryIndex.For(inventory);
        return index.Find(lotId) is { } lot && GoodsExclusion(request, index, lot) is null
            ? MakeGoodsMatch(index, lot, request) : null;
    }

    private GoodsAnswer FindGoods(InventoryCheckpoint inventory, GoodsRequest request)
    {
        GoodsInventoryQuery.Validate(request);
        var routes = request.Use is GoodsUse.Collect or GoodsUse.ReachableHoldings or GoodsUse.Recover or GoodsUse.Deliver
            ? new Dictionary<(GridPoint Position, int Range), bool>() : null;
        var deliveries = request.Use == GoodsUse.Deliver ? new Dictionary<string, int>(StringComparer.Ordinal) : null;
        return GoodsInventoryQuery.FindGoods(inventory, request,
            (index, lot) => GoodsContextExclusion(request, index, lot, routes, deliveries),
            (index, lot) => MakeGoodsMatch(index, lot, request, deliveries));
    }

    private GoodsMatch MakeGoodsMatch(InventoryIndex index, InventoryLot lot, GoodsRequest request,
        Dictionary<string, int>? deliveries = null)
    {
        var quantity = request.Use == GoodsUse.Deliver ? DeliveryGoodsQuantity(request, index, lot, deliveries) : GoodsQuantity(index, lot, request);
        var root = index.Root(lot);
        var family = InventoryContainerRules.IsContainer(root.ItemKind) &&
            (InventoryContainerRules.IsContainer(lot.ItemKind) || request.Use is GoodsUse.Collect or GoodsUse.Deliver);
        var partialFarmLoad = request.Use == GoodsUse.Deliver && request.DeliveryPurpose == GoodsDeliveryPurpose.FarmStock &&
            (index.FamilyQuantity(root.Id) > DeliveryGoodsCapacity(request, root, deliveries) ||
             UnusableDeliveryStock(society.Checkpoint.Inventory, root));
        return new(lot, root, GoodsPlaceFor(root), quantity, family && !partialFarmLoad ? index.FamilyQuantity(root.Id) : quantity);
    }

    private GoodsReason? GoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes = null)
    {
        return GoodsInventoryQuery.Exclusion(request, index, lot) ?? GoodsContextExclusion(request, index, lot, routes);
    }

    private GoodsReason? GoodsContextExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes = null, Dictionary<string, int>? deliveries = null)
    {
        if (request.Use == GoodsUse.TreePlanting) return TreePlantingGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.FieldPlanting) return FieldPlantingGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.CarriedInputs) return CarriedInputGoodsExclusion(request.Actor!, index, lot);
        if (request.Use is GoodsUse.SpareCargo or GoodsUse.OrchardFoodRecovery) return SpareCargoGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.CarriedDelivery) return CarriedDeliveryGoodsExclusion(request.Actor!, index, lot);
        if (request.Use == GoodsUse.Salvage) return SalvageGoodsExclusion(request.Actor!, index, lot);
        if (GoodsInventoryQuery.IsRecovery(request.Use)) return RecoveryGoodsExclusion(request, index, lot, routes);
        if (request.Use is GoodsUse.Spend or GoodsUse.Sell or GoodsUse.StorePersonal)
            return SpendingGoodsExclusion(request, index, lot);
        if (request.Use == GoodsUse.Cargo)
            return lot.OwnerId == request.Actor && index.Root(lot).ItemKind == InventoryContainerRules.Handcart
                ? null : GoodsReason.Custody;
        if (request.Use == GoodsUse.Eat)
        {
            var root = index.Root(lot);
            if (!FoodRules.IsEdible(lot.ItemKind)) return GoodsReason.Kind;
            if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } eatingPromise) return eatingPromise;
            return PersonalEquipmentRules.IsCarried(root, request.Actor!) ? null : GoodsReason.Custody;
        }
        if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } promise) return promise;
        if (request.Use == GoodsUse.Deliver) return DeliveryGoodsExclusion(request, index, lot, routes, deliveries);
        if (request.Use == GoodsUse.Collect) return CollectGoodsExclusion(request.Actor!, index, lot, routes);
        if (request.Use == GoodsUse.ReachableHoldings) return GoodsRouteExclusion(request.Actor!, index.Root(lot), routes);
        return null;
    }

    private GoodsPlace GoodsPlaceFor(InventoryLot root)
    {
        if (root.StorageBuildingId is { } id)
            return new(GoodsPlaceKind.Stored, id,
                worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == id)?.Position,
                DeliveryBuildingId: root.DeliveryBuildingId);
        if (root.GroundPosition is { } ground)
            return new(GoodsPlaceKind.Ground, Position: new GridPoint(ground.X, ground.Y), DeliveryBuildingId: root.DeliveryBuildingId);
        var carrier = root.CarrierId ?? (agents.ContainsKey(root.OwnerId) ? root.OwnerId : null);
        return carrier is not null
            ? new(GoodsPlaceKind.Carried, Position: agents.GetValueOrDefault(carrier)?.Position,
                CarrierId: carrier, DeliveryBuildingId: root.DeliveryBuildingId)
            : new(GoodsPlaceKind.Unlocated, DeliveryBuildingId: root.DeliveryBuildingId);
    }
}
