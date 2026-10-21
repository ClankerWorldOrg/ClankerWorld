namespace ClankerWorld.Simulation.World;

internal interface ICartRepairOrdersWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? CartRepairCollectionPlan(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.Generic.IReadOnlyList<string> CartRepairKinds { get; }
    bool HasCarriedMaterial(string actor, string itemKind, int quantity);
    string RepairCartPrefix { get; }
    string CollectCartRepairPrefix { get; }
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep StockCollectionStep(global::ClankerWorld.Simulation.Kernel.InventoryLot stock, int quantity);
    global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? PlanInputCollection(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep> inputs, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    int FreeCarryCapacity(string actor);
    void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string CartRepairOrderReceipt(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, long tick);
    long WorldTick { get; }
}
