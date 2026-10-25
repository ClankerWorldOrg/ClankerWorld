using System.Collections.ObjectModel;
using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private IMarketDemandWorld MarketDemandWorld => new MarketWorldAdapter(this);
    private IMarketAdministrationWorld MarketAdministrationWorld => new MarketWorldAdapter(this);
    private IMarketStallWorld MarketStallWorld => new MarketWorldAdapter(this);
    private IMarketBarterWorld MarketBarterWorld => new MarketWorldAdapter(this);
    private IMarketShopPlanningWorld MarketShopPlanningWorld => new MarketWorldAdapter(this);
    private IMarketShopWorld MarketShopWorld => new MarketWorldAdapter(this);

    private sealed class MarketWorldAdapter(WorldRuntime runtime) : IMarketShopWorld, IMarketShopPlanningWorld, IMarketBarterWorld, IMarketStallWorld, IMarketAdministrationWorld, IMarketDemandWorld
    {
        public IMarketShopWorld Shops => this;
        public string HouseholdFor(string actor) => runtime.HouseholdFor(actor);
        public int RestaurantInputDemand(string actor, string kind, int spentPersonalQuantity = 0) => runtime.marketSystem.RestaurantInputDemand(runtime.MarketDemandWorld, actor, kind, spentPersonalQuantity);
        public bool ShopBuyerWantsWithoutRestaurant(string actor, InventoryLot lot) => MarketSystem.ShopBuyerWantsWithoutRestaurant(runtime.MarketDemandWorld, actor, lot);
        public IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null, string? building = null, InventoryCheckpoint? inventory = null) => runtime.SpendingGoods(actor, use, owners, kind, building, inventory);
        IEnumerable<GoodsMatch> IMarketShopPlanningWorld.SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, InventoryCheckpoint? inventory, string? building) => runtime.SpendingGoods(actor, use, owners, inventory: inventory, building: building);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool WantsTradeFoodKind(string actor, string kind) => MarketSystem.WantsTradeFoodKind(runtime.MarketBarterWorld, actor, kind);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => runtime.RecheckGoods(request, lot);
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public GridPoint SharedStoragePosition => runtime.SharedStoragePosition;
        public int ReservedShopCarrySpace(string actor) => runtime.marketSystem.ReservedShopCarrySpace(runtime.MarketShopWorld, actor, null);
        public bool MedicalSupplyWanted(string actor, string kind) => FamilySystem.MedicalSupplyWanted(runtime.FamilyMedicalWorld, actor, kind);
        public bool WantsOrnamentInput(string actor, string kind) => runtime.WantsOrnamentInput(actor, kind);
        public bool HasUnknownArtifactContents(string actor, AgentKnowledgeArtifact artifact) => runtime.HasUnknownArtifactContents(actor, artifact);
        public string BuildingConstructionOwner(string actor, BuildingDefinition building) => runtime.BuildingConstructionOwner(actor, building);
        public string PersonalProductionOwner(string actor) => runtime.ProductionOwnerFor(null, actor);
        public bool HasAvailableQuantities(IReadOnlyList<ContentQuantity> quantities, string? owner = null) => runtime.HasAvailableQuantities(quantities, owner);
        public string DirectBarterOfferId(long tick, string actor, string other) => WorldRuntime.DirectBarterOfferId(tick, actor, other);
        public void CreateBarterOffer(DirectBarterProposal proposal) => runtime.society.Apply(checkpoint => SocietyRules.CreateBarterOffer(checkpoint, proposal));
        public void AcceptBarterOffer(string id, int revision, string actor) => runtime.society.Apply(checkpoint => SocietyRules.AcceptBarterOffer(checkpoint, id, revision, actor));
        public void CancelBarterOffer(string id, int revision, string actor) => runtime.RunInventoryCommand(InventorySystem.CancelBarterOffer(id, revision, actor));
        public void RetireBarterOffers(IReadOnlySet<string> retired) => RetireShopOffers(retired);
        public void ReadTradedKnowledge(string first, string second, string firstLot, string secondLot) => runtime.ReadTradedKnowledge(first, second, firstLot, secondLot);
        public void IncreaseTrust(string actor, string other, int amount, string reason) => CommunitySystem.IncreaseTrust(runtime.CommunitySkillWorld, actor, other, amount, reason);
        public string TrustMemoryId(string actor, string other) => WorldRuntime.TrustMemoryId(actor, other);
        public void RecordBarterMemory(string id, string actor, string other) => runtime.society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint,
            new(Identity.Parse(id), actor, other, $"Completed a mutually accepted exchange with {checkpoint.GetAgent(other).Name}.", "public", runtime.WorldTick) { Kind = SocietyMemoryKind.Relationship }));
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsConversationBusy(string actor) => runtime.IsConversationBusy(actor);
        public InventoryLot? CarriedHouseDelivery(string actor) => runtime.CarriedHouseDelivery(actor);
        public bool HasFarmWork(string actor) => runtime.fields.Any(field => field.Work?.WorkerId == actor);
        public int CaregiverFoodCarryRequirement(string actor, AgentState person) => FamilySystem.CaregiverFoodCarryRequirement(runtime.FamilyParenthoodWorld, actor, person);
        public PlacedBuilding? HouseForHousehold(string household) => runtime.HouseForHousehold(household);
        public bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.CanReach(actor, target, purpose, range, load, swimming);
        public bool ShopBuyerWants(string actor, InventoryLot lot) => MarketSystem.ShopBuyerWants(runtime.MarketShopPlanningWorld, actor, lot);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string lot) => WorldRuntime.HasActiveContainerReservation(inventory, lot);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public void ReplaceTownMarket(string town, TownMarketState market) => runtime.townSystem.ReplaceMarketState(runtime.civicWorld, town, market);
        public void UpdateMarketStockHolds() => runtime.RunInventoryCommand(InventorySystem.UpdateMarketStockHolds(runtime.InventoryMarketsWorld));
        public void PickUpMarketStock(string actor, MarketSystem.MarketChoice choice) => runtime.RunInventoryCommand(InventorySystem.PickUpMarketStock(runtime.InventoryMarketsWorld, actor, choice));
        public long DepositMarketLoad(string actor, InventoryLot lot, int quantity, string operation, GridPoint position)
        {
            long eventId = 0;
            runtime.RunInventoryCommand(InventorySystem.DepositMarketLoad(operation, lot, quantity, position, actor, eventId, inventoryResultValue => eventId = inventoryResultValue));
            return eventId;
        }
        public void OpenMarketOffer(string id, string buyer, MarketSystem.MarketChoice choice) => runtime.RunInventoryCommand(InventorySystem.OpenMarketOffer(runtime.InventoryMarketsWorld, id, choice, buyer));
        public long SettleMarketOffer(string actor, MarketTradeState trade, DirectBarterOffer offer, string receipt, string goods, string payment)
        {
            long eventId = 0;
            runtime.RunInventoryCommand(InventorySystem.SettleMarketOffer(offer, trade, goods, payment, actor, receipt, eventId, inventoryResultValue => eventId = inventoryResultValue));
            return eventId;
        }
        public void CancelMarketOffer(DirectBarterOffer offer) => CancelShopOffer(offer);
        public void RetireMarketHistory() => MarketSystem.RetireMarketHistory(runtime.MarketAdministrationWorld);
        void IMarketStallWorld.AppendEvent(WorldEventPayload payload, GridPoint? position) => runtime.AppendEvent(payload, position);
        public IMarketStallWorld Stalls => this;
        public void PlaceStarterStalls(IReadOnlyList<PlacedBuilding> buildings) => runtime.constructionSystem.Buildings.PlaceBuildings(buildings);
        public void RecordConstructedMarkets(TownRuntimeState town, IReadOnlyList<TownMarketState> markets) => runtime.townSystem.RecordMarkets(runtime.civicWorld, town, markets);
        public void RecordRemovedMarketBuildings(TownRuntimeState town, IReadOnlyList<TownMarketState> markets) => runtime.townSystem.RecordMarkets(runtime.civicWorld, town, markets);
        public void AssignBuildingToTown(PlacedBuilding building, BuildingDefinition definition) => runtime.AssignBuildingToTown(building, definition);
        public void MaintainTownProjects() => runtime.MaintainTownProjects();
        public void RetireMarketOffers(IReadOnlySet<string> offers, IReadOnlySet<string> claims) => runtime.RunInventoryCommand(InventorySystem.RetireMarketOffers(offers, claims));
        void IMarketAdministrationWorld.AppendEvent(WorldEventPayload payload, GridPoint? position) => runtime.AppendEvent(payload, position);
        public int WorkstationInputTarget(PlacedBuilding building, string kind) => runtime.WorkstationInputTarget(building, kind);
        public int WorkstationOnsiteQuantity(InventoryCheckpoint inventory, PlacedBuilding building, string kind) => WorldRuntime.WorkstationOnsiteQuantity(inventory, building, kind);
        public int WorkstationIncomingQuantity(InventoryCheckpoint inventory, PlacedBuilding building, string kind) => runtime.WorkstationIncomingQuantity(inventory, building, kind);
        public bool UnusableDeliveryStock(InventoryCheckpoint inventory, InventoryLot lot) => WorldRuntime.UnusableDeliveryStock(inventory, lot);
        public int ContainerFamilyQuantity(InventoryCheckpoint inventory, string container) => WorldRuntime.ContainerFamilyQuantity(inventory, container);
        public int UsableWorkstationQuantity(InventoryCheckpoint inventory, InventoryLot lot) => WorldRuntime.UsableWorkstationQuantity(inventory, lot);
        public GoodsAnswer FindGoods(GoodsRequest request) => runtime.FindGoods(request);
        public GoodsRequest DeliveryGoodsRequest(string actor, string owner, string kind, string destination) => WorldRuntime.DeliveryGoodsRequest(actor, owner, kind, destination);
        public int WorkstationPickupQuantity(string actor, InventoryCheckpoint inventory, InventoryLot stock, string building, int maximum) => runtime.WorkstationPickupQuantity(actor, inventory, stock, building, maximum);
        public int WorkstationSourceSurplus(InventoryCheckpoint inventory, InventoryLot lot) => runtime.WorkstationSourceSurplus(inventory, lot);
        public bool WantsFieldPlantingStock(string actor, string kind) => runtime.farmingSystem.WantsFieldPlantingStock(runtime.farmingWorld, actor, kind);
        public WeatherKind WeatherAt(GridPoint position) => runtime.WeatherAt(position);
        public bool WantsTradeItem(string actor, InventoryLot lot) => MarketSystem.WantsTradeItem(runtime.MarketBarterWorld, actor, lot);
        public PlacedBuilding? HouseholdBuildingWithTag(string household, string tag) => runtime.HouseholdBuildingWithTag(household, tag);
        public long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds) => runtime.HoldingsTotal(owners, kinds);
        public AgentStore PhysicalAgents => runtime.agentStore;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public long WorldTick => runtime.WorldTick;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool MayRespondAtShop(string actor, ShopTradeState trade) => MarketSystem.MayRespondAtShop(runtime.MarketShopPlanningWorld, actor, trade);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => runtime.IsWithinInteractionRange(origin, destination, range);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range) => runtime.MoveToward(actor, person, destination, reason, range);
        public string ShopTradeOfferId(long tick, string actor, string building) => WorldRuntime.ShopTradeOfferId(tick, actor, building);
        public int DestinationRoom(InventoryCheckpoint inventory, string building, int outgoingQuantity = 0, int releasedShopReservation = 0) => runtime.DestinationRoom(inventory, building, outgoingQuantity: outgoingQuantity, releasedShopReservation: releasedShopReservation);
        public int HorseCargoCapacity(string actor) => runtime.HorseCargoCapacity(actor);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.Reach(actor, target, purpose, range, load, swimming);
        public void AcceptShopTrade(ShopTradeState trade, DirectBarterOffer offer) => runtime.RunInventoryCommand(InventorySystem.AcceptShopTrade(offer, trade));
        public void OpenShopOffer(string id, string actor, PlacedBuilding building, MarketSystem.ShopQuote quote) => runtime.RunInventoryCommand(InventorySystem.OpenShopOffer(runtime.InventoryMarketsWorld, id, building, actor, quote));
        public void CancelShopOffer(DirectBarterOffer offer) => runtime.RunInventoryCommand(InventorySystem.CancelShopOffer(offer));
        public void RetireShopOffers(IReadOnlySet<string> retired) => runtime.RunInventoryCommand(InventorySystem.RetireShopOffers(retired));
        public void AppendEvent(WorldEventPayload payload) => runtime.AppendEvent(payload);
    }
}
