using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryExecutionWorld InventoryExecutionWorld => new InventoryExecutionAdapter(this);
    private sealed class InventoryExecutionAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryExecutionWorld
    {
        public IReadOnlyList<TownRuntimeState> Towns => Runtime.towns;
        public int ReservedShopStorageSpace(string building, InventoryCheckpoint inventory) => Runtime.ReservedShopStorageSpace(building, inventory);
        public int ReservedShopCarrySpace(string actor, InventoryCheckpoint inventory) => Runtime.ReservedShopCarrySpace(actor, inventory);
        public int HorseCargoCapacity(string actor) => Runtime.HorseCargoCapacity(actor);
    }
}
