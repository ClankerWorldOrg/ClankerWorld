using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
}
