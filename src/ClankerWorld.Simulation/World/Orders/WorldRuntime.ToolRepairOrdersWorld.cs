using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IToolRepairOrdersWorld ToolRepairOrdersWorld => new ToolRepairOrdersAdapter(this);

    private sealed class ToolRepairOrdersAdapter(WorldRuntime runtime) : IToolRepairOrdersWorld
    {
        public long WorldTick => runtime.WorldTick;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, IReadOnlyList<string>? protectedToolIds = null) => runtime.RepairableTools(actor, protectedToolIds);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string? RepairTool(string actor, AgentState state, string lotId, IReadOnlyList<string>? protectedToolIds = null) => runtime.RepairTool(actor, state, lotId, protectedToolIds);
        public string ToolRepairReceipt(string actor, long tick, string lot) => WorldRuntime.ToolRepairReceipt(actor, tick, lot);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public int UnreservedQuantity(InventoryCheckpoint inventory, InventoryLot lot) => WorldRuntime.UnreservedQuantity(inventory, lot);
        public ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
    }
}
