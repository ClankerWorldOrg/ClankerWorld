using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
}
