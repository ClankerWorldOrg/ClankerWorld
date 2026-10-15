using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartCargoOrdersWorld CartCargoOrdersWorld => new CartCargoOrdersAdapter(this);

    private sealed class CartCargoOrdersAdapter(WorldRuntime runtime) : ICartCargoOrdersWorld
    {
        public WorldMap Map => runtime.map;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public OrderStore Orders => runtime.orderStore;
        public string LoadCartPrefix => WorldRuntime.LoadCartPrefix;
        public string UnloadCartGroundPrefix => WorldRuntime.UnloadCartGroundPrefix;
        public string UnloadCartPrefix => WorldRuntime.UnloadCartPrefix;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool IsCartCargoKind(string? kind) => WorldRuntime.IsCartCargoKind(kind);
        public ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor) => runtime.ParseCartOrder(text, actor);
        public bool IsValidInstructionIdentifier(string? value) => WorldRuntime.IsValidInstructionIdentifier(value);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId) => WorldRuntime.HasActiveContainerReservation(inventory, containerId);
        public bool CanPullHandcart(InventoryLot cart) => runtime.CanPullHandcart(cart);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId) => runtime.CartCargoLots(actor, cartId);
        public ClankerWorld.Simulation.World.GridPoint CartPosition(InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public bool CanLoadCartLot(string actor, AgentState person, InventoryLot lot) => runtime.CanLoadCartLot(actor, person, lot);
        public ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(OwnerQueuedInstruction instruction, AgentState person) => runtime.CartForOrder(instruction, person);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public int ContainerContentsQuantity(InventoryCheckpoint inventory, string containerId) => WorldRuntime.ContainerContentsQuantity(inventory, containerId);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartCargoOrderEffectId(OwnerQueuedInstruction instruction, int completed) => WorldRuntime.CartCargoOrderEffectId(instruction, completed);
        public string FoodKnowledgeKind(MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
    }
}
