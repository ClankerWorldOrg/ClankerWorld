using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryTownsWorld InventoryTownsWorld => new InventoryTownsAdapter(this);
    private sealed class InventoryTownsAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryTownsWorld
    {
        public InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery) => WorldRuntime.ReserveTownProjectDelivery(inventory, townId, project, delivery);
    }
}
