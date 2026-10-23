namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartCargoOrdersWorld CartCargoOrdersWorld => new CartCargoOrdersAdapter(this);

    private sealed class CartCargoOrdersAdapter(WorldRuntime runtime) : ICartCargoOrdersWorld
    {
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
        public bool IsCartCargoKind(string? kind) => WorldRuntime.IsCartCargoKind(kind);
        public global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => runtime.ParseCartOrder(text, actor);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public bool CanPullHandcart(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => TransportSystem.CanPullHandcart(runtime.HandcartWorld, cart);
        public int CargoQuantity(string cartId) => runtime.CargoQuantity(cartId);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId) => runtime.CartCargoLots(actor, cartId);
        public global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool CanLoadCartLot(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => TransportSystem.CanLoadCartLot(runtime.HandcartWorld, actor, person, lot);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartForOrder(instruction, person);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CargoLotForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart) => runtime.CargoLotForOrder(instruction, person, cart);
        public string? CartCargoOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.CartCargoOrderBlocker(instruction, person);
        public int ContainerContentsQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId) => WorldRuntime.ContainerContentsQuantity(inventory, containerId);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public OrderStore Orders => runtime.orderStore;
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public string LoadCartPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.LoadPrefix;
        public string UnloadCartGroundPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.GroundPrefix;
        public string UnloadCartPrefix => global::ClankerWorld.Simulation.World.HandcartCargoChoice.UnloadPrefix;
        public bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartCargoOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, int completed) => WorldRuntime.CartCargoOrderEffectId(instruction, completed);
    }
}
