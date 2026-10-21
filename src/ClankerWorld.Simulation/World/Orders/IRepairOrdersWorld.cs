namespace ClankerWorld.Simulation.World;

internal interface IRepairOrdersWorld
{
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor);
    bool CanPrepareEquipmentRepair(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    string EquipmentRepairReceipt(string actor, long startedTick, string lot);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    void CancelEquipmentRepair(string actor);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction);
    void RepairEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string? requestedLotId = null, string? orderInstructionId = null);
    bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    global::ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    int MissingRepairInputUnits(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot item);
    int FreeCarryCapacity(string actor);
    bool HasCarriedOwnItem(string actor, string kind);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
}
