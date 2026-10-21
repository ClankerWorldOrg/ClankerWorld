namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IToolRepairOrdersWorld ToolRepairOrdersWorld => new ToolRepairOrdersAdapter(this);

    private sealed class ToolRepairOrdersAdapter(WorldRuntime runtime) : IToolRepairOrdersWorld
    {
        public global::ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null) => runtime.RepairableTools(actor, protectedToolIds);
        public string[] ToolRepairOrderTargets(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ToolRepairOrderTargets(instruction);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ToolRepairOrderCandidateFor(instruction);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string ToolRepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.ToolRepairOrderBlockedReason(instruction);
        public string? RepairTool(string actor, global::ClankerWorld.Simulation.World.AgentState state, string lotId, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null) => runtime.RepairTool(actor, state, lotId, protectedToolIds);
        public string ToolRepairReceipt(string actor, long tick, string lot) => WorldRuntime.ToolRepairReceipt(actor, tick, lot);
        public long WorldTick => runtime.WorldTick;
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public int UnreservedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.UnreservedQuantity(inventory, lot);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag) => runtime.HouseholdBuildingWithTag(householdId, tag);
    }
}
