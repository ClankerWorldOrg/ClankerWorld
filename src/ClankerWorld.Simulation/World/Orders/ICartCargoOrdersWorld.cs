namespace ClankerWorld.Simulation.World;

internal interface ICartCargoOrdersWorld
{
    bool IsAdult(string actor);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    string FoodKnowledgeKind(global::ClankerWorld.Simulation.World.MapResource resource);
    bool IsCartCargoKind(string? kind);
    global::ClankerWorld.Simulation.World.OrderTask? ParseCartOrder(string text, string actor);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    bool HasActiveContainerReservation(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    bool CanPullHandcart(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    int CargoQuantity(string cartId);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> CartCargoLots(string actor, string cartId);
    global::ClankerWorld.Simulation.World.GridPoint CartPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    bool CanLoadCartLot(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CartForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CargoLotForOrder(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.Kernel.InventoryLot cart);
    string? CartCargoOrderBlocker(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    int ContainerContentsQuantity(global::ClankerWorld.Simulation.Kernel.InventoryCheckpoint inventory, string containerId);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    OrderStore Orders { get; }
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    string LoadCartPrefix { get; }
    string UnloadCartGroundPrefix { get; }
    string UnloadCartPrefix { get; }
    bool ApplyHandcartCandidate(string actor, global::ClankerWorld.Simulation.World.AgentState person, string candidateId, string? targetCartId = null, int maximumQuantity = 2147483647, global::ClankerWorld.Simulation.World.ActionPlan? originalPlan = null);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string CartCargoOrderEffectId(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, int completed);
}
