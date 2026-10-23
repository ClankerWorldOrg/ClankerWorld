namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ITreePlantingOrdersWorld TreePlantingOrdersWorld => new TreePlantingOrdersAdapter(this);

    private sealed class TreePlantingOrdersAdapter(WorldRuntime runtime) : GoodsRuntimeAdapter(runtime), ITreePlantingOrdersWorld
    {
        public global::ClankerWorld.Simulation.World.GridPoint? PlantingSite(string actor, global::ClankerWorld.Simulation.World.GridPoint origin) => Runtime.PlantingSite(actor, origin);
        public global::System.Collections.Generic.IReadOnlyList<global::ClankerWorld.Simulation.World.TownRuntimeState> Towns => Runtime.towns;
        public global::ClankerWorld.Simulation.World.TreePlantingRefusal? PlantingSiteRefusal(global::ClankerWorld.Simulation.World.GridPoint point, (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) obstacles) => Runtime.PlantingSiteRefusal(point, obstacles);
        public (global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Buildings, global::System.Collections.Generic.HashSet<global::ClankerWorld.Simulation.World.GridPoint> Occupied) PlantingObstacles() => Runtime.PlantingObstacles();
        public bool TreeAreaFull(global::ClankerWorld.Simulation.World.GridPoint point) => Runtime.TreeAreaFull(point);
        public string PlantingSpecies(global::ClankerWorld.Simulation.World.GridPoint site) => Runtime.PlantingSpecies(site);
        public int PlantingSeedQuantity(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.PlantingSeedQuantity(lot);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.Kernel.InventoryLot> RecoverablePersonalGoods(string actor, string? kind = null) => Runtime.RecoverablePersonalGoods(actor, kind);
        public int PickupCarryCapacity(string actor, global::ClankerWorld.Simulation.Kernel.InventoryLot stock, global::ClankerWorld.Simulation.World.GridPoint destination, int destinationRange = 0) => Runtime.PickupCarryCapacity(actor, stock, destination, destinationRange);
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => Runtime.HouseholdStockPosition(lot);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? SharedItem(string kind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0) => Runtime.SharedItem(kind, actor, returnTo, returnRange);
        public global::ClankerWorld.Simulation.World.GridPoint? TreeOrderSite(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreeOrderSite(Runtime.TreePlantingOrdersWorld, instruction, person);
        public string TreeOrderSpecies(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.GridPoint site) => OrderSystem.TreeOrderSpecies(Runtime.TreePlantingOrdersWorld, order, site);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedTreeOrderSeed(string actor, string kind) => OrderSystem.CarriedTreeOrderSeed(Runtime.TreePlantingOrdersWorld, actor, kind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CollectableTreeOrderSeed(string actor, string kind, global::ClankerWorld.Simulation.World.GridPoint site) => OrderSystem.CollectableTreeOrderSeed(Runtime.TreePlantingOrdersWorld, actor, kind, site);
        public global::ClankerWorld.Simulation.World.OrderSystem.TreeOrderPlan? TreePlantingOrderPlan(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderPlan(Runtime.TreePlantingOrdersWorld, instruction, person);
        public string TreePlantingOrderBlockedReason(global::ClankerWorld.Simulation.World.OwnerQueuedInstruction instruction, global::ClankerWorld.Simulation.World.AgentState person) => OrderSystem.TreePlantingOrderBlockedReason(Runtime.TreePlantingOrdersWorld, instruction, person);
        public global::ClankerWorld.Simulation.World.WorldRuntime.PersonalCollectionEffect? CollectPersonalGoods(string actor, string lotId, int maximumQuantity = 2147483647) => Runtime.CollectPersonalGoods(actor, lotId, maximumQuantity);
        public void CollectEquipment(string actor, global::ClankerWorld.Simulation.World.AgentState person, string kind, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, string? plannedLotId = null) => Runtime.CollectEquipment(actor, person, kind, returnTo, returnRange, plannedLotId);
        public global::ClankerWorld.Simulation.World.TreePlantingResult PlantTreeCore(string planterId, string species, string seedLotId, global::ClankerWorld.Simulation.World.GridPoint destination) => Runtime.PlantTreeCore(planterId, species, seedLotId, destination);
        public string TreePlantingReceipt(string treeId) => WorldRuntime.TreePlantingReceipt(treeId);
    }
}
