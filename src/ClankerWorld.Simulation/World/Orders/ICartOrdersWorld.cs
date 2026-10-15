using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICartOrdersWorld
{
    SocietyCheckpoint Society { get; }
    WorldMap Map { get; }
    IReadOnlyList<HandcartHitch> Hitches { get; }
    string AttachCartPrefix { get; }
    AnimalWorldState Animals { get; }
    OrderStore Orders { get; }
    bool IsAdult(string actor);
    bool IsValidInstructionIdentifier(string? value);
    ClankerWorld.Simulation.World.GridPoint CartPosition(InventoryLot cart);
    bool IsCartCargoOrder(string action);
    ClankerWorld.Simulation.Kernel.InventoryLot? CartForCargoOrder(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Kernel.InventoryLot? AttachedHandcart(string actor);
    ClankerWorld.Simulation.Kernel.InventoryLot? CartForRepairOrder(string actor, AgentState person);
    bool CanPullHandcart(InventoryLot cart);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    ClankerWorld.Simulation.Cognition.CognitionCandidate? CartCargoOrderCandidateFor(OwnerQueuedInstruction instruction, AgentState person);
    ClankerWorld.Simulation.Cognition.CognitionCandidate CartRepairOrderCandidateFor(OwnerQueuedInstruction instruction, InventoryLot cart);
    string? CartCargoOrderBlocker(OwnerQueuedInstruction instruction, AgentState person);
    string? CartRepairOrderBlocker(string actor, AgentState person, InventoryLot cart);
    void ExecuteCartCargoOrderStep(OwnerQueuedInstruction instruction, AgentState person);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void ExecuteCartRepairOrderStep(OwnerQueuedInstruction instruction, AgentState person, InventoryLot cart);
    bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string CartOrderEffectId(OwnerQueuedInstruction instruction);
    string FoodKnowledgeKind(MapResource resource);
}
