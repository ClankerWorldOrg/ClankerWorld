using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IEnvironmentResidentWorld : IEnvironmentWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, InhabitantState> Inhabitants { get; }
    void AddInhabitant(string id, InhabitantState state);
    void SetInhabitant(string id, InhabitantState state);
    void RemoveInhabitant(string id);
    void AddDeceasedInhabitant(string id, DeceasedInhabitantState state);
    OwnerQueuedInstruction? PendingInstructionFor(string id);
    void CancelConstructionForOrder(OwnerQueuedInstruction instruction);
    void CancelExpansionForOrder(OwnerQueuedInstruction instruction);
    void CancelProductionForOrder(OwnerQueuedInstruction instruction);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason);
    void ApplyInventoryTransition(Func<InventoryCheckpoint, InventoryCheckpoint> transition);
    void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome);
    string? TownForResident(string id);
    string? RecordBoatDeath(string id);
    void RemoveTownResident(string id);
}
