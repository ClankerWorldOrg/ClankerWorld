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
        public int RestaurantInputDemand(string actor, string kind, int spentPersonalQuantity = 0) => runtime.RestaurantInputDemand(actor, kind, spentPersonalQuantity);
        public bool ShopBuyerWantsWithoutRestaurant(string actor, InventoryLot lot) => runtime.ShopBuyerWantsWithoutRestaurant(actor, lot);
        public IEnumerable<GoodsMatch> SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, string? kind = null, string? building = null, InventoryCheckpoint? inventory = null) => runtime.SpendingGoods(actor, use, owners, kind, building, inventory);
        IEnumerable<GoodsMatch> IMarketShopPlanningWorld.SpendingGoods(string actor, GoodsUse use, IReadOnlyList<string> owners, InventoryCheckpoint? inventory, string? building) => runtime.SpendingGoods(actor, use, owners, inventory: inventory, building: building);
        public bool NeedsUrgentWarmth(AgentState person) => runtime.NeedsUrgentWarmth(person);
        public bool WantsTradeFoodKind(string actor, string kind) => runtime.WantsTradeFoodKind(actor, kind);
        public ActorCapabilityState? CapabilitiesFor(string actor) => runtime.CapabilitiesFor(actor);
        public int AvailableLotQuantity(InventoryLot lot) => runtime.AvailableLotQuantity(lot);
        public GoodsMatch? RecheckGoods(GoodsRequest request, string lot) => runtime.RecheckGoods(request, lot);
        public WorldKnowledgeState Knowledge => runtime.knowledge;
        public GridPoint SharedStoragePosition => runtime.SharedStoragePosition;
        public int ReservedShopCarrySpace(string actor) => runtime.ReservedShopCarrySpace(actor);
        public bool MedicalSupplyWanted(string actor, string kind) => runtime.MedicalSupplyWanted(actor, kind);
        public bool WantsOrnamentInput(string actor, string kind) => runtime.WantsOrnamentInput(actor, kind);
        public bool HasUnknownArtifactContents(string actor, AgentKnowledgeArtifact artifact) => runtime.HasUnknownArtifactContents(actor, artifact);
        public string BuildingConstructionOwner(string actor, BuildingDefinition building) => runtime.BuildingConstructionOwner(actor, building);
        public string PersonalProductionOwner(string actor) => runtime.ProductionOwnerFor(null, actor);
        public bool HasAvailableQuantities(IReadOnlyList<ContentQuantity> quantities, string? owner = null) => runtime.HasAvailableQuantities(quantities, owner);
        public string DirectBarterOfferId(long tick, string actor, string other) => WorldRuntime.DirectBarterOfferId(tick, actor, other);
        public void CreateBarterOffer(DirectBarterProposal proposal) => runtime.society.Apply(checkpoint => SocietyRules.CreateBarterOffer(checkpoint, proposal));
        public void AcceptBarterOffer(string id, int revision, string actor) => runtime.society.Apply(checkpoint => SocietyRules.AcceptBarterOffer(checkpoint, id, revision, actor));
        public void CancelBarterOffer(string id, int revision, string actor) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory, id, revision, actor));
        public void RetireBarterOffers(IReadOnlySet<string> retired) => RetireShopOffers(retired);
        public void ReadTradedKnowledge(string first, string second, string firstLot, string secondLot) => runtime.ReadTradedKnowledge(first, second, firstLot, secondLot);
        public void IncreaseTrust(string actor, string other, int amount, string reason) => runtime.IncreaseTrust(actor, other, amount, reason);
        public string TrustMemoryId(string actor, string other) => WorldRuntime.TrustMemoryId(actor, other);
        public void RecordBarterMemory(string id, string actor, string other) => runtime.society.Apply(checkpoint => SocietyRules.RecordSocialMemory(checkpoint,
            new(Identity.Parse(id), actor, other, $"Completed a mutually accepted exchange with {checkpoint.GetAgent(other).Name}.", "public", runtime.WorldTick) { Kind = SocietyMemoryKind.Relationship }));
        public IReadOnlyList<TownRuntimeState> Towns => runtime.towns;
        public bool NeedsUrgentFood(AgentState person) => WorldRuntime.NeedsUrgentFood(person);
        public bool IsConversationBusy(string actor) => runtime.IsConversationBusy(actor);
        public InventoryLot? CarriedHouseDelivery(string actor) => runtime.CarriedHouseDelivery(actor);
        public bool HasFarmWork(string actor) => runtime.fields.Any(field => field.Work?.WorkerId == actor);
        public int CaregiverFoodCarryRequirement(string actor, AgentState person) => runtime.CaregiverFoodCarryRequirement(actor, person);
        public PlacedBuilding? HouseForHousehold(string household) => runtime.HouseForHousehold(household);
        public bool CanReach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.CanReach(actor, target, purpose, range, load, swimming);
        public bool ShopBuyerWants(string actor, InventoryLot lot) => runtime.ShopBuyerWants(actor, lot);
        public bool VesselFits(InventoryLot lot, int room) => runtime.VesselFits(lot, room);
        public int FreeCarryCapacity(string actor) => runtime.FreeCarryCapacity(actor);
        public bool HasActiveContainerReservation(InventoryCheckpoint inventory, string lot) => WorldRuntime.HasActiveContainerReservation(inventory, lot);
        public int PhysicalUnreservedQuantity(InventoryLot lot) => runtime.PhysicalUnreservedQuantity(lot);
        public GridPoint HouseholdStockPosition(InventoryLot lot) => runtime.HouseholdStockPosition(lot);
        public int HouseholdStockInteractionRange(InventoryLot lot) => WorldRuntime.HouseholdStockInteractionRange(lot);
        public void ReplaceTownMarket(string town, TownMarketState market) => runtime.townSystem.ReplaceMarketState(runtime.civicWorld, town, market);
        public void UpdateMarketStockHolds() => runtime.ApplyInventoryTransition(inventory => MarketStockHolds.Update(inventory, runtime.towns));
        public void PickUpMarketStock(string actor, MarketSystem.MarketChoice choice) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
            MarketTradeRules.Identity("market-pickup:", actor, choice.Id, runtime.WorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture)), choice.Lot!.Id, choice.Lot.OwnerId, choice.Quantity, carrierId: actor,
            holdAccess: choice.Kind == "collect" && choice.Occupancy is { } loan ? [new(InventoryHoldPurpose.MarketStall, loan.Id), new(InventoryHoldPurpose.Borrowed, actor)] : InventoryHoldRules.BorrowedBy(actor)));
        public long DepositMarketLoad(string actor, InventoryLot lot, int quantity, string operation, GridPoint position)
        {
            long eventId = 0;
            runtime.ApplyInventoryTransition(inventory =>
            {
                var next = InventoryAuthority.Relocate(inventory, operation, lot.Id, lot.OwnerId, quantity, groundPosition: new(position.X, position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor));
                eventId = next.Events[^1].EventId;
                return next;
            });
            return eventId;
        }
        public void OpenMarketOffer(string id, string buyer, MarketSystem.MarketChoice choice) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.AcceptDirectBarterOffer(
            InventoryAuthority.CreateDirectBarterOffer(inventory, new(Identity.Parse(id), 1, choice.Lot!.OwnerId, buyer, choice.Lot.Id, 1, choice.Payment!.Id, 1, runtime.WorldTick + MarketTradeRules.OfferLifetimeTicks), holdAccess: [new(InventoryHoldPurpose.MarketStall, choice.Occupancy!.Id)]), id, 1, buyer));
        public long SettleMarketOffer(string actor, MarketTradeState trade, DirectBarterOffer offer, string receipt, string goods, string payment)
        {
            long eventId = 0;
            runtime.ApplyInventoryTransition(inventory =>
            {
                var settled = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer.Id, offer.Revision, trade.GoodsOwnerId);
                settled = InventoryAuthority.Relocate(settled, IdentityRules.PrefixedReference(offer.Id.Value, ":collect"), goods, trade.BuyerId, offer.FirstQuantity, carrierId: trade.BuyerId, holdAccess: [new(InventoryHoldPurpose.MarketStall, trade.OccupancyId)]);
                if (trade.GoodsOwnerId != trade.PaymentOwnerId) settled = InventoryAuthority.Transfer(settled, IdentityRules.PrefixedReference(offer.Id.Value, ":household-payment"), trade.GoodsOwnerId, trade.PaymentOwnerId, payment, offer.SecondQuantity, "market_household_payment", holdAccess: InventoryHoldRules.BorrowedBy(actor));
                settled = InventoryAuthority.Relocate(settled, TownRecordIds.DepositOperation(receipt), payment, trade.PaymentOwnerId, offer.SecondQuantity, groundPosition: new(trade.Position.X, trade.Position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor));
                eventId = settled.Events[^1].EventId;
                return settled;
            });
            return eventId;
        }
        public void CancelMarketOffer(DirectBarterOffer offer) => CancelShopOffer(offer);
        public void RetireMarketHistory() => runtime.RetireMarketHistory();
        void IMarketStallWorld.AppendEvent(WorldEventPayload payload, GridPoint? position) => runtime.AppendEvent(payload, position);
        public IMarketStallWorld Stalls => this;
        public void PlaceStarterStalls(IReadOnlyList<PlacedBuilding> buildings) => runtime.constructionSystem.Buildings.PlaceBuildings(buildings);
        public void RecordConstructedMarkets(TownRuntimeState town, IReadOnlyList<TownMarketState> markets) => runtime.townSystem.RecordConstructedMarkets(runtime.civicWorld, town, markets);
        public void RecordRemovedMarketBuildings(TownRuntimeState town, IReadOnlyList<TownMarketState> markets) => runtime.townSystem.RecordRemovedMarketBuildings(runtime.civicWorld, town, markets);
        public void AssignBuildingToTown(PlacedBuilding building, BuildingDefinition definition) => runtime.AssignBuildingToTown(building, definition);
        public void MaintainTownProjects() => runtime.MaintainTownProjects();
        public void RetireMarketOffers(IReadOnlySet<string> offers, IReadOnlySet<string> claims) => runtime.ApplyInventoryTransition(inventory => inventory with
        {
            Offers = inventory.Offers.Where(offer => !offers.Contains(offer.Id)).ToArray(),
            Reservations = inventory.Reservations.Where(claim => !claims.Contains(claim.Id)).ToArray(),
        });
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
        public bool WantsFieldPlantingStock(string actor, string kind) => runtime.WantsFieldPlantingStock(actor, kind);
        public WeatherKind WeatherAt(GridPoint position) => runtime.WeatherAt(position);
        public bool WantsTradeItem(string actor, InventoryLot lot) => runtime.WantsTradeItem(actor, lot);
        public PlacedBuilding? HouseholdBuildingWithTag(string household, string tag) => runtime.HouseholdBuildingWithTag(household, tag);
        public long HoldingsTotal(IEnumerable<string> owners, GoodsKinds kinds) => runtime.HoldingsTotal(owners, kinds);
        public AgentStore PhysicalAgents => runtime.agentStore;
        public SocietyCheckpoint Society => runtime.society.Checkpoint;
        public DeclarativeWorldContentState Content => runtime.worldContent;
        public IReadOnlyList<PlacedBuilding> Buildings => runtime.worldSimulation.Buildings;
        public long WorldTick => runtime.WorldTick;
        public int ResourceInteractionRange => WorldRuntime.ResourceInteractionRange;
        public bool IsAdult(string actor) => runtime.IsAdult(actor);
        public bool MayRespondAtShop(string actor, ShopTradeState trade) => runtime.MayRespondAtShop(actor, trade);
        public bool IsWithinInteractionRange(GridPoint origin, GridPoint destination, int range) => runtime.IsWithinInteractionRange(origin, destination, range);
        public void MoveToward(string actor, AgentState person, GridPoint destination, string reason, int range) => runtime.MoveToward(actor, person, destination, reason, range);
        public string ShopTradeOfferId(long tick, string actor, string building) => WorldRuntime.ShopTradeOfferId(tick, actor, building);
        public int DestinationRoom(InventoryCheckpoint inventory, string building, int outgoingQuantity = 0, int releasedShopReservation = 0) => runtime.DestinationRoom(inventory, building, outgoingQuantity: outgoingQuantity, releasedShopReservation: releasedShopReservation);
        public int HorseCargoCapacity(string actor) => runtime.HorseCargoCapacity(actor);
        public ReadOnlyCollection<GridPoint> Reach(string actor, GridPoint target, ReachPurpose purpose, int range, int load, SwimmingPolicy swimming) => runtime.Reach(actor, target, purpose, range, load, swimming);
        public void AcceptShopTrade(ShopTradeState trade, DirectBarterOffer offer) => runtime.ApplyInventoryTransition(inventory =>
        {
            var payment = inventory.GetLot(offer.SecondLotId);
            var paymentId = payment.Quantity == offer.SecondQuantity ? payment.Id : DerivedId.Create($"{payment.Id}#barter:{offer.Id}");
            var settled = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer.Id, offer.Revision, trade.SellerHouseholdId);
            return settled with { Lots = settled.Lots.Select(lot => lot.Id == paymentId ? lot with { StorageBuildingId = trade.BuildingInstanceId } : lot).ToArray() };
        });
        public void OpenShopOffer(string id, string actor, PlacedBuilding building, MarketSystem.ShopQuote quote) => runtime.ApplyInventoryTransition(inventory =>
            InventoryAuthority.AcceptDirectBarterOffer(InventoryAuthority.CreateDirectBarterOffer(inventory,
                new(Identity.Parse(id), 1, building.HouseholdId!, actor, quote.Goods.Id, quote.GoodsQuantity, quote.Payment.Id, quote.PaymentQuantity, runtime.WorldTick + 120)), id, 1, actor));
        public void CancelShopOffer(DirectBarterOffer offer) => runtime.ApplyInventoryTransition(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory, offer.Id, offer.Revision, offer.FirstPartyId));
        public void RetireShopOffers(IReadOnlySet<string> retired) => runtime.ApplyInventoryTransition(inventory => inventory with
        {
            Offers = inventory.Offers.Where(offer => !retired.Contains(offer.Id)).ToArray(),
            Reservations = inventory.Reservations.Where(reservation => !retired.Any(id => reservation.Id == DerivedId.Create(id + ":first") || reservation.Id == DerivedId.Create(id + ":second"))).ToArray(),
        });
        public void AppendEvent(WorldEventPayload payload) => runtime.AppendEvent(payload);
    }
}
