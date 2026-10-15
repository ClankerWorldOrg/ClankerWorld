using System.Collections.ObjectModel;
using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryCommunityWorld InventoryCommunityWorld => new InventoryCommunityAdapter(this);
    private sealed class InventoryCommunityAdapter(WorldRuntime runtime) : IInventoryCommunityWorld
    {
        public long WorldTick => runtime.WorldTick;

    }
}
