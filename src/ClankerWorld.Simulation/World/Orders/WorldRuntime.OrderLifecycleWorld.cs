namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderLifecycleWorld OrderLifecycleWorld => new OrderLifecycleAdapter(this);

    private sealed class OrderLifecycleAdapter(WorldRuntime runtime) : IOrderLifecycleWorld
    {
        public OrderStore Orders => runtime.orderStore;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null) => runtime.AppendEvent(payload, eventPosition);
        public void PruneClosedOrderHistory() => runtime.PruneClosedOrderHistory();
        public global::ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.BoundProductionJob(runtime.ProductionOrdersWorld, instruction);
        public void PauseProductionOrderJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.WorldProductionJob job) => runtime.PauseProductionOrderJob(instruction, job);
        public void PauseConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.PauseConstructionForOrder(runtime.ConstructionOrdersWorld, instruction);
        public void PauseExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.PauseExpansionForOrder(runtime.ExpansionOrdersWorld, instruction);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
