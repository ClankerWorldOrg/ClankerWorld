using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static GoodsReason? EquipmentRepairGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (lot.OwnerId != actor || !PersonalEquipmentRules.IsCarried(lot, actor)) return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (lot.Quantity != 1 || lot.ConditionBasisPoints > 4_000 ||
            !(PersonalEquipmentRules.IsGarment(lot.ItemKind) || PersonalEquipmentRules.IsCarryAid(lot.ItemKind))) return GoodsReason.Kind;
        if (index.ActiveReservationsFor(lot.Id).Any()) return GoodsReason.Reservation;
        return InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(actor)) ? null : GoodsReason.Custody;
    }
}
