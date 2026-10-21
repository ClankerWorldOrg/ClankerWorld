using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    internal sealed record BorrowedReturnEffect(string MoveId, string ItemKind, int Quantity);

    private IEnumerable<InventoryLot> ReturnableBorrowedGoods(string actor) => CarriedBorrowedGoods(actor).Where(lot =>
        lot.DeliveryBuildingId is null && !PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id) &&
        PhysicalUnreservedQuantity(lot) > 0 &&
        (!InventoryContainerRules.IsContainer(lot.ItemKind) || !HasActiveContainerReservation(society.Checkpoint.Inventory, lot.Id)));

    private BorrowedReturnEffect? ReturnBorrowedGoods(string actor, string lotId, int maximumQuantity = int.MaxValue,
        string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null)
    {
        var plan = PlanPersonalGoods(actor, new(PersonalGoodsKind.Return, lotId), maximumQuantity,
            requiredHouseId is null ? PersonalGoodsMode.Native : PersonalGoodsMode.BoundOrder,
            requiredHouseId, requiredHouseholdId, requiredPosition);
        return ApplyPersonalGoods(plan) is { } effect ? new(effect.MoveId, effect.ItemKind, effect.Quantity) : null;
    }
}
