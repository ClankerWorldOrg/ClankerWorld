using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryTownsWorld InventoryTownsWorld => new InventoryTownsAdapter(this);
    private sealed class InventoryTownsAdapter(WorldRuntime runtime) : IInventoryTownsWorld
    {
        public InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery) => WorldRuntime.ReserveTownProjectDelivery(inventory, townId, project, delivery);
        public long WorldTick => runtime.WorldTick;
    }
}
