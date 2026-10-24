namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IInventoryMarketsWorld InventoryMarketsWorld => new InventoryMarketsAdapter(this);
    private sealed class InventoryMarketsAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IInventoryMarketsWorld
    {
        public IReadOnlyList<TownRuntimeState> towns => Runtime.towns;
    }
}
