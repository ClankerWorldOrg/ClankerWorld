using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IFamilyChildWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    WorldMap Map { get; }
    IReadOnlyDictionary<string, ResourceState> Resources { get; }
    long WorldTick { get; }
    GridPoint SharedStoragePosition { get; }
    int ResourceInteractionRange { get; }
    bool IsChild(string actor);
    bool IsAdult(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    PlacedBuilding? HouseForHousehold(string household);
    int FreeCarryCapacity(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ReadOnlyCollection<GridPoint> ReachLeg(string actor, GridPoint origin, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    int AvailableLotQuantity(InventoryLot lot);
    bool CanDeliverHouseDelivery(InventoryLot lot);
    GoodsAnswer FindGoods(GoodsRequest request);
    bool OnBorrowedMarketStall(InventoryLot lot);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    int DestinationRoom(string house);
    int FoodHarvestCarryUnits(MapResource resource);
    int FoodHarvestQuantity(MapResource resource);
    InventoryLot? CarriedHouseDelivery(string actor);
    ActorCapabilityState? CapabilitiesFor(string actor);
    InventoryLot? PersonalSpareFood(string actor, string? sourceLotId = null);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    string HouseholdFor(string actor);
    string ChildSocialMemoryId(string kind, string actor, string target, long tick);
    void IncreaseTrust(string actor, string target, int amount, string reason);
    void OfferChildChoice(List<CognitionCandidate> candidates, string actor, ChildChoice choice, string description, int priority, string? destinationId = null);
    void RecordChildSocialMemory(string id, string actor, string target, string kind);
    void DeliverChildLoad(ChildActionPlan plan);
    void CollectChildLoad(ChildActionPlan plan);
    void GatherChildLoad(ChildActionPlan plan);
    void ShareChildFood(ChildActionPlan plan);
    void AppendEvent(string kind, string detail);
}
