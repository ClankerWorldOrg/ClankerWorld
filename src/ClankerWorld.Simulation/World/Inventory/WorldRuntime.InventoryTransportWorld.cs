using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryTransportWorld InventoryTransportWorld => new InventoryTransportAdapter(this);
    private sealed class InventoryTransportAdapter(WorldRuntime runtime) : IInventoryTransportWorld
    {
        public long WorldTick => runtime.WorldTick;
        public string CartRepairReservationId(long tick, string actor, string material) => WorldRuntime.CartRepairReservationId(tick, actor, material);
    }
}
