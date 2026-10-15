using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ICartOrdersWorld CartOrdersWorld => new CartOrdersAdapter(this);

    private sealed class CartOrdersAdapter(WorldRuntime runtime) : ICartOrdersWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public WorldMap Map => runtime.map;
        public IReadOnlyList<HandcartHitch> Hitches => runtime.handcartHitches;
        public string AttachCartPrefix => WorldRuntime.AttachCartPrefix;
        public AnimalWorldState Animals => runtime.animalWorld;
        public OrderStore Orders => runtime.orderStore;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool IsValidInstructionIdentifier(string? value) => WorldRuntime.IsValidInstructionIdentifier(value);
        public ClankerWorld.Simulation.World.GridPoint CartPosition(InventoryLot cart) => WorldRuntime.CartPosition(cart);
        public bool IsCartCargoOrder(string action) => WorldRuntime.IsCartCargoOrder(action);
        public ClankerWorld.Simulation.Kernel.InventoryLot? CartForCargoOrder(OwnerQueuedInstruction instruction, AgentState person) => runtime.CartForCargoOrder(instruction, person);
        public ClankerWorld.Simulation.Kernel.InventoryLot? AttachedHandcart(string actor) => runtime.AttachedHandcart(actor);
        public ClankerWorld.Simulation.Kernel.InventoryLot? CartForRepairOrder(string actor, AgentState person) => runtime.CartForRepairOrder(actor, person);
        public bool CanPullHandcart(InventoryLot cart) => runtime.CanPullHandcart(cart);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate? CartCargoOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person) => runtime.CartCargoOrderCandidateFor(instruction, person);
        public ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(OwnerQueuedInstruction instruction, InventoryLot cart) => runtime.CartRepairOrderCandidateFor(instruction, cart);
        public string? CartCargoOrderBlocker(OwnerQueuedInstruction instruction, AgentState person) => runtime.CartCargoOrderBlocker(instruction, person);
        public string? CartRepairOrderBlocker(string actor, AgentState person, InventoryLot cart) => runtime.CartRepairOrderBlocker(actor, person, cart);
        public void ExecuteCartCargoOrderStep(OwnerQueuedInstruction instruction, AgentState person) => runtime.ExecuteCartCargoOrderStep(instruction, person);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public void ExecuteCartRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart) => runtime.ExecuteCartRepairOrderStep(instruction, person, cart);
        public bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null) => runtime.ApplyHandcartCandidate(actor, person, candidateId, targetCartId, maximumQuantity, originalPlan);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string CartOrderEffectId(OwnerQueuedInstruction instruction) => WorldRuntime.CartOrderEffectId(instruction);
        public string FoodKnowledgeKind(MapResource resource) => WorldRuntime.FoodKnowledgeKind(resource);
    }
}
