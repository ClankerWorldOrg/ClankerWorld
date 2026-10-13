using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed record PlannedStockMember(string LotId, string OwnerId, string ItemKind, int Quantity);

public static class GoodsPlanFamily
{
    public static ReadOnlyCollection<PlannedStockMember> Capture(InventoryCheckpoint inventory, InventoryLot root) =>
        Array.AsReadOnly(InventoryIndex.For(inventory).ContentsOf(root.Id).Prepend(root).OrderBy(lot => lot.Id, StringComparer.Ordinal)
            .Select(lot => new PlannedStockMember(lot.Id, lot.OwnerId, lot.ItemKind, lot.Quantity)).ToArray());

    public static bool Matches(InventoryCheckpoint inventory, InventoryLot root, IReadOnlyList<PlannedStockMember>? family) =>
        family is null || Capture(inventory, root).SequenceEqual(family);
}
