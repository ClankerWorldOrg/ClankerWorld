namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartOrdersWorld CartOrdersWorld => new CartOrdersAdapter(this);

    private sealed class CartOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), ICartOrdersWorld
    {
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public bool IsCartCargoOrder(string action) => WorldRuntime.IsCartCargoOrder(action);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForCargoOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartForCargoOrder(instruction, person);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? AttachedHandcart(string actor) => TransportSystem.AttachedHandcart(Runtime.HandcartWorld, actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForRepairOrder(string actor, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartForRepairOrder(actor, person);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.HandcartHitch> HandcartHitches => Runtime.handcartHitches;
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate? CartCargoOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.CartCargoOrderCandidateFor(instruction, person);
        public global::ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.CartRepairOrderCandidateFor(instruction, cart);
        public string AttachCartPrefix => WorldRuntime.AttachCartPrefix;
        public string? CartRepairOrderBlocker(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.CartRepairOrderBlocker(actor, person, cart);
        public global::ClankerWorld.Simulation.World.AnimalWorldState AnimalWorld => Runtime.animalWorld;
        public void ExecuteCartCargoOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => Runtime.ExecuteCartCargoOrderStep(instruction, person);
        public void ExecuteCartRepairOrderStep(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => Runtime.ExecuteCartRepairOrderStep(instruction, person, cart);
        public string CartOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction) => WorldRuntime.CartOrderEffectId(instruction);
    }
}
