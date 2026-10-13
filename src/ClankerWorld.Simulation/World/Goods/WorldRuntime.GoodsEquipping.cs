using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GoodsReason? EquippingGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot,
        Dictionary<(GridPoint Position, int Range), bool>? routes)
    {
        var actor = request.Actor!;
        if (lot.ContainerLotId is not null) return GoodsReason.Vessel;
        if (!InventoryHoldRules.Allows(index, lot, InventoryHoldRules.BorrowedBy(actor))) return GoodsReason.Custody;
        if (lot.OwnerId == actor) return PersonalEquipmentRules.IsCarried(lot, actor) ? null : GoodsReason.Custody;
        if (lot.CarrierId is not null) return GoodsReason.Custody;
        if (!MayCollectGoodsAt(actor, lot)) return GoodsReason.Place;
        return GoodsRouteExclusion(actor, lot, routes);
    }
}
