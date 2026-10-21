namespace ClankerWorld.Simulation.World;

internal interface ITreePlantingOrdersWorld
{
    global::ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, global::ClankerWorld.Simulation.World.GridPoint origin);
    global::ClankerWorld.Simulation.World.WorldMap Map { get; }
    global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns { get; }
    global::ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(global::ClankerWorld.Simulation.World.GridPoint point, (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) obstacles);
    (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles();
    bool TreeAreaFull(global::ClankerWorld.Simulation.World.GridPoint point);
    bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange);
    int ResourceInteractionRange { get; }
    global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    string PlantingSpecies(global::ClankerWorld.Simulation.World.GridPoint site);
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    int PlantingSeedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null);
    int PickupCarryCapacity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0);
    global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents { get; }
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0);
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    global::ClankerWorld.Simulation.World.GridPoint? TreeOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    string TreeOrderSpecies(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.GridPoint site);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedTreeOrderSeed(string actor, string kind);
    int FreeCarryCapacity(string actor);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CollectableTreeOrderSeed(string actor, string kind, global::ClankerWorld.Simulation.World.GridPoint site);
    global::ClankerWorld.Simulation.World.OrderSystem.TreeOrderPlan? TreePlantingOrderPlan(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false);
    string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person);
    global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647);
    void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    global::ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, global::ClankerWorld.Simulation.World.GridPoint destination);
    void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units);
    string TreePlantingReceipt(string treeId);
}
