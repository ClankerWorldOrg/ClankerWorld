using InputCollectionStep = ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICartRepairOrdersWorld
{
    SocietyCheckpoint Society { get; }
    WorldMap Map { get; }
    System.Collections.Generic.IReadOnlyList<string> CartRepairKinds { get; }
    string RepairCartPrefix { get; }
    string CollectCartRepairPrefix { get; }
    long WorldTick { get; }
    bool IsValidInstructionIdentifier(string? value);
    bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId);
    ClankerWorld.Simulation.World.GridPoint CartPosition(InventoryLot cart);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    bool HasCarriedMaterial(string actor, string itemKind, int quantity);
    ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
    ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep StockCollectionStep(InventoryLot stock, int quantity);
    System.Collections.Generic.List<ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? PlanInputCollection(string actor, IReadOnlyList<InputCollectionStep> inputs, GridPoint? returnTo = null, int returnRange = 0);
    int FreeCarryCapacity(string actor);
    void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string CartRepairOrderReceipt(OwnerQueuedInstruction instruction, long tick);
}
