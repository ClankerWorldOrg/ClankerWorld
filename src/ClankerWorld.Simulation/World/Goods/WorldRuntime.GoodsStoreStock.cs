using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private bool MayUseStoreStock(string actor, string household, string store, InventoryLot lot) =>
        IsLooseShopLot(lot, includeGround: true) && !OnBorrowedMarketStall(lot) && ShopRules.MaySell("store", lot.ItemKind) &&
        lot.StorageBuildingId != store &&
        (lot.OwnerId == actor && PersonalEquipmentRules.IsCarried(lot, actor) || lot.OwnerId == household && lot.CarrierId is null &&
            (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building => building.InstanceId == lot.StorageBuildingId &&
                building.HouseholdId == household))) &&
        !PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id) &&
        InventoryHoldRules.Allows(InventoryIndex.For(society.Checkpoint.Inventory), lot, InventoryHoldRules.BorrowedBy(actor));

    private InventoryLot[] StoreStockGoods(string actor, string household, string store, string? kind = null)
    {
        var owners = new[] { actor, household };
        return SpendingGoods(actor, GoodsUse.Sell, owners, kind).Select(match => match.Lot)
            .Where(lot => MayUseStoreStock(actor, household, store, lot))
            .OrderBy(lot => lot.OwnerId == actor ? 0 : 1).ThenBy(lot => lot.Id, StringComparer.Ordinal).ToArray();
    }

    private int StoreShelfQuantity(string store, string kind) => InventoryIndex.For(society.Checkpoint.Inventory).StoredAt(store)
        .Where(lot => lot.ItemKind == kind && lot.FreshnessBasisPoints > 0 && lot.ConditionBasisPoints > 0).Sum(lot => lot.Quantity);
}
