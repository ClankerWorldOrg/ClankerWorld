using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IToolRepairOrdersWorld
{
    long WorldTick { get; }
    SocietyCheckpoint Society { get; }
    ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, IReadOnlyList<string>? protectedToolIds = null);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    string? RepairTool(string actor, AgentState state, string lotId, IReadOnlyList<string>? protectedToolIds = null);
    string ToolRepairReceipt(string actor, long tick, string lot);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    bool IsAdult(string actor);
    int UnreservedQuantity(InventoryCheckpoint inventory, InventoryLot lot);
    ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
}
