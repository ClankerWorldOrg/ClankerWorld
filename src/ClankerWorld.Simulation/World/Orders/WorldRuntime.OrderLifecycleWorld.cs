namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderLifecycleWorld OrderLifecycleWorld => new OrderLifecycleAdapter(this);

    private sealed class OrderLifecycleAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IOrderLifecycleWorld
    {
        public void PruneClosedOrderHistory() => Runtime.PruneClosedOrderHistory();
        public global::ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.BoundProductionJob(Runtime.ProductionOrdersWorld, instruction);
        public void PauseProductionOrderJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.WorldProductionJob job) => Runtime.PauseProductionOrderJob(instruction, job);
        public void PauseConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.PauseConstructionForOrder(Runtime.ConstructionOrdersWorld, instruction);
        public void PauseExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.PauseExpansionForOrder(Runtime.ExpansionOrdersWorld, instruction);
    }
}
