using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryMarketsWorld InventoryMarketsWorld => new InventoryMarketsAdapter(this);
    private sealed class InventoryMarketsAdapter(WorldRuntime runtime) : IInventoryMarketsWorld
    {
        public IReadOnlyList<TownRuntimeState> towns => runtime.towns;
        public long WorldTick => runtime.WorldTick;

    }
}
