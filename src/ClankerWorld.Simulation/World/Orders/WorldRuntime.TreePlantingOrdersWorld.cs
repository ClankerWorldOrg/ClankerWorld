using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ITreePlantingOrdersWorld TreePlantingOrdersWorld => new TreePlantingOrdersAdapter(this);

    private sealed class TreePlantingOrdersAdapter(WorldRuntime runtime) : ITreePlantingOrdersWorld
    {
        public WorldMap Map => runtime.map;
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public IReadOnlyDictionary<string, AgentState> Agents => runtime.agents;
        public ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, GridPoint origin) => runtime.PlantingSite(actor, origin);
        public ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(GridPoint point, (HashSet<GridPoint> Buildings, HashSet<GridPoint> Occupied) obstacles) => runtime.PlantingSiteRefusal(point, obstacles);
        public (System.Collections.Generic.HashSet<ClankerWorld.Simulation.World.GridPoint> Buildings, System.Collections.Generic.HashSet<ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles() => runtime.PlantingObstacles();
        public bool TreeAreaFull(GridPoint point) => runtime.TreeAreaFull(point);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public System.Collections.ObjectModel.ReadOnlyCollection<ClankerWorld.Simulation.World.GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public string PlantingSpecies(GridPoint site) => runtime.PlantingSpecies(site);
        public int PlantingSeedQuantity(InventoryLot lot) => runtime.PlantingSeedQuantity(lot);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => runtime.RecoverablePersonalGoods(actor, kind);
        public int PickupCarryCapacity(string actor, InventoryLot stock, GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public void SetOrderStatus(OwnerQueuedInstruction instruction, string status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public void CollectEquipment(string actor, AgentState person, string kind, GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public void MoveToward(string agentId, AgentState state, GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, GridPoint destination) => runtime.PlantTreeCore(planterId, species, seedLotId, destination);
        public void CreditOrderEffect(OwnerQueuedInstruction instruction, string effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string TreePlantingReceipt(string treeId) => WorldRuntime.TreePlantingReceipt(treeId);
    }
}
