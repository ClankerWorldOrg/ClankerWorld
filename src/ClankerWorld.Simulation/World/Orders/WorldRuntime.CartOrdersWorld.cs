namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartOrdersWorld CartOrdersWorld => new CartOrdersAdapter(this);

    private sealed class CartOrdersAdapter(WorldRuntime runtime) : ICartOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public bool IsCartCargoOrder(string action) => WorldRuntime.IsCartCargoOrder(action);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForCargoOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartForCargoOrder(instruction, person);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(runtime.HandcartWorld, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForRepairOrder(string actor, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartForRepairOrder(actor, person);
        public bool CanPullHandcart(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => TransportSystem.CanPullHandcart(runtime.HandcartWorld, cart);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.HandcartHitch> HandcartHitches => runtime.handcartHitches;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartCargoOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartCargoOrderCandidateFor(instruction, person);
        public string? CartOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartOrderBlocker(instruction, person);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartForOrder(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.CartRepairOrderCandidateFor(instruction, cart);
        public string AttachCartPrefix => global::ClankerWorld.Simulation.World.HandcartControlChoice.AttachPrefix;
        public string? CartCargoOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartCargoOrderBlocker(instruction, person);
        public string? CartRepairOrderBlocker(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.CartRepairOrderBlocker(actor, person, cart);
        public global::ClankerWorld.Simulation.World.AnimalWorldState AnimalWorld => runtime.animalWorld;
        public void ExecuteCartCargoOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.ExecuteCartCargoOrderStep(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public OrderStore Orders => runtime.orderStore;
        public void ExecuteCartRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.ExecuteCartRepairOrderStep(instruction, person, cart);
        public bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.CartOrderEffectId(instruction);
    }
}
