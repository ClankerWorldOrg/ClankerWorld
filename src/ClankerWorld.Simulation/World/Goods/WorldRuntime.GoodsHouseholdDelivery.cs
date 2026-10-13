using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
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
}
