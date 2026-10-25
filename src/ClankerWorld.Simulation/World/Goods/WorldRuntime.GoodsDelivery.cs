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
        if (InventoryContainerRules.IsContainer(root.ItemKind) && !InventoryRules.CanMoveFamily(index, root, InventoryHoldRules.BorrowedBy(actor)))
            return GoodsReason.Reservation;
        if (handcartHitches.Any(hitch => hitch.CartLotId == root.Id) || agents.Values.Any(agent =>
                PersonalEquipmentRules.IsSelected(agent.Equipment, root.Id))) return GoodsReason.Custody;
        if (IsActiveTownProjectDelivery(root.Id)) return GoodsReason.Delivery;
        if (request.DeliveryPurpose != GoodsDeliveryPurpose.FarmStock && UnusableDeliveryStock(society.Checkpoint.Inventory, root))
            return GoodsReason.Vessel;
        if (DeliveryGoodsRoom(request, root) <= 0) return GoodsReason.DestinationRoom;
        if (PersonalEquipmentRules.IsCarried(root, actor))
            return Reach(actor, destination, ReachPurpose.GoodsDelivery, 0, 0, SwimmingPolicy.WhenCapable).Count > 0
                ? DeliveryGoodsQuantity(request, index, lot, deliveries) > 0 ? null : GoodsReason.DestinationRoom : GoodsReason.Route;
        if (GoodsRouteExclusion(actor, root, routes) is { } reason) return reason;
        if (FreeCarryCapacity(actor) <= 0) return GoodsReason.CarryRoom;
        return DeliveryGoodsQuantity(request, index, lot, deliveries) > 0 ? null : GoodsReason.CarryRoom;
    }

    private GoodsReason? CarriedDeliveryGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.ContainsKey(actor) || !PersonalEquipmentRules.IsCarried(lot, actor) || lot.ContainerLotId is not null)
            return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not { } destination) return GoodsReason.Delivery;
        var access = InventoryHoldRules.DeliveryFor(actor, destination);
        return index.ContentsOf(lot.Id).Prepend(lot).All(member => InventoryHoldRules.Allows(index, member, access))
            ? null : GoodsReason.Custody;
    }

    private GoodsRequest? CarriedDeliveryGoodsRequest(string actor)
    {
        var candidates = InventoryIndex.For(society.Checkpoint.Inventory).Lots.Where(lot =>
            PersonalEquipmentRules.IsCarried(lot, actor) && lot.DeliveryBuildingId is not null).ToArray();
        if (candidates.Length == 0) return null;
        return new(GoodsUse.CarriedDelivery, actor,
            new(candidates.Select(lot => lot.OwnerId).Distinct(StringComparer.Ordinal).ToArray()),
            new(candidates.Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray()));
    }

    private IEnumerable<InventoryLot> CarriedDeliveryGoods(string actor) => CarriedDeliveryGoodsRequest(actor) is { } request
        ? FindGoods(request).Matches.Select(match => match.Lot) : [];

    private bool MayUseStoreStock(string actor, string household, string store, InventoryLot lot) =>
        MarketSystem.IsLooseShopLot(lot, true) && !OnBorrowedMarketStall(lot) && ShopRules.MaySell("store", lot.ItemKind) &&
        lot.StorageBuildingId != store &&
        (lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) || lot.OwnerId == household && lot.CarrierId is null &&
            (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building => building.InstanceId == lot.StorageBuildingId &&
                building.HouseholdId == household))) &&
        !PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id) &&
        InventoryHoldRules.Allows(InventoryIndex.For(society.Checkpoint.Inventory), lot, InventoryHoldRules.BorrowedBy(actor));

    private InventoryLot[] StoreStockGoods(string actor, string household, string store, string? kind = null)
    {
        var owners = new[] { actor, household };
        return SpendingGoods(actor, GoodsUse.Sell, owners, kind).Select(match => match.Lot)
            .Where(lot => MayUseStoreStock(actor, household, store, lot))
            .OrderBy(lot => lot.OwnerId == actor ? 0 : 1).ThenBy(lot => lot.Id, StringComparer.Ordinal).ToArray();
    }

    private int StoreShelfQuantity(string store, string kind) => InventoryIndex.For(society.Checkpoint.Inventory).StoredAt(store)
        .Where(lot => lot.ItemKind == kind && lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0).Sum(lot => lot.Quantity);

    private int SmithStoredQuantity(string household, string smith, string kind) => InventoryIndex.For(society.Checkpoint.Inventory).StoredAt(smith)
        .Where(lot => lot.OwnerId == household && lot.ItemKind == kind).Sum(AvailableLotQuantity);

    private int SmithIncomingQuantity(string smith, string kind) => InventoryIndex.For(society.Checkpoint.Inventory).Lots
        .Where(lot => lot.DeliveryBuildingId == smith && lot.ItemKind == kind).Sum(AvailableLotQuantity);

    private static IEnumerable<InventoryLot> WorkstationIncomingLots(InventoryCheckpoint inventory, string building) =>
        InventoryIndex.For(inventory).Lots.Where(lot => lot.DeliveryBuildingId == building);

    private GoodsReason? CarriedInputGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        var root = index.Root(lot);
        if (!agents.ContainsKey(actor) || !PersonalEquipmentRules.IsCarried(root, actor)) return GoodsReason.Custody;
        if (root.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (InventoryContainerRules.IsContainer(root.ItemKind) && HasActiveContainerReservation(society.Checkpoint.Inventory, root.Id))
            return GoodsReason.Reservation;
        var access = InventoryHoldRules.BorrowedBy(actor);
        return index.ContentsOf(root.Id).Prepend(root).All(member => InventoryHoldRules.Allows(index, member, access))
            ? null : GoodsReason.Custody;
    }

    private GoodsReason? MedicalDoseExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.ContainsKey(actor)) return GoodsReason.Custody;
        if (lot.ItemKind != "medicine" || lot.ContainerLotId is not null || lot.GroundPosition is not null || lot.DeliveryBuildingId is not null)
            return GoodsReason.Place;
        if (!InventoryRules.CanReserve(index, lot, lot.OwnerId, 1, InventoryHoldRules.BorrowedBy(actor))) return GoodsReason.Reservation;
        if (lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor)) return null;
        return lot.OwnerId == HouseholdFor(actor) && lot.StorageBuildingId is { } buildingId &&
            worldSimulation.Buildings.Any(building => building.InstanceId == buildingId && building.HouseholdId == lot.OwnerId) &&
            IsWithinInteractionRange(agents[actor].Position, HouseholdStockPosition(lot), HouseholdStockInteractionRange(lot))
            ? null : GoodsReason.Custody;
    }

    private IEnumerable<GoodsMatch> MedicalDoseStock(string actor)
    {
        var owners = HouseholdFor(actor) is { } home ? new[] { actor, home } : [actor];
        return FindGoods(new(GoodsUse.MedicalDose, actor, new(owners), GoodsKinds.One("medicine"))).Matches
            .OrderBy(match => match.Lot.OwnerId == actor ? 0 : 1).ThenBy(match => match.Lot.Id, StringComparer.Ordinal);
    }
}
