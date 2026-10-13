using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private int SmithStoredQuantity(string household, string smith, string kind) => InventoryIndex.For(society.Checkpoint.Inventory).StoredAt(smith)
        .Where(lot => lot.OwnerId == household && lot.ItemKind == kind).Sum(AvailableLotQuantity);

    private int SmithIncomingQuantity(string smith, string kind) => InventoryIndex.For(society.Checkpoint.Inventory).Lots
        .Where(lot => lot.DeliveryBuildingId == smith && lot.ItemKind == kind).Sum(AvailableLotQuantity);
}
