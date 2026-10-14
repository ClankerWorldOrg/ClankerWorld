using System.Globalization;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private sealed record MarketChoice(string Id, string Kind, TownRuntimeState Town, TownMarketState Market,
        MarketStallState Stall, MarketStallOccupancy? Occupancy = null, InventoryLot? Lot = null,
        int Quantity = 0, MarketTradeState? Trade = null, InventoryLot? Payment = null);

    private const int MarketCandidateLimit = 16;

    private static bool IsMarketCandidate(string id) => id.StartsWith("market_", StringComparison.Ordinal);

    // A settled or cancelled trade is history; only the others still need their offer looked up.
    private static bool IsOpenMarketTrade(MarketTradeState trade) =>
        trade.SettledTick is null && trade.CancellationReason is null;

    /// <summary>The Hall, stall slots and aisles of every standing Market, which other building and planting leave clear.</summary>
    private IEnumerable<GridPoint> MarketSiteTiles() => Ground.MarketSites;

    /// <summary>Household goods stay on sale while one of the household's members borrows the stall they lie on.</summary>
    private bool OnBorrowedMarketStall(InventoryLot lot) =>
        InventoryHoldRules.For(InventoryIndex.For(society.Checkpoint.Inventory), lot)
            .Any(hold => hold.Purpose == InventoryHoldPurpose.MarketStall);

    private bool MarketAdult(string actor) => IsAdult(actor) &&
        society.Checkpoint.GetAgent(actor).Status == SocietyAgentStatus.Active;

    private bool ReadyForMarket(string actor) => MarketAdult(actor) &&
        !NeedsUrgentFood(agents[actor]) && !NeedsUrgentWarmth(agents[actor]) &&
        !IsConversationBusy(actor) && agents[actor].Equipment?.Repair is null &&
        !(agents[actor].Project is { Stage: not ("completed" or "cancelled") } project && !project.RequiresFreshChoice) &&
        CarriedHouseDelivery(actor) is null && !fields.Any(field => field.Work?.WorkerId == actor);

    private void SetMarket(string townId, TownMarketState market)
    {
        var town = towns.Single(item => item.Id == townId);
        SetTown(town with { Markets = town.Markets.Select(item => item.Id == market.Id ? market : item).ToArray() });
        ApplyInventoryTransition(inventory => MarketStockHolds.Update(inventory, towns));
    }

    private bool LiveMarketStall(TownRuntimeState town, TownMarketState market, MarketStallState stall) =>
        market.RemovedTick is null && stall.RemovedTick is null &&
        worldSimulation.Buildings.Any(building => building.InstanceId == market.HallBuildingId &&
            building.TownId == town.Id && building.HouseholdId is null && building.Position == market.Site &&
            building.DefinitionId == MarketContent.Hall2x2().CanonicalId) &&
        worldSimulation.Buildings.Any(building => building.InstanceId == stall.BuildingId &&
            building.TownId == town.Id && building.HouseholdId is null &&
            building.Position == MarketContent.StallSite(market.Site, stall.SlotIndex) &&
            building.DefinitionId == MarketContent.Stall1x1().CanonicalId);

    private static MarketStallOccupancy? MarketOccupant(TownMarketState market, MarketStallState stall) =>
        market.Occupancies.SingleOrDefault(item => item.StallBuildingId == stall.BuildingId && item.EndedTick is null);

    private bool HasMarketOccupancy(string actor) => towns.Any(town => town.Markets.Any(market =>
        market.Occupancies.Any(item => item.SellerAgentId == actor && item.EndedTick is null)));

    private static string MarketChoiceId(string kind, params string[] parts) =>
        MarketTradeRules.Identity("market_" + kind + ":", parts);

    private string MarketPlaceMode(string actor, GridPoint destination, int range) =>
        IsWithinInteractionRange(agents[actor].Position, destination, range) ? "at-site" : "walking";

    private bool CanReachMarketPoint(string actor, GridPoint destination, int range) =>
        IsWithinInteractionRange(agents[actor].Position, destination, range) ||
        Reach(actor, destination, ReachPurpose.MarketExchange, range, 0, SwimmingPolicy.WhenCapable).Count > 0;

    private string MarketTradeMode(string actor, MarketTradeState trade) =>
        MarketPlaceMode(actor, trade.Position, ResourceInteractionRange) == "walking" ? "walking" :
        !IsWithinInteractionRange(agents[trade.BuyerId].Position, trade.Position, ResourceInteractionRange)
            ? "waiting-for-buyer" : "at-site";

    private int MarketStockQuantity(TownMarketState market, MarketStallState stall, InventoryCheckpoint? inventory = null) =>
        MarketTradeRules.StockAt(market, stall.BuildingId, MarketContent.StallSite(market.Site, stall.SlotIndex),
            inventory ?? society.Checkpoint.Inventory).Sum(lot => lot.Quantity +
                InventoryIndex.For(inventory ?? society.Checkpoint.Inventory).ContentsQuantity(lot.Id));

    private int MarketIncomingPayment(TownMarketState market, MarketStallState stall,
        InventoryCheckpoint? inventory = null) => market.Trades
        .Where(trade => IsOpenMarketTrade(trade) && trade.StallBuildingId == stall.BuildingId)
        .Select(trade => (inventory ?? society.Checkpoint.Inventory).GetOffer(trade.OfferId))
        .Where(offer => offer.State == DirectBarterState.Open)
        .Sum(offer => Math.Max(0, offer.SecondQuantity - offer.FirstQuantity));

    private bool OwnMarketGoods(string actor, InventoryLot lot) =>
        lot.OwnerId == actor || society.Checkpoint.GetAgent(actor).HouseholdId is { } household && lot.OwnerId == household;

    private sealed record MarketGoodsBasis(IReadOnlyList<GoodsMatch> Matches,
        IReadOnlyDictionary<string, int> Quantities, IReadOnlyDictionary<(string Owner, string Kind), long> KeptFood);

    private MarketGoodsBasis MarketGoodsFor(string actor)
    {
        var owners = HouseholdFor(actor) is { } household ? new[] { actor, household } : [actor];
        var matches = SpendingGoods(actor, GoodsUse.Sell, owners).ToArray();
        var quantities = matches.ToDictionary(match => match.Lot.Id, match => match.Quantity, StringComparer.Ordinal);
        var kept = matches.Where(match => IsEdibleFood(match.Lot.ItemKind) && MarketReserveStock(actor, match.Lot))
            .GroupBy(match => (match.Lot.OwnerId, match.Lot.ItemKind))
            .ToDictionary(group => group.Key, group => group.Sum(match => (long)match.Quantity));
        return new(matches, quantities, kept);
    }

    private bool MarketReserveStock(string actor, InventoryLot lot) => lot.ContainerLotId is null &&
        (lot.OwnerId == actor ? PersonalEquipmentRules.IsCarried(lot, actor) : lot.CarrierId is null) &&
        !OnMarketStall(lot);

    private int MarketSurplus(string actor, InventoryLot lot, MarketGoodsBasis? goods = null)
    {
        goods ??= MarketGoodsFor(actor);
        var available = goods.Quantities.GetValueOrDefault(lot.Id);
        if (!IsEdibleFood(lot.ItemKind)) return available;
        var reserve = lot.OwnerId == actor
            ? Math.Max(2, CaregiverFoodCarryRequirement(actor, agents[actor]))
            : 2 * society.Checkpoint.GetHousehold(lot.OwnerId).MemberIds.Count(id => agents.ContainsKey(id));
        // Sum each owner's usable food once per synchronous query, across split
        // lots. A bound source limits the load, not which lots cover its reserve.
        var kept = goods.KeptFood.GetValueOrDefault((lot.OwnerId, lot.ItemKind)) -
            (MarketReserveStock(actor, lot) ? available : 0);
        return (int)Math.Max(0L, available - Math.Max(0L, reserve - kept));
    }

    private bool OnMarketStall(InventoryLot lot) => towns.Any(town => town.Markets.Any(market => market.Stalls.Any(stall =>
        MarketTradeRules.IsAt(lot, MarketContent.StallSite(market.Site, stall.SlotIndex)))));

    // Household goods a member carries away need the household's House to come back to.
    private bool CanCarryMarketGoods(string actor, InventoryLot lot) =>
        lot.OwnerId == actor || HouseForHousehold(lot.OwnerId) is not null;

    private bool ProtectedMarketItem(string actor, InventoryLot lot) =>
        AgentKnowledgeRules.IsArtifactKind(lot.ItemKind) ||
        PersonalEquipmentRules.IsSelected(agents[actor].Equipment, lot.Id) ||
        BestUsableToolIds(society.Checkpoint.Inventory, actor).Contains(lot.Id);

    private IEnumerable<InventoryLot> MarketLoadSources(string actor, MarketGoodsBasis goods)
    {
        return goods.Matches.Select(match => match.Lot)
            .Where(lot => MarketTradeRules.IsLoose(lot) &&
            !ProtectedMarketItem(actor, lot) && MarketSurplus(actor, lot, goods) > 0 &&
            (PersonalEquipmentRules.IsCarried(lot, actor) ||
             lot.OwnerId != actor && lot.CarrierId is null &&
             (lot.StorageBuildingId is null || worldSimulation.Buildings.Any(building =>
                 building.InstanceId == lot.StorageBuildingId && building.HouseholdId == lot.OwnerId)) &&
             CanCarryMarketGoods(actor, lot)) &&
            !OnMarketStall(lot))
            .OrderBy(lot => PersonalEquipmentRules.IsCarried(lot, actor) ? 0 : 1)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal);
    }

    private (InventoryLot Goods, InventoryLot Payment)? MarketQuote(string buyer, TownMarketState market,
        MarketStallState stall, MarketStallOccupancy occupancy, MarketGoodsBasis? buyerGoods = null,
        string? selectedGoodsId = null, string? selectedPaymentId = null)
    {
        var inventory = society.Checkpoint.Inventory;
        var position = MarketContent.StallSite(market.Site, stall.SlotIndex);
        if (!MarketAdult(buyer) || buyer == occupancy.SellerAgentId || !ReadyForMarket(buyer) ||
            !MarketAdult(occupancy.SellerAgentId) ||
            society.Checkpoint.GetAgent(occupancy.SellerAgentId).HouseholdId != occupancy.SellerHouseholdId ||
            !MarketTradeRules.IsInside(market, agents[occupancy.SellerAgentId].Position) ||
            !CanReachMarketPoint(buyer, position, ResourceInteractionRange) ||
            inventory.Offers.Any(offer => offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == buyer || offer.SecondPartyId == buyer)) ||
            MarketStockQuantity(market, stall) + MarketIncomingPayment(market, stall) > MarketTradeRules.StallCapacity)
            return null;
        buyerGoods ??= MarketGoodsFor(buyer);
        foreach (var goods in SpendingGoods(occupancy.SellerAgentId, GoodsUse.Sell,
                     [occupancy.SellerAgentId, occupancy.SellerHouseholdId], inventory: inventory).Select(match => match.Lot)
                     .Where(lot => (selectedGoodsId is null || lot.Id == selectedGoodsId) && MarketTradeRules.IsAt(lot, position) && MarketTradeRules.MaySell(market, occupancy, lot, society.Checkpoint, occupancy.SellerAgentId) &&
                         ShopBuyerWants(buyer, lot)))
        {
            foreach (var payment in buyerGoods.Matches.Select(match => match.Lot)
                         .Where(lot => (selectedPaymentId is null || lot.Id == selectedPaymentId) && lot.OwnerId == buyer && PersonalEquipmentRules.IsCarried(lot, buyer) && MarketTradeRules.IsLoose(lot) &&
                         lot.StorageBuildingId is null && lot.GroundPosition is null && lot.ItemKind != goods.ItemKind &&
                         !ProtectedMarketItem(buyer, lot) && !ShopBuyerWants(buyer, lot) && MarketSurplus(buyer, lot, buyerGoods) > 0)
                         .OrderBy(lot => lot.Id, StringComparer.Ordinal))
                // Trial exact barter gives and receives one physical unit, so neither receiving load grows.
                if (PersonalEquipmentRules.CarriedQuantity(inventory, buyer, agents[buyer].Equipment) +
                    ReservedShopCarrySpace(buyer) <= PersonalEquipmentRules.Capacity(inventory, buyer, agents[buyer].Equipment) + HorseCargoCapacity(buyer))
                    return (goods, payment);
        }
        return null;
    }

    private MarketExchangeActionPlan PlanMarketExchange(string actor, MarketChoice choice)
    {
        var kind = choice.Kind == "buy" ? MarketExchangeKind.Buy : choice.Kind == "cancel" ? MarketExchangeKind.Cancel : MarketExchangeKind.Continue;
        var offer = choice.Trade is { } trade ? society.Checkpoint.Inventory.GetOffer(trade.OfferId) : null;
        var mode = kind == MarketExchangeKind.Buy ? MarketPlaceMode(actor, MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex), ResourceInteractionRange) :
            kind == MarketExchangeKind.Cancel ? "" : MarketTradeMode(actor, choice.Trade!);
        var destination = choice.Trade?.Position ?? MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var occupancy = choice.Trade is { } selected ? choice.Market.Occupancies.Single(item => item.Id == selected.OccupancyId) : choice.Occupancy!;
        return new(actor, new(kind, occupancy.Id, actor, choice.Lot?.Id ?? offer!.FirstLotId, choice.Payment?.Id ?? offer!.SecondLotId,
            offer?.Id, mode), choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, choice.Market.Site, choice.Stall.SlotIndex,
            destination, occupancy, choice.Lot?.OwnerId ?? choice.Trade!.GoodsOwnerId, choice.Lot?.ItemKind ?? choice.Trade!.GoodsKind,
            choice.Payment?.ItemKind ?? choice.Trade!.PaymentKind, choice.Trade, offer,
            Reach(actor, destination, ReachPurpose.MarketExchange, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private bool MarketOfferIsCurrent(MarketExchangeActionPlan plan)
    {
        var current = society.Checkpoint.Inventory.GetOffer(plan.Choice.OfferId!);
        return current.State == DirectBarterState.Open && TownHearingProcedure.Digest(current) == TownHearingProcedure.Digest(plan.Offer);
    }

    private bool CanCollectMarketStock(string actor, InventoryLot lot, MarketStallOccupancy? occupancy, InventoryCheckpoint inventory) =>
        (MarketTradeRules.IsLoose(lot) || lot.ItemKind == InventoryContainerRules.WaterJug &&
            VesselFits(lot, FreeCarryCapacity(actor)) && !HasActiveContainerReservation(inventory, lot.Id)) && OwnMarketGoods(actor, lot) &&
        (lot.OwnerId == actor || occupancy is null || occupancy.SellerHouseholdId != lot.OwnerId || occupancy.SellerAgentId == actor) &&
        PhysicalUnreservedQuantity(lot) > 0 && CanCarryMarketGoods(actor, lot);

    private MarketStockActionPlan PlanMarketStock(string actor, MarketChoice choice)
    {
        var kind = choice.Kind == "load" ? MarketStockKind.Load : choice.Kind == "deposit" ? MarketStockKind.Deposit : MarketStockKind.Collect;
        var target = kind == MarketStockKind.Load ? HouseholdStockPosition(choice.Lot!) : MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var range = kind == MarketStockKind.Load ? HouseholdStockInteractionRange(choice.Lot!) : ResourceInteractionRange;
        return new(actor, new(kind, choice.Market.Id, choice.Stall.BuildingId, choice.Lot!.Id, choice.Quantity, choice.Occupancy?.Id,
            IsWithinInteractionRange(agents[actor].Position, target, range)), choice.Town.Id, HouseholdFor(actor), choice.Market.Site,
            choice.Stall.SlotIndex, choice.Lot.OwnerId, choice.Lot.ItemKind, target, range, choice.Lot.CarrierId,
            choice.Lot.StorageBuildingId, choice.Lot.GroundPosition, choice.Occupancy,
            InventoryContainerRules.IsContainer(choice.Lot.ItemKind) ? GoodsPlanFamily.Capture(society.Checkpoint.Inventory, choice.Lot) : null,
            Reach(actor, target, ReachPurpose.MarketStock, range, 0, SwimmingPolicy.WhenCapable));
    }

    private bool MarketStockFamilyMatches(InventoryLot lot, IReadOnlyList<PlannedStockMember>? family) =>
        GoodsPlanFamily.Matches(society.Checkpoint.Inventory, lot, family);

    private InventoryLot? CurrentMarketStock(MarketStockActionPlan plan, TownMarketState market, MarketStallState stall, MarketStallOccupancy? occupancy)
    {
        if (plan.Choice.Kind == MarketStockKind.Collect)
        {
            var stock = MarketTradeRules.StockAt(market, stall.BuildingId, MarketContent.StallSite(market.Site, stall.SlotIndex), society.Checkpoint.Inventory)
                .SingleOrDefault(lot => lot.Id == plan.Choice.StockId);
            return stock is not null && CanCollectMarketStock(plan.Actor, stock, occupancy, society.Checkpoint.Inventory) &&
                Math.Min(MarketTradeRules.LoadQuantity, Math.Min(PhysicalUnreservedQuantity(stock), FreeCarryCapacity(plan.Actor))) >= plan.Choice.Quantity ? stock : null;
        }
        var goods = MarketGoodsFor(plan.Actor);
        var lot = goods.Matches.Select(match => match.Lot).SingleOrDefault(item => item.Id == plan.Choice.StockId);
        if (lot is null) return null;
        if (plan.Choice.Kind == MarketStockKind.Load)
            return MarketLoadSources(plan.Actor, goods).Any(item => item.Id == lot.Id) && !PersonalEquipmentRules.IsCarried(lot, plan.Actor) &&
                Math.Min(MarketTradeRules.LoadQuantity, Math.Min(MarketSurplus(plan.Actor, lot, goods), FreeCarryCapacity(plan.Actor))) >= plan.Choice.Quantity &&
                MarketStockQuantity(market, stall) + MarketIncomingPayment(market, stall) < MarketTradeRules.StallCapacity ? lot : null;
        var room = MarketTradeRules.StallCapacity - MarketStockQuantity(market, stall) - MarketIncomingPayment(market, stall);
        return MarketTradeRules.IsLoose(lot) && PersonalEquipmentRules.IsCarried(lot, plan.Actor) && !ProtectedMarketItem(plan.Actor, lot) &&
            Math.Min(MarketTradeRules.LoadQuantity, Math.Min(room, MarketSurplus(plan.Actor, lot, goods))) >= plan.Choice.Quantity ? lot : null;
    }

    private IEnumerable<MarketChoice> MarketChoices(string actor)
    {
        if (!MarketAdult(actor)) yield break;
        var inventory = society.Checkpoint.Inventory;
        var ready = ReadyForMarket(actor);
        MarketGoodsBasis? goods = null;
        var offeredLoad = false;
        foreach (var town in towns.OrderBy(item => item.Id, StringComparer.Ordinal))
            foreach (var market in town.Markets.OrderBy(item => item.Id, StringComparer.Ordinal))
                foreach (var stall in market.Stalls.OrderBy(item => item.SlotIndex))
                {
                    var position = MarketContent.StallSite(market.Site, stall.SlotIndex);
                    var occupancy = MarketOccupant(market, stall);
                    // Owners retain physical collection even after the building or borrowing ends.
                    foreach (var lot in MarketTradeRules.StockAt(market, stall.BuildingId, position, inventory)
                                 .Where(lot => CanCollectMarketStock(actor, lot, occupancy, inventory)))
                    {
                        var quantity = Math.Min(MarketTradeRules.LoadQuantity, Math.Min(PhysicalUnreservedQuantity(lot), FreeCarryCapacity(actor)));
                        if (quantity > 0 && CanReachMarketPoint(actor, position, ResourceInteractionRange))
                            yield return new(MarketChoiceId("collect", market.Id, stall.BuildingId, lot.Id,
                                quantity.ToString(CultureInfo.InvariantCulture), MarketPlaceMode(actor, position, ResourceInteractionRange)),
                                "collect", town, market, stall, occupancy, lot, quantity);
                    }
                    if (!LiveMarketStall(town, market, stall)) continue;
                    goods ??= MarketGoodsFor(actor);
                    foreach (var trade in market.Trades.Where(item => IsOpenMarketTrade(item) && item.StallBuildingId == stall.BuildingId &&
                                 (item.SellerAgentId == actor || item.BuyerId == actor)).OrderBy(item => item.OfferId, StringComparer.Ordinal))
                    {
                        if (inventory.GetOffer(trade.OfferId).State != DirectBarterState.Open) continue;
                        yield return new(MarketChoiceId("cancel", trade.OfferId), "cancel", town, market, stall, occupancy, Trade: trade);
                        if (!NeedsUrgentWarmth(agents[actor]))
                            yield return new(MarketChoiceId("continue", trade.OfferId, MarketTradeMode(actor, trade)),
                                "continue", town, market, stall, occupancy, Trade: trade);
                    }
                    if (occupancy?.SellerAgentId == actor)
                    {
                        yield return new(MarketChoiceId("leave", occupancy.Id), "leave", town, market, stall, occupancy);
                        if (!ready || society.Checkpoint.GetAgent(actor).HouseholdId != occupancy.SellerHouseholdId ||
                            !CanReachMarketPoint(actor, position, ResourceInteractionRange)) continue;
                        var room = MarketTradeRules.StallCapacity - MarketStockQuantity(market, stall) - MarketIncomingPayment(market, stall);
                        foreach (var lot in goods.Matches.Select(match => match.Lot).Where(lot => MarketTradeRules.IsLoose(lot) &&
                                     PersonalEquipmentRules.IsCarried(lot, actor) && !ProtectedMarketItem(actor, lot) && MarketSurplus(actor, lot, goods) > 0)
                                     .OrderBy(lot => lot.Id, StringComparer.Ordinal))
                        {
                            var quantity = Math.Min(MarketTradeRules.LoadQuantity, Math.Min(room, MarketSurplus(actor, lot, goods)));
                            if (quantity > 0)
                                yield return new(MarketChoiceId("deposit", occupancy.Id, lot.Id,
                                    quantity.ToString(CultureInfo.InvariantCulture), MarketPlaceMode(actor, position, ResourceInteractionRange)),
                                    "deposit", town, market, stall, occupancy, lot, quantity);
                        }
                    }
                    else if (occupancy is not null && MarketQuote(actor, market, stall, occupancy, goods) is { } quote)
                        yield return new(MarketChoiceId("buy", occupancy.Id, actor, quote.Goods.Id, quote.Payment.Id,
                            MarketPlaceMode(actor, position, ResourceInteractionRange)),
                            "buy", town, market, stall, occupancy, quote.Goods, 1, Payment: quote.Payment);
                    if (!ready || occupancy is not null || HasMarketOccupancy(actor) ||
                        society.Checkpoint.GetAgent(actor).HouseholdId is null ||
                        !CanReachMarketPoint(actor, position, ResourceInteractionRange) ||
                        MarketStockQuantity(market, stall) + MarketIncomingPayment(market, stall) >= MarketTradeRules.StallCapacity)
                        continue;
                    var offeredBorrow = false;
                    foreach (var lot in MarketLoadSources(actor, goods))
                    {
                        if (PersonalEquipmentRules.IsCarried(lot, actor))
                        {
                            if (!offeredBorrow)
                            {
                                yield return new(MarketChoiceId("borrow", market.Id, stall.BuildingId, actor,
                                    MarketPlaceMode(actor, position, ResourceInteractionRange)),
                                    "borrow", town, market, stall);
                                offeredBorrow = true;
                            }
                            continue;
                        }
                        // One empty stall is enough to plan a load; the stall itself is borrowed on arrival.
                        if (offeredLoad) continue;
                        var quantity = Math.Min(MarketTradeRules.LoadQuantity, Math.Min(MarketSurplus(actor, lot, goods), FreeCarryCapacity(actor)));
                        if (quantity > 0 && CanReachMarketPoint(actor, HouseholdStockPosition(lot), HouseholdStockInteractionRange(lot)))
                            yield return new(MarketChoiceId("load", market.Id, stall.BuildingId, lot.Id,
                                quantity.ToString(CultureInfo.InvariantCulture), MarketPlaceMode(actor, HouseholdStockPosition(lot),
                                    HouseholdStockInteractionRange(lot))), "load", town, market, stall, Lot: lot, Quantity: quantity);
                    }
                    offeredLoad = true;
                }
    }

    private bool IsMarketFoodCandidate(string actor, string candidateId) =>
        candidateId.StartsWith("market_collect:", StringComparison.Ordinal) && MarketChoices(actor).Any(choice =>
            choice.Id == candidateId && choice.Kind == "collect" && choice.Lot is { } lot && IsEdibleFood(lot.ItemKind));

    private void AddMarketCandidates(List<CognitionCandidate> candidates, string actor)
    {
        var urgentFood = NeedsUrgentFood(agents[actor]);
        // Trade answers and leaving come first, so the limit never hides them behind stock choices.
        var offered = 0;
        foreach (var choice in MarketChoices(actor)
                     .Where(item => !urgentFood || item.Kind == "collect" && IsEdibleFood(item.Lot!.ItemKind) &&
                         AvailableLotQuantity(item.Lot) > 0)
                     .OrderBy(item => item.Kind is "continue" or "cancel" or "leave" ? 0 : 1)
                     )
        {
            ActionPlan? plan = choice.Kind switch
            {
                "borrow" or "leave" => PlanMarketOccupancy(actor, choice),
                "load" or "deposit" or "collect" => PlanMarketStock(actor, choice),
                "buy" or "continue" or "cancel" => PlanMarketExchange(actor, choice),
                _ => null,
            };
            if (choice.Kind is "borrow" or "leave" && plan is null || plan is not null && !CheckActionPlan(plan).Allowed) continue;
            var offer = choice.Trade is { } trade ? society.Checkpoint.Inventory.GetOffer(trade.OfferId) : null;
            var text = choice.Kind switch
            {
                "borrow" => "Walk to the Market and borrow this empty Town-owned stall while you remain at the Market.",
                "load" => $"Collect {choice.Quantity} household-owned {choice.Lot!.ItemKind} to carry to an empty Market stall.",
                "deposit" => $"Place {choice.Quantity} {choice.Lot!.ItemKind} at your borrowed stall; its owner stays the same.",
                "collect" => $"Collect {choice.Quantity} of your {choice.Lot!.ItemKind} from this Market stall.",
                "leave" => "Give up this stall. Goods left here keep their owner and cannot be sold by its next borrower.",
                "buy" => $"Walk to this Market stall and offer 1 {choice.Payment!.ItemKind} for 1 {choice.Lot!.ItemKind}; its named seller may refuse.",
                "cancel" => "Cancel this Market exchange and release its exact goods.",
                _ => actor == choice.Trade!.BuyerId
                    ? $"Wait at the stall for {offer!.FirstQuantity} {choice.Trade.GoodsKind} in exchange for {offer.SecondQuantity} {choice.Trade.PaymentKind}."
                    : MarketTradeMode(actor, choice.Trade) == "waiting-for-buyer"
                        ? $"Wait at your stall for the buyer, who offers {offer!.SecondQuantity} {choice.Trade.PaymentKind} for {offer.FirstQuantity} {choice.Trade.GoodsKind}."
                        : $"Meet the buyer and accept {offer!.SecondQuantity} {choice.Trade.PaymentKind} for {offer.FirstQuantity} {choice.Trade.GoodsKind}; the payment goes to your household.",
            };
            // Above safe idle, like Town project donations: built-in rules cannot act on a Market choice.
            candidates.Add(new(choice.Id, text, choice.Kind switch
            {
                "cancel" or "leave" => 175,
                "collect" => 174,
                "load" or "deposit" or "borrow" => 173,
                "buy" => 172,
                _ => 171,
            }, choice.Stall.BuildingId)
            { ActionPlan = plan });
            if (++offered == MarketCandidateLimit) break;
        }
    }

    private bool ApplyMarketCandidate(string actor, AgentState state, string id, ActionPlan? originalPlan = null)
    {
        if (!IsMarketCandidate(id)) return false;
        if (originalPlan is MarketOccupancyActionPlan occupancyPlan) { ApplyMarketOccupancy(occupancyPlan); return true; }
        if (originalPlan is MarketStockActionPlan stockPlan) { ApplyMarketStock(stockPlan); return true; }
        if (originalPlan is MarketExchangeActionPlan exchangePlan) { ApplyMarketExchange(exchangePlan); return true; }
        var choice = MarketChoices(actor).SingleOrDefault(item => item.Id == id);
        if (choice is null) return true;
        var position = MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        if (choice.Kind is "borrow" or "leave")
        {
            if (PlanMarketOccupancy(actor, choice) is { } plan) ApplyMarketOccupancy(plan);
            return true;
        }
        if (choice.Kind is "load" or "deposit" or "collect") { ApplyMarketStock(PlanMarketStock(actor, choice)); return true; }
        ApplyMarketExchange(PlanMarketExchange(actor, choice));
        return true;
    }

    private void PickUpMarketStock(string actor, MarketChoice choice, GridPoint position)
    {
        ApplyInventoryTransition(inventory => InventoryAuthority.Relocate(inventory,
            MarketTradeRules.Identity("market-pickup:", actor, choice.Id, WorldTick.ToString(CultureInfo.InvariantCulture)),
            choice.Lot!.Id, choice.Lot.OwnerId, choice.Quantity, carrierId: actor,
            holdAccess: choice.Kind == "collect" && choice.Occupancy is { } loan
                ? [new(InventoryHoldPurpose.MarketStall, loan.Id), new(InventoryHoldPurpose.Borrowed, actor)]
                : InventoryHoldRules.BorrowedBy(actor)));
        AppendEvent(choice.Kind == "load" ? "market_stock_loaded" : "market_stock_collected",
            $"{choice.Town.Id}|{choice.Market.Id}|{choice.Stall.BuildingId}|{actor}|{choice.Lot!.Id}", position);
    }

    private void BorrowMarketStall(string actor, MarketChoice choice, GridPoint position)
    {
        // Occupancy is claimed only by physical arrival, not a plan to arrive later.
        var occupancy = new MarketStallOccupancy(MarketTradeRules.Identity("market-occupancy:", choice.Market.Id,
            choice.Stall.BuildingId, actor, WorldTick.ToString(CultureInfo.InvariantCulture),
            choice.Market.Occupancies.Count.ToString(CultureInfo.InvariantCulture)),
            choice.Stall.BuildingId, actor, WorldTick)
        {
            SellerHouseholdId = society.Checkpoint.GetAgent(actor).HouseholdId!,
        };
        SetMarket(choice.Town.Id, choice.Market with { Occupancies = choice.Market.Occupancies.Append(occupancy).ToArray() });
        AppendEvent("market_stall_borrowed", $"{choice.Town.Id}|{choice.Market.Id}|{choice.Stall.BuildingId}|{actor}|{occupancy.Id}", position);
    }

    /// <summary>A continued personal intention may walk, but physical arrival needs a new admitted choice.</summary>
    private void ContinueMarketWalk(string actor, string id)
    {
        var choice = MarketChoices(actor).SingleOrDefault(item => item.Id == id);
        if (choice is null || choice.Kind is "leave" or "cancel") return;
        var destination = choice.Kind == "load" ? HouseholdStockPosition(choice.Lot!) :
            MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var range = choice.Kind == "load" ? HouseholdStockInteractionRange(choice.Lot!) : ResourceInteractionRange;
        if (!IsWithinInteractionRange(agents[actor].Position, destination, range))
            MoveToward(actor, agents[actor], destination, "market_" + choice.Kind, range);
    }

    private void DepositMarketStock(string actor, MarketChoice choice, GridPoint position)
    {
        var lot = choice.Lot!;
        var receiptId = MarketTradeRules.ReceiptId(choice.Occupancy!.Id, lot.Id, lot.OwnerId, choice.Quantity,
            WorldTick, choice.Market.NextStockReceiptSequence);
        var operation = TownRecordIds.DepositOperation(receiptId);
        var movedId = lot.Quantity == choice.Quantity ? lot.Id : DerivedId.Create(lot.Id + "#move:" + operation);
        long eventId = 0;
        ApplyInventoryTransition(inventory =>
        {
            var next = InventoryAuthority.Relocate(inventory, operation, lot.Id, lot.OwnerId, choice.Quantity,
                groundPosition: new(position.X, position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor));
            eventId = next.Events[^1].EventId;
            return next;
        });
        var receipt = new MarketStockReceipt(receiptId, choice.Occupancy.Id, actor, lot.OwnerId,
            lot.Id, movedId, lot.ItemKind, choice.Quantity, WorldTick, eventId)
        {
            Sequence = choice.Market.NextStockReceiptSequence,
        };
        SetMarket(choice.Town.Id, choice.Market with
        {
            StockReceipts = choice.Market.StockReceipts.Append(receipt).ToArray(),
            NextStockReceiptSequence = checked(choice.Market.NextStockReceiptSequence + 1),
        });
        AppendEvent("market_stock_delivered", $"{choice.Town.Id}|{choice.Market.Id}|{choice.Stall.BuildingId}|{actor}|{receipt.Id}", position);
    }

    private void OfferMarketTrade(string buyer, MarketChoice choice, GridPoint position)
    {
        var occupancy = choice.Occupancy!;
        var seller = occupancy.SellerAgentId;
        var owner = occupancy.SellerHouseholdId;
        var id = MarketTradeRules.Identity(MarketTradeRules.OfferPrefix, occupancy.Id, buyer,
            choice.Lot!.Id, choice.Payment!.Id, WorldTick.ToString(CultureInfo.InvariantCulture),
            choice.Market.Trades.Count.ToString(CultureInfo.InvariantCulture));
        ApplyInventoryTransition(inventory => InventoryAuthority.AcceptDirectBarterOffer(
            InventoryAuthority.CreateDirectBarterOffer(inventory, new(id, 1, choice.Lot.OwnerId, buyer,
                choice.Lot.Id, 1, choice.Payment.Id, 1, WorldTick + MarketTradeRules.OfferLifetimeTicks),
                holdAccess: [new(InventoryHoldPurpose.MarketStall, occupancy.Id)]),
            id, 1, buyer));
        var trade = new MarketTradeState(id, occupancy.Id, choice.Stall.BuildingId, seller,
            choice.Lot.OwnerId, owner, buyer, position, WorldTick, choice.Lot.ItemKind, choice.Payment.ItemKind)
        {
            StockReceiptSequence = choice.Market.StockReceipts.Last(receipt => receipt.OccupancyId == occupancy.Id &&
                receipt.OwnerId == choice.Lot.OwnerId && receipt.ItemKind == choice.Lot.ItemKind &&
                MarketTradeRules.IsReceiptLot(receipt, choice.Lot)).Sequence,
        };
        SetMarket(choice.Town.Id, choice.Market with { Trades = choice.Market.Trades.Append(trade).ToArray() });
        AppendEvent("market_trade_offered", $"{choice.Town.Id}|{choice.Market.Id}|{choice.Stall.BuildingId}|{seller}|{buyer}|{id}", position);
    }

    private string? MarketTradeFailure(TownRuntimeState town, TownMarketState market, MarketTradeState trade, DirectBarterOffer offer)
    {
        if (offer.ExpiryTick < WorldTick) return "The offer ran out of time.";
        var stall = market.Stalls.Single(item => item.BuildingId == trade.StallBuildingId);
        var occupancy = market.Occupancies.Single(item => item.Id == trade.OccupancyId);
        if (!LiveMarketStall(town, market, stall) || occupancy.EndedTick is not null ||
            MarketOccupant(market, stall)?.Id != occupancy.Id || !MarketAdult(trade.SellerAgentId) ||
            !MarketTradeRules.IsInside(market, agents[trade.SellerAgentId].Position) ||
            society.Checkpoint.GetAgent(trade.SellerAgentId).HouseholdId != trade.PaymentOwnerId)
            return "The seller no longer has this stall or no longer belongs to the household being paid.";
        if (!MarketAdult(trade.BuyerId)) return "The buyer is no longer available.";
        var inventory = society.Checkpoint.Inventory;
        var goods = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.FirstLotId);
        var payment = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.SecondLotId);
        if (goods is null || payment is null || goods.OwnerId != trade.GoodsOwnerId ||
            !MarketTradeRules.IsAt(goods, trade.Position) ||
            !MarketTradeRules.MaySell(market, occupancy, goods, society.Checkpoint, trade.SellerAgentId) ||
            goods.ConditionBasisPoints <= 0 || goods.FreshnessBasisPoints <= 0 || goods.Quantity < offer.FirstQuantity ||
            payment.OwnerId != trade.BuyerId || !MarketTradeRules.IsLoose(payment) ||
            !PersonalEquipmentRules.IsCarried(payment, trade.BuyerId) || payment.GroundPosition is not null ||
            payment.StorageBuildingId is not null || payment.ConditionBasisPoints <= 0 || payment.FreshnessBasisPoints <= 0 ||
            payment.Quantity < offer.SecondQuantity || ProtectedMarketItem(trade.BuyerId, payment) ||
            !MarketTradeValidation.HasExactOpenClaim(inventory, offer, true) ||
            !MarketTradeValidation.HasExactOpenClaim(inventory, offer, false))
            return "The goods set aside for this trade are no longer usable at the agreed place.";
        if (MarketStockQuantity(market, stall) + MarketIncomingPayment(market, stall) > MarketTradeRules.StallCapacity ||
            PersonalEquipmentRules.CarriedQuantity(inventory, trade.BuyerId, agents[trade.BuyerId].Equipment) +
            ReservedShopCarrySpace(trade.BuyerId) > PersonalEquipmentRules.Capacity(inventory, trade.BuyerId, agents[trade.BuyerId].Equipment) + HorseCargoCapacity(trade.BuyerId))
            return "There is no longer enough room to receive the goods.";
        if (!IsWithinInteractionRange(agents[trade.BuyerId].Position, trade.Position, ResourceInteractionRange) &&
            Reach(trade.BuyerId, trade.Position, ReachPurpose.MarketExchange, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return "The buyer can no longer reach the stall.";
        return null;
    }

    private void ContinueMarketTrade(string actor, AgentState state, MarketChoice choice, bool cancel, DirectBarterOffer? originalOffer = null)
    {
        var trade = choice.Trade!;
        var offer = originalOffer ?? society.Checkpoint.Inventory.GetOffer(trade.OfferId);
        if (cancel || MarketTradeFailure(choice.Town, choice.Market, trade, offer) is { })
        {
            CancelMarketTrade(choice.Town.Id, choice.Market, trade, cancel
                ? "One of the traders cancelled the exchange." : MarketTradeFailure(choice.Town, choice.Market, trade, offer)!);
            return;
        }
        if (!IsWithinInteractionRange(state.Position, trade.Position, ResourceInteractionRange))
        {
            MoveToward(actor, state, trade.Position, "market_trade", ResourceInteractionRange);
            return;
        }
        if (actor != trade.SellerAgentId || !IsWithinInteractionRange(agents[trade.BuyerId].Position,
                trade.Position, ResourceInteractionRange)) return;
        // The named seller's own admitted choice accepts for personal goods or their current household.
        var goods = society.Checkpoint.Inventory.GetLot(offer.FirstLotId);
        var payment = society.Checkpoint.Inventory.GetLot(offer.SecondLotId);
        var goodsId = goods.Quantity == offer.FirstQuantity ? goods.Id : DerivedId.Create(goods.Id + "#barter:" + offer.Id);
        var paymentId = payment.Quantity == offer.SecondQuantity ? payment.Id : DerivedId.Create(payment.Id + "#barter:" + offer.Id);
        var receiptId = MarketTradeRules.ReceiptId(trade.OccupancyId, paymentId, trade.PaymentOwnerId,
            offer.SecondQuantity, WorldTick, choice.Market.NextStockReceiptSequence, offer.Id);
        long eventId = 0;
        ApplyInventoryTransition(inventory =>
        {
            var settled = InventoryAuthority.AcceptDirectBarterOffer(inventory, offer.Id, offer.Revision, trade.GoodsOwnerId);
            settled = InventoryAuthority.Relocate(settled, offer.Id + ":collect", goodsId, trade.BuyerId,
                offer.FirstQuantity, carrierId: trade.BuyerId,
                holdAccess: [new(InventoryHoldPurpose.MarketStall, trade.OccupancyId)]);
            if (trade.GoodsOwnerId != trade.PaymentOwnerId)
                settled = InventoryAuthority.Transfer(settled, offer.Id + ":household-payment", trade.GoodsOwnerId,
                    trade.PaymentOwnerId, paymentId, offer.SecondQuantity, "market_household_payment", holdAccess: InventoryHoldRules.BorrowedBy(actor));
            settled = InventoryAuthority.Relocate(settled, TownRecordIds.DepositOperation(receiptId), paymentId, trade.PaymentOwnerId,
                offer.SecondQuantity, groundPosition: new(trade.Position.X, trade.Position.Y), holdAccess: InventoryHoldRules.BorrowedBy(actor));
            eventId = settled.Events[^1].EventId;
            return settled;
        });
        var receipt = new MarketStockReceipt(receiptId, trade.OccupancyId, actor, trade.PaymentOwnerId,
            paymentId, paymentId, trade.PaymentKind, offer.SecondQuantity, WorldTick, eventId, offer.Id)
        {
            Sequence = choice.Market.NextStockReceiptSequence,
        };
        SetMarket(choice.Town.Id, choice.Market with
        {
            Trades = choice.Market.Trades.Select(item => item.OfferId == trade.OfferId ? trade with { SettledTick = WorldTick } : item).ToArray(),
            StockReceipts = choice.Market.StockReceipts.Append(receipt).ToArray(),
            NextStockReceiptSequence = checked(choice.Market.NextStockReceiptSequence + 1),
        });
        AppendEvent("market_trade_completed", $"{choice.Town.Id}|{choice.Market.Id}|{choice.Stall.BuildingId}|{actor}|{trade.BuyerId}|{trade.OfferId}", trade.Position);
    }

    private void CancelMarketTrade(string townId, TownMarketState market, MarketTradeState trade, string reason)
    {
        var offer = society.Checkpoint.Inventory.GetOffer(trade.OfferId);
        if (offer.State == DirectBarterState.Open)
            ApplyInventoryTransition(inventory => InventoryAuthority.CancelDirectBarterOffer(inventory,
                offer.Id, offer.Revision, offer.FirstPartyId));
        SetMarket(townId, market with
        {
            Trades = market.Trades.Select(item => item.OfferId == trade.OfferId
            ? trade with { CancellationReason = reason } : item).ToArray()
        });
        AppendEvent("market_trade_cancelled", $"{townId}|{market.Id}|{trade.StallBuildingId}|{trade.SellerAgentId}|{trade.BuyerId}|{trade.OfferId}", trade.Position);
    }

    private void EndMarketOccupancy(string townId, TownMarketState market, MarketStallOccupancy occupancy, string reason)
    {
        foreach (var trade in market.Trades.Where(item => IsOpenMarketTrade(item) && item.OccupancyId == occupancy.Id &&
                     society.Checkpoint.Inventory.GetOffer(item.OfferId).State == DirectBarterState.Open).ToArray())
        {
            CancelMarketTrade(townId, market, trade, reason);
            market = towns.Single(town => town.Id == townId).Markets.Single(item => item.Id == market.Id);
        }
        market = market with
        {
            Occupancies = market.Occupancies.Select(item => item.Id == occupancy.Id
            ? occupancy with { EndedTick = WorldTick, ReleaseReason = reason } : item).ToArray()
        };
        SetMarket(townId, market);
        var stall = market.Stalls.Single(item => item.BuildingId == occupancy.StallBuildingId);
        AppendEvent("market_stall_left", $"{townId}|{market.Id}|{stall.BuildingId}|{occupancy.SellerAgentId}|{occupancy.Id}",
            MarketContent.StallSite(market.Site, stall.SlotIndex));
    }

    private void MaintainMarkets()
    {
        foreach (var originalTown in towns.ToArray())
            foreach (var originalMarket in originalTown.Markets.ToArray())
            {
                var market = originalMarket;
                foreach (var occupancy in market.Occupancies.Where(item => item.EndedTick is null).ToArray())
                {
                    var stall = market.Stalls.Single(item => item.BuildingId == occupancy.StallBuildingId);
                    var reason = !LiveMarketStall(originalTown, market, stall) ? "The Market stall is no longer available." :
                        !MarketAdult(occupancy.SellerAgentId) ? "The seller is no longer available." :
                        society.Checkpoint.GetAgent(occupancy.SellerAgentId).HouseholdId != occupancy.SellerHouseholdId
                            ? "The seller's household changed." :
                        !MarketTradeRules.IsInside(market, agents[occupancy.SellerAgentId].Position) ? "The seller left the Market." : null;
                    if (reason is null) continue;
                    EndMarketOccupancy(originalTown.Id, market, occupancy, reason);
                    market = towns.Single(town => town.Id == originalTown.Id).Markets.Single(item => item.Id == market.Id);
                }
                foreach (var trade in market.Trades.Where(IsOpenMarketTrade).ToArray())
                {
                    var offer = society.Checkpoint.Inventory.GetOffer(trade.OfferId);
                    // The inventory closes an offer at its deadline before this check sees it.
                    var failure = offer.State == DirectBarterState.Open ? MarketTradeFailure(originalTown, market, trade, offer) :
                        offer.State != DirectBarterState.Cancelled ? null :
                        offer.ExpiryTick < WorldTick ? "The offer ran out of time." : "The offer was cancelled.";
                    if (failure is null) continue;
                    CancelMarketTrade(originalTown.Id, market, trade, failure);
                    market = towns.Single(town => town.Id == originalTown.Id).Markets.Single(item => item.Id == market.Id);
                }
                // Removal ends borrowing but retains the stock history and owners' physical collection.
                var built = worldSimulation.Buildings.Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
                if ((market.RemovedTick is not null || built.Contains(market.HallBuildingId)) &&
                    market.Stalls.All(stall => stall.RemovedTick is not null || built.Contains(stall.BuildingId)))
                    continue;
                SetMarket(originalTown.Id, market with
                {
                    RemovedTick = built.Contains(market.HallBuildingId) ? market.RemovedTick : market.RemovedTick ?? WorldTick,
                    Stalls = market.Stalls.Select(stall => built.Contains(stall.BuildingId)
                        ? stall : stall with { RemovedTick = stall.RemovedTick ?? WorldTick }).ToArray(),
                });
            }
        RetireMarketHistory();
    }
}
