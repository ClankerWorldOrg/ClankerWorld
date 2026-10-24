namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderLifecycleWorld OrderLifecycleWorld => new OrderLifecycleAdapter(this);

    private sealed class OrderLifecycleAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderLifecycleWorld
    {
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void PruneClosedOrderHistory() => Runtime.PruneClosedOrderHistory();
        public void PauseConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.PauseConstructionForOrder(Runtime.ConstructionOrdersWorld, instruction);
    }
}
