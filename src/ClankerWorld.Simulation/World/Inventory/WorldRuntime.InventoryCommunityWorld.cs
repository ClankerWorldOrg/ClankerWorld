using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Society;
using System.Text.Json.Serialization;
using ClankerWorld.Simulation.Cognition;
namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryCommunityWorld InventoryCommunityWorld => new InventoryCommunityAdapter(this);
    private sealed class InventoryCommunityAdapter(WorldRuntime runtime) : IInventoryCommunityWorld
    {
        public long WorldTick => runtime.WorldTick;
    }
}
