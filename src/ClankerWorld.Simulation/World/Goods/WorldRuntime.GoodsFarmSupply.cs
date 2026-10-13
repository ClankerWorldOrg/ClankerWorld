using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    // Physical milling accounting includes usable stock promised to this
    // building. Those promises must not be selected as a second pickup.
    private int FarmSuppliedQuantity(string household, string farmhouse) => InventoryIndex.For(society.Checkpoint.Inventory)
        .StoredAt(farmhouse).Where(lot => lot.OwnerId == household && lot.ItemKind == FarmFieldRules.Grain).Sum(AvailableLotQuantity);

    private int FarmIncomingQuantity(string farmhouse) => InventoryIndex.For(society.Checkpoint.Inventory).Lots
        .Where(lot => lot.DeliveryBuildingId == farmhouse && lot.ItemKind == FarmFieldRules.Grain).Sum(AvailableLotQuantity);
}
