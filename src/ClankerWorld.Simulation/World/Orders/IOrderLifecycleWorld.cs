namespace ClankerWorld.Simulation.World;

internal interface IOrderLifecycleWorld
{
    OrderStore Orders { get; }
    bool IsActiveOrder(string status);
    void AppendEvent(global::ClankerWorld.Simulation.World.WorldEventPayload payload, global::ClankerWorld.Simulation.World.GridPoint? eventPosition = null);
    void PruneClosedOrderHistory();
    global::ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void PauseProductionOrderJob(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.WorldProductionJob job);
    void PauseConstructionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void PauseExpansionForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void MarkCheckpointCurrent();
}
