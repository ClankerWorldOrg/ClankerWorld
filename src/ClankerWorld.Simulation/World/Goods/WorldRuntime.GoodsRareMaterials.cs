using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // The trial-stock quota counts physical owned units, including promises and
    // unusable stock, just as before; it is not a spending/availability query.
    private int RareMaterialStockQuantity(string actor, string kind)
    {
        var index = InventoryIndex.For(society.Checkpoint.Inventory);
        return new[] { actor, HouseholdFor(actor) }.OfType<string>().Distinct(StringComparer.Ordinal)
            .SelectMany(index.OwnedBy).Where(lot => lot.ItemKind == kind).Sum(lot => lot.Quantity);
    }
}
