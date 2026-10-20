using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarketShopPlanningWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IMarketShopWorld Shops { get; }
    int ResourceInteractionRange { get; }
    bool IsAdult(string actor);
    string HouseholdFor(string actor);
    int RestaurantInputDemand(string actor, string kind, int spentPersonalQuantity = 0);
    bool ShopBuyerWantsWithoutRestaurant(string actor, InventoryLot lot);
    int HorseCargoCapacity(string actor);
    int DestinationRoom(InventoryCheckpoint inventory, string building, int outgoingQuantity = 0, int releasedShopReservation = 0);
    IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, InventoryCheckpoint? inventory = null, string? building = null);
    bool NeedsUrgentWarmth(AgentState person);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    bool WantsTradeFoodKind(string actor, string kind);
    ActorCapabilityState? CapabilitiesFor(string actor);
    int AvailableLotQuantity(InventoryLot lot);
    GoodsMatch? RecheckGoods(GoodsRequest request, string lot);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
}
