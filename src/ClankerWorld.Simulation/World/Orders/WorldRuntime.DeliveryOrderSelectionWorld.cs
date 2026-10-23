namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IDeliveryOrderSelectionWorld DeliveryOrderSelectionWorld => new DeliveryOrderSelectionAdapter(this);

    private sealed class DeliveryOrderSelectionAdapter(WorldRuntime runtime) : IDeliveryOrderSelectionWorld
    {
        public global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society => runtime.society.Checkpoint;
        public global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content => runtime.worldContent;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order) => WorldRuntime.DeliveryOrderRemaining(order);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.OrderTask order) => runtime.CarriedDeliveryOrderPlan(actor, order);
        public bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedHouseDelivery(string actor) => runtime.CarriedHouseDelivery(actor);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.HouseholdDeliveryOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetBlacksmithOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetWorkstationOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetTownOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetTownOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetStoreOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity) => runtime.GetStoreOrderPlan(actor, person, order, maximumQuantity);
        public global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public global::ClankerWorld.Simulation.World.PlacedBuilding? BlacksmithForHousehold(string householdId) => runtime.HouseholdBuildingWithTag(householdId, "blacksmith");
        public bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building) => runtime.DeliveryDestinationMatches(order, building);
        public (string ItemKind, global::ClankerWorld.Simulation.World.MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId, string actor, string? itemKind = null) => runtime.BlacksmithInputToGather(householdId, blacksmithId, actor, itemKind);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public int ProjectMaterialCarryUnits(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialCarryUnits(actor, itemKind, source, useHarvestBonus);
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public int BlacksmithOreStocked(string householdId, string blacksmithId) => runtime.SmithStoredQuantity(householdId, blacksmithId, "iron_ore");
        public int BlacksmithInputTarget(string blacksmithId, string itemKind) => runtime.BlacksmithInputTarget(blacksmithId, itemKind);
        public global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSmithOre(string actor) => runtime.PersonalSmithOre(actor);
        public bool BlacksmithHasDeliverableInput(string householdId, global::ClankerWorld.Simulation.World.PlacedBuilding blacksmith, string actor, string itemKind) => runtime.BlacksmithHasDeliverableInput(householdId, blacksmith, actor, itemKind);
        public global::ClankerWorld.Simulation.World.MapResource? MaterialSource(string itemKind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, bool useHarvestBonus = true) => runtime.MaterialSource(itemKind, actor, returnTo, returnRange, useHarvestBonus);
        public global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.WorldRuntime.WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null, string? buildingId = null, int maximumResourceQuantity = 2147483647, string? sourceLotId = null) => runtime.WorkstationSupplyNeeds(actor, itemKind, buildingId, maximumResourceQuantity, sourceLotId);
    }
}
