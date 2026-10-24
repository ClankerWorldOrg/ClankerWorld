namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IToolRepairOrdersWorld ToolRepairOrdersWorld => new ToolRepairOrdersAdapter(this);

    private sealed class ToolRepairOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IToolRepairOrdersWorld
    {
        public global::ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null) => Runtime.RepairableTools(actor, protectedToolIds);
        public string[] ToolRepairOrderTargets(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => OrderSystem.ToolRepairOrderTargets(Runtime.ToolRepairOrdersWorld, instruction);
        public string? RepairTool(string actor, global::ClankerWorld.Simulation.World.AgentState state, string lotId, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null) => Runtime.RepairTool(actor, state, lotId, protectedToolIds);
        public string ToolRepairReceipt(string actor, long tick, string lot) => WorldRuntime.ToolRepairReceipt(actor, tick, lot);
        public int UnreservedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.UnreservedQuantity(inventory, lot);
    }
}
