using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IReadOnlyList<GoodsMatch> DismountCargo(string actor)
    {
        var roots = InventoryIndex.For(society.Checkpoint.Inventory).Lots.Where(lot => lot.ContainerLotId is null &&
            PersonalEquipmentRules.IsCarried(lot, actor)).ToArray();
        return roots.Length == 0 ? [] : FindGoods(new(GoodsUse.DismountCargo, actor,
            new GoodsOwners(roots.Select(lot => lot.OwnerId).Distinct(StringComparer.Ordinal).ToArray()),
            new GoodsKinds(roots.Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray()))).Matches;
    }

    private GoodsReason? DismountCargoExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (lot.ContainerLotId is not null || !PersonalEquipmentRules.IsCarried(lot, actor)) return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id)) return GoodsReason.Custody;
        return InventoryContainerRules.IsContainer(lot.ItemKind)
            ? InventoryRules.CanMoveFamily(index, lot, InventoryHoldRules.SupplyTripFor(actor)) ? null : GoodsReason.Reservation
            : InventoryHoldRules.Allows(index, lot, InventoryHoldRules.SupplyTripFor(actor)) ? null : GoodsReason.Custody;
    }
}
