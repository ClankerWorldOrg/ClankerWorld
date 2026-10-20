using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarketStallWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    IReadOnlyList<TownRuntimeState> Towns { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    ActorCapabilityState? CapabilitiesFor(string actor);
    int AvailableLotQuantity(InventoryLot lot);
    bool IsAdult(string actor);
    bool NeedsUrgentFood(AgentState person);
    bool NeedsUrgentWarmth(AgentState person);
    bool IsConversationBusy(string actor);
    InventoryLot? CarriedHouseDelivery(string actor);
    bool HasFarmWork(string actor);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    string HouseholdFor(string actor);
    IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null, string? building = null, InventoryCheckpoint? inventory = null);
    int CaregiverFoodCarryRequirement(string actor, AgentState person);
    PlacedBuilding? HouseForHousehold(string household);
    bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    bool ShopBuyerWants(string actor, InventoryLot lot);
    int ReservedShopCarrySpace(string actor);
    int HorseCargoCapacity(string actor);
    bool VesselFits(InventoryLot lot, int room);
    int FreeCarryCapacity(string actor);
    bool HasActiveContainerReservation(InventoryCheckpoint inventory, string lot);
    int PhysicalUnreservedQuantity(InventoryLot lot);
    GridPoint HouseholdStockPosition(InventoryLot lot);
    int HouseholdStockInteractionRange(InventoryLot lot);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    void ReplaceTownMarket(string town, TownMarketState market);
    void UpdateMarketStockHolds();
    void PickUpMarketStock(string actor, MarketSystem.MarketChoice choice);
    long DepositMarketLoad(string actor, InventoryLot lot, int quantity, string operation, GridPoint position);
    void OpenMarketOffer(string id, string buyer, MarketSystem.MarketChoice choice);
    long SettleMarketOffer(string actor, MarketTradeState trade, DirectBarterOffer offer, string receipt, string goods, string payment);
    void CancelMarketOffer(DirectBarterOffer offer);
    void RetireMarketHistory();
    void AppendEvent(WorldEventPayload payload, GridPoint? position = null);
}
