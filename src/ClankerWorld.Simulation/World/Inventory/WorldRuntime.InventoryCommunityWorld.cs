namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryCommunityWorld InventoryCommunityWorld => new InventoryCommunityAdapter(this);
    private sealed class InventoryCommunityAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryCommunityWorld
    {
    }
}
