using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
}
