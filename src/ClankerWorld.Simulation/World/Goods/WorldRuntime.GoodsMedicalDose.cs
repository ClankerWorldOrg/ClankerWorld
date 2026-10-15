using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private GoodsReason? MedicalDoseExclusion(string actor, InventoryIndex index, InventoryLot lot)
    {
        if (!agents.ContainsKey(actor)) return GoodsReason.Custody;
        if (lot.ItemKind != "medicine" || lot.ContainerLotId is not null || lot.GroundPosition is not null || lot.DeliveryBuildingId is not null)
            return GoodsReason.Place;
        if (!InventoryRules.CanReserve(index, lot, lot.OwnerId, 1, InventoryHoldRules.BorrowedBy(actor))) return GoodsReason.Reservation;
        if (lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor)) return null;
        return lot.OwnerId == HouseholdFor(actor) && lot.StorageBuildingId is { } buildingId &&
            worldSimulation.Buildings.Any(building => building.InstanceId == buildingId && building.HouseholdId == lot.OwnerId) &&
            IsWithinInteractionRange(agents[actor].Position, HouseholdStockPosition(lot), HouseholdStockInteractionRange(lot))
            ? null : GoodsReason.Custody;
    }

    private IEnumerable<GoodsMatch> MedicalDoseStock(string actor)
    {
        var owners = HouseholdFor(actor) is { } home ? new[] { actor, home } : [actor];
        return FindGoods(new(GoodsUse.MedicalDose, actor, new(owners), GoodsKinds.One("medicine"))).Matches
            .OrderBy(match => match.Lot.OwnerId == actor ? 0 : 1).ThenBy(match => match.Lot.Id, StringComparer.Ordinal);
    }
}
