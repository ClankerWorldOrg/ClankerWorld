using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IEnvironmentAgentWorld : IEnvironmentWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    OwnerQueuedInstruction? PendingInstructionFor(string id);
    void CancelConstructionForOrder(OwnerQueuedInstruction instruction);
    void CancelExpansionForOrder(OwnerQueuedInstruction instruction);
    void CancelProductionForOrder(OwnerQueuedInstruction instruction);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason);
    void RunInventoryCommand(InventorySystem.Command command);
    void FinishIdentityMoment(string id, AgentIdentityMomentKind kind, string outcome);
    string? TownForResident(string id);
    string? RecordBoatDeath(string id);
    void RemoveTownResident(string id);
}
