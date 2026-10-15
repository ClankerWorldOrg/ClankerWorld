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
}
