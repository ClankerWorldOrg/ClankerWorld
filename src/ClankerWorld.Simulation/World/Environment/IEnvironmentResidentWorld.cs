using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IEnvironmentResidentWorld : IEnvironmentWorld
{
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    void AddAgent(string id, AgentState state);
    void SetAgent(string id, AgentState state);
    void RemoveAgent(string id);
    void AddDeceasedAgent(string id, DeceasedAgentState state);
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
