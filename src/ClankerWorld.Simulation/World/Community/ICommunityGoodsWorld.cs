using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface ICommunityGoodsWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    string HouseholdFor(string actor);
    GoodsRequest? PersonalRecoveryRequest(string actor, GoodsUse use, string? kind = null);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    PlacedBuilding? HouseForHousehold(string household);
    IEnumerable<InventoryLot> PersonalStorageLots(string actor, string house);
    IEnumerable<InventoryLot> ReturnableBorrowedGoods(string actor);
    IEnumerable<InventoryLot> BorrowedGoods(string actor);
    int DestinationRoom(string house);
    bool VesselFits(InventoryLot lot, int room);
    int PhysicalUnreservedQuantity(InventoryLot lot);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    ActorCapabilityState? CapabilitiesFor(string actor);
    bool ReadyForBriefInteraction(string actor);
    int FreeCarryCapacity(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    void InterruptOrdinaryFieldWorkForCustody(string actor, string lot);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    string PersonalMoveId(string actor, long tick, string lot);
    void MovePersonalGoods(PersonalGoodsActionPlan plan, string moveId);
    void AppendEvent(string kind, string detail);
}
