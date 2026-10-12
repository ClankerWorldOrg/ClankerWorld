using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // A single request owns the common quantity/commitment rules. Callers add
    // their existing physical-place, food-reserve and business-purpose rules.
    private IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners,
        string? kind = null, string? building = null, InventoryCheckpoint? inventory = null)
    {
        inventory ??= society.Checkpoint.Inventory;
        var index = InventoryIndex.For(inventory);
        var kinds = kind is not null ? new[] { kind } : owners.SelectMany(index.OwnedBy)
            .Select(lot => lot.ItemKind).Distinct(StringComparer.Ordinal).ToArray();
        if (kinds.Length == 0) return [];
        return FindGoods(inventory, new(use, actor, new(owners), new(kinds), AtBuilding: building)).Matches;
    }

    private GoodsReason? SpendingGoodsExclusion(GoodsRequest request, InventoryIndex index, InventoryLot lot)
    {
        var actor = request.Actor!;
        if (!agents.ContainsKey(actor)) return GoodsReason.Custody;
        var root = index.Root(lot);
        if (root.DeliveryBuildingId is not null) return GoodsReason.Delivery;
        if (request.Use == GoodsUse.StorePersonal)
        {
            // Storing physical property keeps damage/spoilage, just as relocation
            // does; it is not a usable-goods spending or recovery action.
            if (lot.OwnerId != actor) return GoodsReason.Owner;
            if (lot.ContainerLotId is not null) return GoodsReason.Place;
            if (!PersonalEquipmentRules.IsCarried(lot, actor) ||
                PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id)) return GoodsReason.Custody;
            if (InventoryContainerRules.IsContainer(lot.ItemKind) && !InventoryRules.CanMoveFamily(index, lot))
                return GoodsReason.Reservation;
            return null;
        }
        // Sale stock on a borrowed stall remains available to its actual seller;
        // the Market caller still validates the exact live occupancy and stock receipt.
        if (request.Use == GoodsUse.Spend && OnBorrowedMarketStall(root)) return GoodsReason.MarketStall;
        return null;
    }
}
