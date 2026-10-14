using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record PersonalStorageEffect(string MoveId, string ItemKind, int Quantity);

    private IEnumerable<InventoryLot> PersonalStorageLots(string actor, string houseId) =>
        SpendingGoods(actor, GoodsUse.StorePersonal, [actor]).Select(match => match.Lot)
            .Where(lot => !IsEdibleFood(lot.ItemKind) &&
            VesselFits(lot, DestinationRoom(houseId))).OrderBy(lot => lot.Id, StringComparer.Ordinal);

    private PersonalStorageEffect? StorePersonalGoods(string actor, string lotId, int maximumQuantity = int.MaxValue,
        string? requiredHouseId = null, string? requiredHouseholdId = null, GridPoint? requiredPosition = null)
    {
        var plan = PlanPersonalGoods(actor, new(PersonalGoodsKind.Store, lotId), maximumQuantity,
            requiredHouseId is null ? PersonalGoodsMode.Native : PersonalGoodsMode.BoundOrder,
            requiredHouseId, requiredHouseholdId, requiredPosition);
        return ApplyPersonalGoods(plan) is { } effect ? new(effect.MoveId, effect.ItemKind, effect.Quantity) : null;
    }
}
