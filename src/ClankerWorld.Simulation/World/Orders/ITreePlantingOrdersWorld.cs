using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ITreePlantingOrdersWorld
{
    WorldMap Map { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    int ResourceInteractionRange { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyDictionary<string, AgentState> Agents { get; }
    ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, GridPoint origin);
    ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(GridPoint point, (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) obstacles);
    (System.Collections.Generic.HashSet<ClankerWorld.Simulation.World.GridPoint> Buildings, System.Collections.Generic.HashSet<ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles();
    bool TreeAreaFull(GridPoint point);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange);
    System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    string PlantingSpecies(GridPoint site);
    int PlantingSeedQuantity(InventoryLot lot);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null);
    int PickupCarryCapacity(string actor, InventoryLot stock, GridPoint destination, int destinationRange = 0);
    ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot);
    ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0);
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    int FreeCarryCapacity(string actor);
    void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false);
    ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647);
    void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null);
    void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100);
    ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, GridPoint destination);
    void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units);
    string TreePlantingReceipt(string treeId);
}
