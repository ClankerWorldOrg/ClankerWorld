using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IRepairOrdersWorld
{
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> WornEquipmentItems(string actor);
    bool CanPrepareEquipmentRepair(string actor, AgentState person, InventoryLot lot);
    ClankerWorld.Simulation.World.EquipmentRepairWork? ContinueEquipmentRepair(string actor);
    string EquipmentRepairReceipt(string actor, long startedTick, string lot);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    void CancelEquipmentRepair(string actor);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void RepairEquipment(string actor, AgentState person, string? requestedLotId = null, string? orderInstructionId = null);
    bool MayFollowOwnerOrder(string actor, OrderTask order);
    ClankerWorld.Simulation.World.PlacedBuilding? EquipmentRepairSite(string actor, InventoryLot lot);
    int MissingRepairInputUnits(string actor, InventoryLot item);
    int FreeCarryCapacity(string actor);
    bool HasCarriedOwnItem(string actor, string kind);
    ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
}
