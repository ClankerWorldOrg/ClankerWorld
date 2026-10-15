using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryTownsWorld InventoryTownsWorld => new InventoryTownsAdapter(this);
    private sealed class InventoryTownsAdapter(WorldRuntime runtime) : IInventoryTownsWorld
    {
        public long WorldTick => runtime.WorldTick;
        public InventoryCheckpoint ReserveTownProjectDelivery(InventoryCheckpoint inventory, string townId, TownProject project, TownProjectDelivery delivery) => WorldRuntime.ReserveTownProjectDelivery(inventory, townId, project, delivery);
    }
}
