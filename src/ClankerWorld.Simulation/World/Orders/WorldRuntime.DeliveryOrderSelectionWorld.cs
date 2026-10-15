using DeliveryOrderPlan = ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan;
using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IDeliveryOrderSelectionWorld DeliveryOrderSelectionWorld => new DeliveryOrderSelectionAdapter(this);

    private sealed class DeliveryOrderSelectionAdapter(WorldRuntime runtime) : IDeliveryOrderSelectionWorld
    {
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool ReadyForBriefInteraction(string actor) => runtime.ReadyForBriefInteraction(actor);
        public int DeliveryOrderRemaining(OrderTask order) => WorldRuntime.DeliveryOrderRemaining(order);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? CarriedDeliveryOrderPlan(string actor, OrderTask order) => runtime.CarriedDeliveryOrderPlan(actor, order);
        public bool CanReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming, ReachObstacles obstacles = 0) => runtime.CanReachLeg(actor, origin, target, purpose, range, load, swimming, obstacles);
        public ClankerWorld.Simulation.Kernel.InventoryLot? CarriedHouseDelivery(string actor) => runtime.CarriedHouseDelivery(actor);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? HouseholdDeliveryOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity) => runtime.HouseholdDeliveryOrderPlan(actor, person, order, maximumQuantity);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetBlacksmithOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity) => runtime.GetBlacksmithOrderPlan(actor, person, order, maximumQuantity);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetWorkstationOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity) => runtime.GetWorkstationOrderPlan(actor, person, order, maximumQuantity);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetTownOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity) => runtime.GetTownOrderPlan(actor, person, order, maximumQuantity);
        public ClankerWorld.Simulation.World.OrderSystem.DeliveryOrderPlan? GetStoreOrderPlan(string actor, AgentState person, OrderTask order, int maximumQuantity) => runtime.GetStoreOrderPlan(actor, person, order, maximumQuantity);
        public ClankerWorld.Simulation.World.GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public ClankerWorld.Simulation.World.PlacedBuilding? BlacksmithForHousehold(string householdId) => runtime.BlacksmithForHousehold(householdId);
        public (string ItemKind, ClankerWorld.Simulation.World.MapResource Source)? BlacksmithInputToGather(string householdId, string blacksmithId, string actor, string? itemKind = null) => runtime.BlacksmithInputToGather(householdId, blacksmithId, actor, itemKind);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public int ProjectMaterialCarryUnits(string actor, string itemKind, MapResource source, bool useHarvestBonus = true) => runtime.ProjectMaterialCarryUnits(actor, itemKind, source, useHarvestBonus);
        public int DestinationRoom(string buildingId, InventoryLot? movingRoot = null) => runtime.DestinationRoom(buildingId, movingRoot);
        public int BlacksmithOreStocked(string householdId, string blacksmithId) => runtime.BlacksmithOreStocked(householdId, blacksmithId);
        public int BlacksmithInputTarget(string blacksmithId, string itemKind) => runtime.BlacksmithInputTarget(blacksmithId, itemKind);
        public ClankerWorld.Simulation.Kernel.InventoryLot? PersonalSmithOre(string actor) => runtime.PersonalSmithOre(actor);
        public bool BlacksmithHasDeliverableInput(string householdId, PlacedBuilding blacksmith, string actor, string itemKind) => runtime.BlacksmithHasDeliverableInput(householdId, blacksmith, actor, itemKind);
        public ClankerWorld.Simulation.World.MapResource? MaterialSource(string itemKind, string actor, GridPoint? returnTo = null, int returnRange = 0, bool useHarvestBonus = true) => runtime.MaterialSource(itemKind, actor, returnTo, returnRange, useHarvestBonus);
        public System.Collections.Generic.IEnumerable<ClankerWorld.Simulation.World.WorldRuntime.WorkstationSupplyNeed> WorkstationSupplyNeeds(string actor, string? itemKind = null, string? buildingId = null, int maximumResourceQuantity = 2147483647, string? sourceLotId = null) => runtime.WorkstationSupplyNeeds(actor, itemKind, buildingId, maximumResourceQuantity, sourceLotId);
    }
}
