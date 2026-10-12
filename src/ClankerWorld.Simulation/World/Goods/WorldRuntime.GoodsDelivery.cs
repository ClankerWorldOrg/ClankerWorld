using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static GoodsRequest DeliveryGoodsRequest(string actor, string owner, string kind, string destination,
        GoodsDeliveryPurpose purpose = GoodsDeliveryPurpose.Stock) =>
        new(GoodsUse.Deliver, actor, GoodsOwners.One(owner), GoodsKinds.One(kind),
            Destination: destination, DeliveryPurpose: purpose);

    private GridPoint? GoodsDeliveryPosition(GoodsRequest request) => request.Near ??
        worldSimulation.Buildings.FirstOrDefault(building => building.InstanceId == request.Destination)?.Position;

    private int DeliveryGoodsRoom(GoodsRequest request, InventoryLot root) =>
        request.DeliveryPurpose is GoodsDeliveryPurpose.Expansion or GoodsDeliveryPurpose.TownProject
            ? int.MaxValue : DestinationRoom(request.Destination!, root);

    private int DeliveryGoodsCapacity(GoodsRequest request, InventoryLot root, Dictionary<string, int>? deliveries = null)
    {
        if (deliveries is not null && deliveries.TryGetValue(root.Id, out var cached)) return cached;
        if (GoodsDeliveryPosition(request) is not { } destination) return 0;
        var capacity = DeliveryGoodsRoom(request, root);
        if (!PersonalEquipmentRules.IsCarried(root, request.Actor!))
            capacity = Math.Min(capacity, PickupCarryCapacity(request.Actor!, root, destination));
        deliveries?.Add(root.Id, capacity);
        return capacity;
    }

    private int DeliveryGoodsQuantity(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<string, int>? deliveries = null)
    {
        var root = index.Root(lot);
        var capacity = DeliveryGoodsCapacity(request, root, deliveries);
        var quantity = GoodsInventoryQuery.Quantity(index, lot, request.Use);
        if (!InventoryContainerRules.IsContainer(root.ItemKind)) return Math.Min(quantity, capacity);
        if (index.FamilyQuantity(root.Id) <= capacity && !UnusableDeliveryStock(society.Checkpoint.Inventory, root)) return quantity;
        // Farm storage already permits taking loose grain or flour out of a
        // free vessel. Other delivery uses move the complete family.
        return request.DeliveryPurpose == GoodsDeliveryPurpose.FarmStock && lot.ContainerLotId == root.Id &&
            lot.ItemKind is FarmFieldRules.Grain or "flour" ? Math.Min(quantity, capacity) : 0;
    }

    private GoodsReason? DeliveryGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes, Dictionary<string, int>? deliveries)
    {
        var actor = request.Actor!;
        if (!agents.TryGetValue(actor, out var person)) return GoodsReason.Custody;
        var root = index.Root(lot);
        if (GoodsDeliveryPosition(request) is not { } destination || root.StorageBuildingId == request.Destination &&
            request.Destination is not null) return GoodsReason.Place;
        if (root.CarrierId is { } carrier && carrier != actor) return GoodsReason.Custody;
        var townGround = request.DeliveryPurpose is GoodsDeliveryPurpose.Expansion or GoodsDeliveryPurpose.TownProject &&
            root.OwnerId == TownForResident(actor) && root.StorageBuildingId is null;
        if (!townGround && !MayCollectGoodsAt(actor, root)) return GoodsReason.Place;
        if (InventoryContainerRules.IsContainer(root.ItemKind) && !InventoryRules.CanMoveFamily(index, root))
            return GoodsReason.Reservation;
        if (handcartHitches.Any(hitch => hitch.CartLotId == root.Id) || agents.Values.Any(agent =>
                PersonalEquipmentRules.IsSelected(agent.Equipment, root.Id))) return GoodsReason.Custody;
        if (IsActiveTownProjectDelivery(root.Id)) return GoodsReason.Delivery;
        if (request.DeliveryPurpose != GoodsDeliveryPurpose.FarmStock && UnusableDeliveryStock(society.Checkpoint.Inventory, root))
            return GoodsReason.Vessel;
        if (DeliveryGoodsRoom(request, root) <= 0) return GoodsReason.DestinationRoom;
        if (PersonalEquipmentRules.IsCarried(root, actor))
            return FindUnoccupiedRoute(actor, person.Position, destination, 0).Count > 0
                ? DeliveryGoodsQuantity(request, index, lot, deliveries) > 0 ? null : GoodsReason.DestinationRoom : GoodsReason.Route;
        if (GoodsRouteExclusion(actor, root, routes) is { } reason) return reason;
        if (FreeCarryCapacity(actor) <= 0) return GoodsReason.CarryRoom;
        return DeliveryGoodsQuantity(request, index, lot, deliveries) > 0 ? null : GoodsReason.CarryRoom;
    }
}
