using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryTransportWorld InventoryTransportWorld => new InventoryTransportAdapter(this);
    private sealed class InventoryTransportAdapter(WorldRuntime runtime) : IInventoryTransportWorld
    {
        public long WorldTick => runtime.WorldTick;
        public string CartBuildMaterialTransferId(long tick, string actor) => WorldRuntime.CartBuildMaterialTransferId(tick, actor);
        public string CartRepairReservationId(long tick, string actor, string material) => WorldRuntime.CartRepairReservationId(tick, actor, material);
    }
}
