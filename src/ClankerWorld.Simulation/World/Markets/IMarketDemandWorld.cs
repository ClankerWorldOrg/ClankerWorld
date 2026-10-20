using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal interface IMarketDemandWorld
{
    AgentStore PhysicalAgents { get; }
    SocietyCheckpoint Society { get; }
    DeclarativeWorldContentState Content { get; }
    IReadOnlyList<PlacedBuilding> Buildings { get; }
    IMarketShopWorld Shops { get; }
    long WorldTick { get; }
    string HouseholdFor(string actor);
    int WorkstationInputTarget(PlacedBuilding building, string kind);
    int WorkstationOnsiteQuantity(InventoryCheckpoint inventory, PlacedBuilding building, string kind);
    int WorkstationIncomingQuantity(InventoryCheckpoint inventory, PlacedBuilding building, string kind);
    int DestinationRoom(InventoryCheckpoint inventory, string building, int outgoingQuantity = 0, int releasedShopReservation = 0);
    bool HasActiveContainerReservation(InventoryCheckpoint inventory, string lot);
    bool UnusableDeliveryStock(InventoryCheckpoint inventory, InventoryLot lot);
    int ContainerFamilyQuantity(InventoryCheckpoint inventory, string container);
    int UsableWorkstationQuantity(InventoryCheckpoint inventory, InventoryLot lot);
    GoodsAnswer FindGoods(GoodsRequest request);
    GoodsRequest DeliveryGoodsRequest(string actor, string owner, string kind, string destination);
    int WorkstationPickupQuantity(string actor, InventoryCheckpoint inventory, InventoryLot stock, string building, int maximum);
    int WorkstationSourceSurplus(InventoryCheckpoint inventory, InventoryLot lot);
    bool MedicalSupplyWanted(string actor, string kind);
    bool WantsFieldPlantingStock(string actor, string kind);
    bool WantsOrnamentInput(string actor, string kind);
    WeatherKind WeatherAt(GridPoint position);
    int AvailableLotQuantity(InventoryLot lot);
    bool WantsTradeItem(string actor, InventoryLot lot);
    PlacedBuilding? HouseholdBuildingWithTag(string household, string tag);
    long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds);
}
