using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
