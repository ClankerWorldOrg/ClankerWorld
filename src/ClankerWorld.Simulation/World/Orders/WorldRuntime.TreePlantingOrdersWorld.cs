namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ITreePlantingOrdersWorld TreePlantingOrdersWorld => new TreePlantingOrdersAdapter(this);

    private sealed class TreePlantingOrdersAdapter(WorldRuntime runtime) : ITreePlantingOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, global::ClankerWorld.Simulation.World.GridPoint origin) => runtime.PlantingSite(actor, origin);
        public global::ClankerWorld.Simulation.World.WorldMap Map => runtime.map;
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns => runtime.towns;
        public global::ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(global::ClankerWorld.Simulation.World.GridPoint point, (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) obstacles) => runtime.PlantingSiteRefusal(point, obstacles);
        public (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles() => runtime.PlantingObstacles();
        public bool TreeAreaFull(global::ClankerWorld.Simulation.World.GridPoint point) => runtime.TreeAreaFull(point);
        public bool IsWithinInteractionRange(global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint destination, int interactionRange) => runtime.IsWithinInteractionRange(origin, destination, interactionRange);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public global::System.Collections.ObjectModel.ReadOnlyCollection<global::ClankerWorld.Simulation.World.GridPoint> Reach(string actor, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.Reach(actor, target, purpose, range, load, swimming, obstacles);
        public string PlantingSpecies(global::ClankerWorld.Simulation.World.GridPoint site) => runtime.PlantingSpecies(site);
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public int PlantingSeedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.PlantingSeedQuantity(lot);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => runtime.RecoverablePersonalGoods(actor, kind);
        public int PickupCarryCapacity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0) => runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::ClankerWorld.Simulation.World.AgentState> Agents => runtime.agents;
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => runtime.SharedItem(kind, actor, returnTo, returnRange);
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public global::ClankerWorld.Simulation.World.GridPoint? TreeOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TreeOrderSite(instruction, person);
        public string TreeOrderSpecies(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.GridPoint site) => runtime.TreeOrderSpecies(order, site);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedTreeOrderSeed(string actor, string kind) => runtime.CarriedTreeOrderSeed(actor, kind);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CollectableTreeOrderSeed(string actor, string kind, global::ClankerWorld.Simulation.World.GridPoint site) => runtime.CollectableTreeOrderSeed(actor, kind, site);
        public global::ClankerWorld.Simulation.World.OrderSystem.TreeOrderPlan? TreePlantingOrderPlan(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TreePlantingOrderPlan(instruction, person);
        public void SetOrderStatus(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.OrderStatus status, string? reason, bool waitForDecision = false) => runtime.SetOrderStatus(instruction, status, reason, waitForDecision);
        public string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => runtime.TreePlantingOrderBlockedReason(instruction, person);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public void MoveToward(string agentId, global::ClankerWorld.Simulation.World.AgentState state, global::ClankerWorld.Simulation.World.GridPoint destination, string reason, int interactionRange = 0, int horseMovementBudget = 100) => runtime.MoveToward(agentId, state, destination, reason, interactionRange, horseMovementBudget);
        public global::ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, global::ClankerWorld.Simulation.World.GridPoint destination) => runtime.PlantTreeCore(planterId, species, seedLotId, destination);
        public void CreditOrderEffect(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.Identity effectId, int units) => runtime.CreditOrderEffect(instruction, effectId, units);
        public string TreePlantingReceipt(string treeId) => WorldRuntime.TreePlantingReceipt(treeId);
    }
}
