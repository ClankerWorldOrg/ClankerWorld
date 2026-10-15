using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICartCargoOrdersWorld
{
    WorldMap Map { get; }
    SocietyCheckpoint Society { get; }
    OrderStore Orders { get; }
    string LoadCartPrefix { get; }
    string UnloadCartGroundPrefix { get; }
    string UnloadCartPrefix { get; }
    bool IsAdult(string actor);
    bool IsCartCargoKind(string? kind);
    ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor);
    bool IsValidInstructionIdentifier(string? value);
    bool HasActiveContainerReservation(InventoryCheckpoint inventory, string containerId);
    bool CanPullHandcart(InventoryLot cart);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId);
    ClankerWorld.Simulation.World.GridPoint CartPosition(InventoryLot cart);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    bool CanLoadCartLot(string actor, AgentState person, InventoryLot lot);
    ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(OwnerQueuedInstruction instruction, AgentState person);
    int FreeCarryCapacity(string actor);
    int ContainerContentsQuantity(InventoryCheckpoint inventory, string containerId);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    bool ApplyHandcartCandidate(string actor, AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, ActionPlan? originalPlan = null);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string CartCargoOrderEffectId(OwnerQueuedInstruction instruction, int completed);
    string FoodKnowledgeKind(MapResource resource);
}
