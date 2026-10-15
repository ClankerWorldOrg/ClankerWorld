using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IRepairOrdersWorld RepairOrdersWorld => new RepairOrdersAdapter(this);

    private sealed class RepairOrdersAdapter(WorldRuntime runtime) : IRepairOrdersWorld
    {
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor) => runtime.WornEquipmentItems(actor);
        public bool CanPrepareEquipmentRepair(string actor, AgentState person, InventoryLot lot) => runtime.CanPrepareEquipmentRepair(actor, person, lot);
        public ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor) => runtime.ContinueEquipmentRepair(actor);
        public string EquipmentRepairReceipt(string actor, long startedTick, string lot) => WorldRuntime.EquipmentRepairReceipt(actor, startedTick, lot);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public void CancelEquipmentRepair(string actor) => runtime.CancelEquipmentRepair(actor);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void RepairEquipment(string actor, AgentState person, string? requestedLotId = null, string? orderInstructionId = null) => runtime.RepairEquipment(actor, person, requestedLotId, orderInstructionId);
        public bool MayFollowOwnerOrder(string actor, OrderTask order) => runtime.MayFollowOwnerOrder(actor, order);
        public ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot) => runtime.EquipmentRepairSite(actor, lot);
        public int MissingRepairInputUnits(string actor, InventoryLot item) => runtime.MissingRepairInputUnits(actor, item);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasCarriedOwnItem(string actor, string kind) => runtime.HasCarriedOwnItem(actor, kind);
        public ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
    }
}
