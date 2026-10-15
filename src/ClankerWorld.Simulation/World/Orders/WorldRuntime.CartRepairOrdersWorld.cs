using InputCollectionStep = ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartRepairOrdersWorld CartRepairOrdersWorld => new CartRepairOrdersAdapter(this);

    private sealed class CartRepairOrdersAdapter(WorldRuntime runtime) : ICartRepairOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public WorldMap Map => runtime.map;
        public System.Collections.Generic.IReadOnlyList<string> CartRepairKinds => WorldRuntime.CartRepairKinds;
        public string RepairCartPrefix => WorldRuntime.RepairCartPrefix;
        public string CollectCartRepairPrefix => WorldRuntime.CollectCartRepairPrefix;
        public long WorldTick => runtime.WorldTick;
        public bool IsValidInstructionIdentifier(string? value) => WorldRuntime.IsValidInstructionIdentifier(value);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public ClankerWorld.Simulation.World.GridPoint CartPosition(InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool HasCarriedMaterial(string actor, string itemKind, int quantity) => runtime.HasCarriedMaterial(actor, itemKind, quantity);
        public ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep StockCollectionStep(InventoryLot stock, int quantity) => runtime.StockCollectionStep(stock, quantity);
        public System.Collections.Generic.List<ClankerWorld.Simulation.World.WorldRuntime.InputCollectionStep>? PlanInputCollection(string actor, IReadOnlyList<InputCollectionStep> inputs, GridPoint? returnTo = null, int returnRange = 0) => runtime.PlanInputCollection(actor, inputs, returnTo, returnRange);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartRepairOrderReceipt(OwnerQueuedInstruction instruction, long tick) => WorldRuntime.CartRepairOrderReceipt(instruction, tick);
    }
}
