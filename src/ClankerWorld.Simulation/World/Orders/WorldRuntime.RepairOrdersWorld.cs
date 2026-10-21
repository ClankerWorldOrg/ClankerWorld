namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IRepairOrdersWorld RepairOrdersWorld => new RepairOrdersAdapter(this);

    private sealed class RepairOrdersAdapter(WorldRuntime runtime) : IRepairOrdersWorld
    {
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor) => runtime.WornEquipmentItems(actor);
        public bool CanPrepareEquipmentRepair(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public string EquipmentRepairReceipt(string actor, long startedTick, string lot) => WorldRuntime.EquipmentRepairReceipt(actor, startedTick, lot);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void CancelEquipmentRepair(string actor) => runtime.CancelEquipmentRepair(actor);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.RepairOrderCandidateFor(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => runtime.RepairOrderBlockedReason(instruction);
        public void RepairEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string? requestedLotId = null, string? orderInstructionId = null) => runtime.RepairEquipment(actor, person, requestedLotId, orderInstructionId);
        public bool MayFollowOwnerOrder(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.EquipmentRepairSite(actor, lot);
        public int MissingRepairInputUnits(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot item) => runtime.MissingRepairInputUnits(actor, item);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasCarriedOwnItem(string actor, string kind) => runtime.HasCarriedOwnItem(actor, kind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
    }
}
