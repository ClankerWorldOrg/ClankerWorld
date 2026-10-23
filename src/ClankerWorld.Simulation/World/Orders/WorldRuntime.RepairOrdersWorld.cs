namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IRepairOrdersWorld RepairOrdersWorld => new RepairOrdersAdapter(this);

    private sealed class RepairOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), IRepairOrdersWorld
    {
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor) => Runtime.WornEquipmentItems(actor);
        public bool CanPrepareEquipmentRepair(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public global::ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => Runtime.ContinueEquipmentRepair(actor);
        public string EquipmentRepairReceipt(string actor, long startedTick, string lot) => WorldRuntime.EquipmentRepairReceipt(actor, startedTick, lot);
        public void CancelEquipmentRepair(string actor) => Runtime.CancelEquipmentRepair(actor);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? RepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.RepairOrderCandidateFor(instruction, person);
        public string RepairOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => Runtime.RepairOrderBlockedReason(instruction);
        public void RepairEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string? requestedLotId = null, string? orderInstructionId = null) => Runtime.RepairEquipment(actor, person, requestedLotId, orderInstructionId);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.EquipmentRepairSite(actor, lot);
        public int MissingRepairInputUnits(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot item) => Runtime.MissingRepairInputUnits(actor, item);
        public bool HasCarriedOwnItem(string actor, string kind) => Runtime.HasCarriedOwnItem(actor, kind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => Runtime.SharedItem(kind, actor, returnTo, returnRange);
    }
}
