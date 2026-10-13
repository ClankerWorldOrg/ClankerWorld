using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GoodsReason? SalvageGoodsExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.TryGetValue(actor, out var person)) return GoodsReason.Custody;
        if (towns.SingleOrDefault(town => town.Id == lot.OwnerId) is not { IsAbandoned: true } town)
            return GoodsReason.Owner;
        if (lot.CarrierId is not null || lot.ContainerLotId is not null) return GoodsReason.Custody;
        if (lot.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (lot.GroundPosition is null && (lot.StorageBuildingId is not { } storage ||
            !WarehousesForTown(town.Id).Any(building => building.InstanceId == storage && building.HouseholdId is null)))
            return GoodsReason.Place;
        var range = lot.GroundPosition is not null ? ResourceInteractionRange : 0;
        if (!IsWithinInteractionRange(person.Position, HouseholdStockPosition(lot), range)) return GoodsReason.Route;
        var access = InventoryHoldRules.BorrowedBy(actor);
        if (index.ContentsOf(lot.Id).Prepend(lot).Any(member => !InventoryHoldRules.Allows(index, member, access)))
            return GoodsReason.Custody;
        return null;
    }
}
