using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryExecutionWorld InventoryExecutionWorld => new InventoryExecutionAdapter(this);
    private sealed class InventoryExecutionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryExecutionWorld
    {
        public int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory) => Runtime.marketSystem.ReservedShopStorageSpace(Runtime.MarketShopWorld, building, inventory);
        public int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory) => Runtime.marketSystem.ReservedShopCarrySpace(Runtime.MarketShopWorld, actor, inventory);
        public int HorseCargoCapacity(string actor) => Runtime.HorseCargoCapacity(actor);
    }
}
