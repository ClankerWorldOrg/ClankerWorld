namespace ClankerWorld.Simulation.World;

internal interface IDeliveryOrderSelectionWorld
{
    global::ClankerWorld.Simulation.Society.SocietyCheckpoint Society { get; }
    global::ClankerWorld.Simulation.Content.DeclarativeWorldContentState Content { get; }
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    int DeliveryOrderRemaining(global::ClankerWorld.Simulation.World.OrderTask order);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.OrderTask order);
    bool CanReachLeg(string actor, global::ClankerWorld.Simulation.World.GridPoint origin, global::ClankerWorld.Simulation.World.GridPoint target, global::ClankerWorld.Simulation.World.ReachPurpose purpose, int range, int load, global::ClankerWorld.Simulation.World.SwimmingPolicy swimming, global::ClankerWorld.Simulation.World.ReachObstacles obstacles = (global::ClankerWorld.Simulation.World.ReachObstacles)0);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? CarriedHouseDelivery(string actor);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetTownOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetStoreOrderPlan(string actor, global::ClankerWorld.Simulation.World.AgentState person, global::ClankerWorld.Simulation.World.OrderTask order, int maximumQuantity);
    global::ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    int HouseholdStockInteractionRange(global::ClankerWorld.Simulation.Kernel.InventoryLot lot);
    string HouseholdFor(string actor);
    global::ClankerWorld.Simulation.World.PlacedBuilding? BlacksmithForHousehold(string householdId);
    bool DeliveryDestinationMatches(global::ClankerWorld.Simulation.World.OrderTask order, global::ClankerWorld.Simulation.World.PlacedBuilding building);
    (string ItemKind, global::ClankerWorld.Simulation.World.MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId, string actor, string? itemKind = null);
    int FreeCarryCapacity(string actor);
    int ProjectMaterialCarryUnits(string actor, string itemKind, global::ClankerWorld.Simulation.World.MapResource source, bool useHarvestBonus = true);
    int ResourceInteractionRange { get; }
    int DestinationRoom(string buildingId, global::ClankerWorld.Simulation.Kernel.InventoryLot? movingRoot = null);
    int BlacksmithOreStocked(string householdId, string blacksmithId);
    int BlacksmithInputTarget(string blacksmithId, string itemKind);
    global::ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSmithOre(string actor);
    bool BlacksmithHasDeliverableInput(string householdId, global::ClankerWorld.Simulation.World.PlacedBuilding blacksmith, string actor, string itemKind);
    global::ClankerWorld.Simulation.World.MapResource? MaterialSource(string itemKind, string actor, global::ClankerWorld.Simulation.World.GridPoint? returnTo = null, int returnRange = 0, bool useHarvestBonus = true);
    global::System.Collections.Generic.IEnumerable<global::ClankerWorld.Simulation.World.WorldRuntime.WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null, string? buildingId = null, int maximumResourceQuantity = 2147483647, string? sourceLotId = null);
}
