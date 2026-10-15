using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarketShopWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    long WorldTick { get; }
    int ResourceInteractionRange { get; }
    bool IsAdult(string actor);
    bool MayRespondAtShop(string actor, ShopTradeState trade);
    bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range);
    void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range);
    string ShopTradeOfferId(long tick, string actor, string building);
    int DestinationRoom(InventoryCheckpoint inventory, string building, int outgoingQuantity = 0, int releasedShopReservation = 0);
    int HorseCargoCapacity(string actor);
    ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming);
    void AcceptShopTrade(ShopTradeState trade, DirectBarterOffer offer);
    void OpenShopOffer(string id, string actor, PlacedBuilding building, MarketSystem.ShopQuote quote);
    void CancelShopOffer(DirectBarterOffer offer);
    void RetireShopOffers(IReadOnlySet<string> retired);
    void AppendEvent(string kind, string detail);
}
