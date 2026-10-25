using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Already promised feed still occupies the yard's target stock. Preserve
    // physical work accounting here; collection uses its own promise checks.
    private int AnimalYardFeedStock(string yardId) => InventoryIndex.For(society.Checkpoint.Inventory)
        .StoredAt(yardId).Where(lot => AnimalRules.IsFeed(lot.ItemKind)).Sum(AvailableLotQuantity);

    private static GoodsReason? AnimalTripGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var actor = request.Actor!;
        if (lot.ContainerLotId is not null || !PersonalEquipmentRules.IsCarried(lot, actor)) return GoodsReason.Custody;
        if (!InventoryHoldRules.For(index, lot).Contains(InventoryStore.CreateInventoryHold(InventoryHoldPurpose.SupplyTrip, actor)) ||
            !InventoryRules.CanMoveFamily(index, lot, InventoryHoldRules.SupplyTripFor(actor))) return GoodsReason.Reservation;
        return null;
    }

    private static GoodsReason? AnimalProductGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        if (lot.ContainerLotId is not null || lot.StorageBuildingId is not null || lot.CarrierId is not null || lot.GroundPosition is null)
            return GoodsReason.Place;
        if (!InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(request.Actor!))) return GoodsReason.Custody;
        if (request.Use == GoodsUse.AnimalHide) return null;
        var claims = index.ActiveReservationsFor(lot.Id).ToArray();
        return claims.Length == 1 && claims[0].Id == request.ReservationId && claims[0].OwnerId == lot.OwnerId &&
            claims[0].Quantity == lot.Quantity && claims[0].IsExclusive && claims[0].State == InventoryReservationState.Reserved
                ? null : GoodsReason.Reservation;
    }

    private IReadOnlyList<GoodsMatch> AnimalPayments(string actor)
    {
        var kinds = InventoryIndex.For(society.Checkpoint.Inventory).OwnedBy(actor).Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        return kinds.Length == 0 ? [] : FindGoods(new(GoodsUse.AnimalPayment, actor, GoodsOwners.One(actor), new(kinds))).Matches;
    }

    private GoodsReason? AnimalPaymentExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.TryGetValue(actor, out var person) || lot.OwnerId != actor || !PersonalEquipmentRules.IsCarried(lot, actor)) return GoodsReason.Custody;
        if (lot.ContainerLotId is not null || InventoryContainerRules.IsContainer(lot.ItemKind)) return GoodsReason.Vessel;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (PersonalEquipmentRules.IsSelected(person.Equipment, lot.Id)) return GoodsReason.Custody;
        return InventoryHoldRules.Allows(index, lot) ? null : GoodsReason.Reservation;
    }

    // Physical milling accounting includes usable stock promised to this
    // building. Those promises must not be selected as a second pickup.
    private int FarmSuppliedQuantity(string household, string farmhouse) => InventoryIndex.For(society.Checkpoint.Inventory)
        .StoredAt(farmhouse).Where(lot => lot.OwnerId == household && lot.ItemKind == FarmFieldRules.Grain).Sum(AvailableLotQuantity);

    private int FarmIncomingQuantity(string farmhouse) => InventoryIndex.For(society.Checkpoint.Inventory).Lots
        .Where(lot => lot.DeliveryBuildingId == farmhouse && lot.ItemKind == FarmFieldRules.Grain).Sum(AvailableLotQuantity);

    private static int FieldPlantingGoodsQuantity(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var available = InventoryRules.UsableQuantity(index, lot);
        var claim = request.ReservationId is { } id ? index.ActiveReservationsFor(lot.Id).SingleOrDefault(item =>
            item.Id == id && item.OwnerId == lot.OwnerId && item.Purpose == "field_replanting" &&
            item.State == InventoryReservationState.Reserved) : null;
        return checked(available + (claim?.Quantity ?? 0));
    }

    private GoodsReason? FieldPlantingGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var actor = request.Actor!;
        if (lot.ContainerLotId is not null) return GoodsReason.Vessel;
        if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } promise) return promise;
        if (FieldPlantingGoodsQuantity(request, index, lot) <= 0) return GoodsReason.Reservation;
        if (!InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(actor))) return GoodsReason.Custody;
        if (lot.OwnerId == actor) return PersonalEquipmentRules.IsCarried(lot, actor) ? null : GoodsReason.Custody;
        if (lot.OwnerId != HouseholdFor(actor) || lot.CarrierId is not null) return GoodsReason.Custody;
        if (FreeCarryCapacity(actor) <= 0) return GoodsReason.CarryRoom;
        return null;
    }

    internal static InventoryReservation? OrchardPlantingClaim(InventoryIndex index, InventoryLot lot, long tick) =>
        index.ActiveReservationsFor(lot.Id).SingleOrDefault(item => item.OwnerId == lot.OwnerId &&
            item.Id == FarmingSystem.OrchardReplantingReservationId(lot.Id) && item.ExpiryTick >= tick &&
            item.State is InventoryReservationState.Reserved or InventoryReservationState.PartiallyConsumed);

    private int TreePlantingGoodsQuantity(InventoryIndex index, InventoryLot lot) =>
        lot.FreshnessBasisPoints <= 0 || lot.ConditionBasisPoints <= 0 ? 0 :
            checked(InventoryRules.UsableQuantity(index, lot) + (OrchardPlantingClaim(index, lot, WorldTick)?.Quantity ?? 0));

    private GoodsReason? TreePlantingGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        if (lot.OwnerId != request.Actor || lot.ContainerLotId is not null || !PersonalEquipmentRules.IsCarried(lot, request.Actor!))
            return GoodsReason.Custody;
        if (GoodsInventoryQuery.PromiseExclusion(request, index, lot) is { } promise) return promise;
        if (!InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(request.Actor!))) return GoodsReason.Custody;
        return TreePlantingGoodsQuantity(index, lot) > 0 ? null : GoodsReason.Reservation;
    }
}
