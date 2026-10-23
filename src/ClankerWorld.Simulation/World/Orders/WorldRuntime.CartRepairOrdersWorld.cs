namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartRepairOrdersWorld CartRepairOrdersWorld => new CartRepairOrdersAdapter(this);

    private sealed class CartRepairOrdersAdapter(WorldRuntime runtime) : ICartRepairOrdersWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? CartRepairCollectionPlan(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.CartRepairCollectionPlan(actor, cart);
        public global::System.Collections.Generic.IReadOnlyList<string> CartRepairKinds => WorldRuntime.CartRepairKinds;
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public string RepairCartPrefix => global::ClankerWorld.Simulation.World.HandcartRepairChoice.Prefix;
        public string CollectCartRepairPrefix => global::ClankerWorld.Simulation.World.HandcartRepairInputChoice.Prefix;
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep StockCollectionStep(global::ClankerWorld.Simulation.Kernel.InventoryLot stock, int quantity) => runtime.StockCollectionStep(stock, quantity);
        public global::System.Collections.Generic.List<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? PlanInputCollection(string actor, global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep> inputs, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.PlanInputCollection(actor, inputs, returnTo, returnRange);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.CartRepairOrderCandidateFor(instruction, cart);
        public bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartRepairOrderReceipt(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, long tick) => WorldRuntime.CartRepairOrderReceipt(instruction, tick);
        public long WorldTick => runtime.WorldTick;
    }
}
