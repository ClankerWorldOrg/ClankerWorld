using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IOrderLifecycleWorld OrderLifecycleWorld => new OrderLifecycleAdapter(this);

    private sealed class OrderLifecycleAdapter(WorldRuntime runtime) : IOrderLifecycleWorld
    {
        public OrderStore Orders => runtime.orderStore;
        public bool IsActiveOrder(string status) => WorldRuntime.IsActiveOrder(status);
        public void AppendEvent(string kind, string detail, GridPoint? eventPosition = null) => runtime.AppendEvent(kind, detail, eventPosition);
        public ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(OwnerQueuedInstruction instruction) => runtime.BoundProductionJob(instruction);
        public void PauseProductionOrderJob(OwnerQueuedInstruction instruction, WorldProductionJob job) => runtime.PauseProductionOrderJob(instruction, job);
        public void PauseConstructionForOrder(OwnerQueuedInstruction instruction) => runtime.PauseConstructionForOrder(instruction);
        public void PauseExpansionForOrder(OwnerQueuedInstruction instruction) => runtime.PauseExpansionForOrder(instruction);
        public void MarkCheckpointCurrent() => runtime.checkpointSchemaVersion = StateSchemaVersion;
    }
}
