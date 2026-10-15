using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyPracticalCareWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    IFamilyCareWorld CareWorld { get; }
    IFamilyParenthoodWorld ParenthoodWorld { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    FoodActionPlan? PlanFoodConsumption(string actor);
    InventoryLot? AvailableHouseholdServing(string actor, GridPoint position, GridPoint returnTo, int returnRange);
    SharedFoodActionPlan? PlanSharedFood(string actor, InventoryLot selected);
    MapResource? AvailableFoodSource(string actor, GridPoint position);
    FoodResourceActionPlan? PlanFoodResource(string actor, bool harvest, MapResource selected);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool NeedsUrgentFood(AgentState person);
    IEnumerable<InventoryLot> PreferredFood(string owner, string actor);
    ActionCheck CheckFoodPlan(FoodActionPlan plan);
    void CompleteChildFoodPickup(string actor, SharedFoodActionPlan plan, GridPoint destination);
    void CompleteFoodHarvest(string actor, MapResource resource, string kind, int quantity, int seedQuantity);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
}
