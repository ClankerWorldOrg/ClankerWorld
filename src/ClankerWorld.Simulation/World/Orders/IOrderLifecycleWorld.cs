using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IOrderLifecycleWorld
{
    OrderStore Orders { get; }
    bool IsActiveOrder(string status);
    void AppendEvent(string kind, string detail, GridPoint? eventPosition = null);
    ClankerWorld.Simulation.World.WorldProductionJob? BoundProductionJob(OwnerQueuedInstruction instruction);
    void PauseProductionOrderJob(OwnerQueuedInstruction instruction, WorldProductionJob job);
    void PauseConstructionForOrder(OwnerQueuedInstruction instruction);
    void PauseExpansionForOrder(OwnerQueuedInstruction instruction);
    void MarkCheckpointCurrent();
}
