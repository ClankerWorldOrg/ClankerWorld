using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IDeliveryOrderSelectionWorld
{
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    int ResourceInteractionRange { get; }
    bool IsAdult(string actor);
    bool ReadyForBriefInteraction(string actor);
    int DeliveryOrderRemaining(OrderTask order);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, OrderTask order);
    bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0);
    ClankerWorld.Simulation.Kernel.InventoryLot? CarriedHouseDelivery(string actor);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetTownOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity);
    ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetStoreOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity);
    ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    string HouseholdFor(string actor);
    ClankerWorld.Simulation.World.PlacedBuilding? BlacksmithForHousehold(string householdId);
    (string ItemKind, ClankerWorld.Simulation.World.MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId, string actor, string? itemKind = null);
    int FreeCarryCapacity(string actor);
    int ProjectMaterialCarryUnits(string actor, string itemKind, MapResource source, bool useHarvestBonus = true);
    int DestinationRoom(string buildingId, InventoryLot? movingRoot = null);
    int BlacksmithOreStocked(string householdId, string blacksmithId);
    int BlacksmithInputTarget(string blacksmithId, string itemKind);
    ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSmithOre(string actor);
    bool BlacksmithHasDeliverableInput(string householdId, PlacedBuilding blacksmith, string actor, string itemKind);
    ClankerWorld.Simulation.World.MapResource? MaterialSource(string itemKind, string actor, GridPoint? returnTo = null, int returnRange = 0, bool useHarvestBonus = true);
    System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.WorldRuntime.WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null, string? buildingId = null, int maximumResourceQuantity = 2147483647, string? sourceLotId = null);
}
