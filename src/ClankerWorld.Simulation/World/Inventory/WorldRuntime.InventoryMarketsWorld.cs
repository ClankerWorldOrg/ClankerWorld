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
