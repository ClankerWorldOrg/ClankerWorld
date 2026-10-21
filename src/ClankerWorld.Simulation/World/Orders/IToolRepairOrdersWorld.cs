namespace ClankerWorld.Simulation.World;

internal interface IToolRepairOrdersWorld
{
    global::ClankerWorld.Simulation.Kernel.InventoryLot[] RepairableTools(string actor, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null);
    string[] ToolRepairOrderTargets(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? ToolRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string ToolRepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    string? RepairTool(string actor, global::ClankerWorld.Simulation.World.AgentState state, string lotId, global::System.Collections.Generic.IReadOnlyList<string>? protectedToolIds = null);
    string ToolRepairReceipt(string actor, long tick, string lot);
    long WorldTick { get; }
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    int UnreservedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.World.PlacedBuilding? HouseholdBuildingWithTag(string householdId, string tag);
}
