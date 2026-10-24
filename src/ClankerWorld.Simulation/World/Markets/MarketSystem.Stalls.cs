using System.Globalization;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal sealed record MarketChoice(string Id, string Kind, TownRuntimeState Town, TownMarketState Market,
        MarketStallState Stall, MarketStallOccupancy? Occupancy = null, InventoryLot? Lot = null,
        int Quantity = 0, MarketTradeState? Trade = null, InventoryLot? Payment = null);
    internal sealed record MarketGoodsBasis(IReadOnlyList<GoodsMatch> Matches,
        IReadOnlyDictionary<string, int> Quantities, IReadOnlyDictionary<(string Owner, string Kind), long> KeptFood);

    internal static bool IsOpenMarketTrade(MarketTradeState trade) =>
        trade.SettledTick is null && trade.CancellationReason is null;

    internal static MarketStallOccupancy? MarketOccupant(TownMarketState market, MarketStallState stall) =>
        market.Occupancies.SingleOrDefault(item => item.StallBuildingId == stall.BuildingId && item.EndedTick is null);

    internal static string MarketChoiceId(string kind, params string[] parts) =>
        MarketTradeRules.Identity("market_" + kind + ":", parts);

    internal static bool MarketAdult(IMarketStallWorld world, string actor) => world.IsAdult(actor) &&
        world.Society.GetAgent(actor).Status == SocietyAgentStatus.Active;

    internal static bool ReadyForMarket(IMarketStallWorld world, string actor) => MarketAdult(world, actor) &&
        !world.NeedsUrgentFood(world.PhysicalAgents.Living[actor]) && !world.NeedsUrgentWarmth(world.PhysicalAgents.Living[actor]) &&
        !world.IsConversationBusy(actor) && world.PhysicalAgents.Living[actor].Equipment?.Repair is null &&
        !(world.PhysicalAgents.Living[actor].Project is { Stage: not ("completed" or "cancelled") } project && !project.RequiresFreshChoice) &&
        world.CarriedHouseDelivery(actor) is null && !world.HasFarmWork(actor);

    internal static void SetMarket(IMarketStallWorld world, string townId, TownMarketState market)
    {
        if (SystemInvariantChecks.Enabled) ValidateLocalMarket(market);
        world.ReplaceTownMarket(townId, market);
        world.UpdateMarketStockHolds();
    }

    internal static bool LiveMarketStall(IMarketStallWorld world, TownRuntimeState town, TownMarketState market, MarketStallState stall) =>
        market.RemovedTick is null && stall.RemovedTick is null &&
        world.Buildings.Any(building => building.InstanceId == market.HallBuildingId &&
            building.TownId == town.Id && building.HouseholdId is null && building.Position == market.Site &&
            building.DefinitionId == MarketContent.Hall2x2().CanonicalId) &&
        world.Buildings.Any(building => building.InstanceId == stall.BuildingId &&
            building.TownId == town.Id && building.HouseholdId is null &&
            building.Position == MarketContent.StallSite(market.Site, stall.SlotIndex) &&
            building.DefinitionId == MarketContent.Stall1x1().CanonicalId);

    internal static bool HasMarketOccupancy(IMarketStallWorld world, string actor) => world.Towns.Any(town => town.Markets.Any(market =>
        market.Occupancies.Any(item => item.SellerAgentId == actor && item.EndedTick is null)));

    internal static string MarketPlaceMode(IMarketStallWorld world, string actor, GridPoint destination, int range) =>
        world.IsWithinInteractionRange(world.PhysicalAgents.Living[actor].Position, destination, range) ? "at-site" : "walking";

    internal static string MarketTradeMode(IMarketStallWorld world, string actor, MarketTradeState trade) =>
        MarketPlaceMode(world, actor, trade.Position, world.ResourceInteractionRange) == "walking" ? "walking" :
        !world.IsWithinInteractionRange(world.PhysicalAgents.Living[trade.BuyerId].Position, trade.Position, world.ResourceInteractionRange)
            ? "waiting-for-buyer" : "at-site";

    internal static int MarketStockQuantity(IMarketStallWorld world, TownMarketState market, MarketStallState stall, InventoryCheckpoint? inventory = null) =>
        MarketTradeRules.StockAt(market, stall.BuildingId, MarketContent.StallSite(market.Site, stall.SlotIndex),
            inventory ?? world.Society.Inventory).Sum(lot => lot.Quantity +
                InventoryIndex.For(inventory ?? world.Society.Inventory).ContentsQuantity(lot.Id));

    internal static int MarketIncomingPayment(IMarketStallWorld world, TownMarketState market, MarketStallState stall,
        InventoryCheckpoint? inventory = null) => market.Trades
        .Where(trade => IsOpenMarketTrade(trade) && trade.StallBuildingId == stall.BuildingId)
        .Select(trade => (inventory ?? world.Society.Inventory).GetOffer(trade.OfferId))
        .Where(offer => offer.State == DirectBarterState.Open)
        .Sum(offer => Math.Max(0, offer.SecondQuantity - offer.FirstQuantity));

    internal static bool OwnMarketGoods(IMarketStallWorld world, string actor, InventoryLot lot) =>
        lot.OwnerId == actor || world.Society.GetAgent(actor).HouseholdId is { } household && lot.OwnerId == household;

    internal static MarketGoodsBasis MarketGoodsFor(IMarketStallWorld world, string actor)
    {
        var owners = world.HouseholdFor(actor) is { } household ? new[] { actor, household } : [actor];
        var matches = world.SpendingGoods(actor, GoodsUse.Sell, owners).ToArray();
        var quantities = matches.ToDictionary(match => match.Lot.Id, match => match.Quantity, StringComparer.Ordinal);
        var kept = matches.Where(match => FoodRules.IsEdible(match.Lot.ItemKind) && MarketReserveStock(world, actor, match.Lot))
            .GroupBy(match => (match.Lot.OwnerId, match.Lot.ItemKind))
            .ToDictionary(group => group.Key, group => group.Sum(match => (long)match.Quantity));
        return new(matches, quantities, kept);
    }

    internal static bool MarketReserveStock(IMarketStallWorld world, string actor, InventoryLot lot) => lot.ContainerLotId is null &&
        (lot.OwnerId == actor ? PersonalEquipmentRules.IsCarried(lot, actor) : lot.CarrierId is null) &&
        !OnMarketStall(world, lot);

    internal static int MarketSurplus(IMarketStallWorld world, string actor, InventoryLot lot, MarketGoodsBasis? goods = null)
    {
        goods ??= MarketGoodsFor(world, actor);
        var available = goods.Quantities.GetValueOrDefault(lot.Id);
        if (!FoodRules.IsEdible(lot.ItemKind)) return available;
        var reserve = lot.OwnerId == actor
            ? Math.Max(2, world.CaregiverFoodCarryRequirement(actor, world.PhysicalAgents.Living[actor]))
            : 2 * world.Society.GetHousehold(lot.OwnerId).MemberIds.Count(id => world.PhysicalAgents.Living.ContainsKey(id));
        // Sum each owner's usable food once per synchronous query, across split
        // lots. A bound source limits the load, not which lots cover its reserve.
        var kept = goods.KeptFood.GetValueOrDefault((lot.OwnerId, lot.ItemKind)) -
            (MarketReserveStock(world, actor, lot) ? available : 0);
        return (int)Math.Max(0L, available - Math.Max(0L, reserve - kept));
    }

    internal static bool OnMarketStall(IMarketStallWorld world, InventoryLot lot) => world.Towns.Any(town => town.Markets.Any(market => market.Stalls.Any(stall =>
        MarketTradeRules.IsAt(lot, MarketContent.StallSite(market.Site, stall.SlotIndex)))));

    internal static bool CanCarryMarketGoods(IMarketStallWorld world, string actor, InventoryLot lot) =>
        lot.OwnerId == actor || world.HouseForHousehold(lot.OwnerId) is not null;

    internal static bool ProtectedMarketItem(IMarketStallWorld world, string actor, InventoryLot lot) =>
        AgentKnowledgeRules.IsArtifactKind(lot.ItemKind) ||
        PersonalEquipmentRules.IsSelected(world.PhysicalAgents.Living[actor].Equipment, lot.Id) ||
        BestUsableToolIds(world.Society.Inventory, actor).Contains(lot.Id);

    internal static IEnumerable<InventoryLot> MarketLoadSources(IMarketStallWorld world, string actor, MarketGoodsBasis goods)
    {
        return goods.Matches.Select(match => match.Lot)
            .Where(lot => MarketTradeRules.IsLoose(lot) &&
            !ProtectedMarketItem(world, actor, lot) && MarketSurplus(world, actor, lot, goods) > 0 &&
            (PersonalEquipmentRules.IsCarried(lot, actor) ||
             lot.OwnerId != actor && lot.CarrierId is null &&
             (lot.StorageBuildingId is null || world.Buildings.Any(building =>
                 building.InstanceId == lot.StorageBuildingId && building.HouseholdId == lot.OwnerId)) &&
             CanCarryMarketGoods(world, actor, lot)) &&
            !OnMarketStall(world, lot))
            .OrderBy(lot => PersonalEquipmentRules.IsCarried(lot, actor) ? 0 : 1)
            .ThenBy(lot => lot.Id, StringComparer.Ordinal);
    }

    internal static (InventoryLot Goods, InventoryLot Payment)? MarketQuote(IMarketStallWorld world, string buyer, TownMarketState market,
        MarketStallState stall, MarketStallOccupancy occupancy, MarketGoodsBasis? buyerGoods = null,
        string? selectedGoodsId = null, string? selectedPaymentId = null)
    {
        var inventory = world.Society.Inventory;
        var position = MarketContent.StallSite(market.Site, stall.SlotIndex);
        if (!MarketAdult(world, buyer) || buyer == occupancy.SellerAgentId || !ReadyForMarket(world, buyer) ||
            !MarketAdult(world, occupancy.SellerAgentId) ||
            world.Society.GetAgent(occupancy.SellerAgentId).HouseholdId != occupancy.SellerHouseholdId ||
            !MarketTradeRules.IsInside(market, world.PhysicalAgents.Living[occupancy.SellerAgentId].Position) ||
            !world.CanReach(buyer, position, ReachPurpose.MarketExchange, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) ||
            inventory.Offers.Any(offer => offer.State == DirectBarterState.Open &&
                (offer.FirstPartyId == buyer || offer.SecondPartyId == buyer)) ||
            MarketStockQuantity(world, market, stall) + MarketIncomingPayment(world, market, stall) > MarketTradeRules.StallCapacity)
            return null;
        buyerGoods ??= MarketGoodsFor(world, buyer);
        foreach (var goods in world.SpendingGoods(occupancy.SellerAgentId, GoodsUse.Sell,
                     [occupancy.SellerAgentId, occupancy.SellerHouseholdId], inventory: inventory).Select(match => match.Lot)
                     .Where(lot => (selectedGoodsId is null || lot.Id == selectedGoodsId) && MarketTradeRules.IsAt(lot, position) && MarketTradeRules.MaySell(market, occupancy, lot, world.Society, occupancy.SellerAgentId) &&
                         world.ShopBuyerWants(buyer, lot)))
        {
            foreach (var payment in buyerGoods.Matches.Select(match => match.Lot)
                         .Where(lot => (selectedPaymentId is null || lot.Id == selectedPaymentId) && lot.OwnerId == buyer && PersonalEquipmentRules.IsCarried(lot, buyer) && MarketTradeRules.IsLoose(lot) &&
                         lot.StorageBuildingId is null && lot.GroundPosition is null && lot.ItemKind != goods.ItemKind &&
                         !ProtectedMarketItem(world, buyer, lot) && !world.ShopBuyerWants(buyer, lot) && MarketSurplus(world, buyer, lot, buyerGoods) > 0)
                         .OrderBy(lot => lot.Id, StringComparer.Ordinal))
                // Trial exact barter gives and receives one physical unit, so neither receiving load grows.
                if (PersonalEquipmentRules.CarriedQuantity(inventory, buyer, world.PhysicalAgents.Living[buyer].Equipment) +
                    world.ReservedShopCarrySpace(buyer) <= PersonalEquipmentRules.Capacity(inventory, buyer, world.PhysicalAgents.Living[buyer].Equipment) + world.HorseCargoCapacity(buyer))
                    return (goods, payment);
        }
        return null;
    }

    internal static MarketExchangeActionPlan PlanMarketExchange(IMarketStallWorld world, string actor, MarketChoice choice)
    {
        var kind = choice.Kind == "buy" ? MarketExchangeKind.Buy : choice.Kind == "cancel" ? MarketExchangeKind.Cancel : MarketExchangeKind.Continue;
        var offer = choice.Trade is { } trade ? world.Society.Inventory.GetOffer(trade.OfferId) : null;
        var mode = kind == MarketExchangeKind.Buy ? MarketPlaceMode(world, actor, MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex), world.ResourceInteractionRange) :
            kind == MarketExchangeKind.Cancel ? "" : MarketTradeMode(world, actor, choice.Trade!);
        var destination = choice.Trade?.Position ?? MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var occupancy = choice.Trade is { } selected ? choice.Market.Occupancies.Single(item => item.Id == selected.OccupancyId) : choice.Occupancy!;
        return new(actor, new(kind, occupancy.Id, actor, choice.Lot?.Id ?? offer!.FirstLotId, choice.Payment?.Id ?? offer!.SecondLotId,
            offer?.Id, mode), choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, choice.Market.Site, choice.Stall.SlotIndex,
            destination, occupancy, choice.Lot?.OwnerId ?? choice.Trade!.GoodsOwnerId, choice.Lot?.ItemKind ?? choice.Trade!.GoodsKind,
            choice.Payment?.ItemKind ?? choice.Trade!.PaymentKind, choice.Trade, offer,
            world.Reach(actor, destination, ReachPurpose.MarketExchange, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static bool MarketOfferIsCurrent(IMarketStallWorld world, MarketExchangeActionPlan plan)
    {
        var current = world.Society.Inventory.GetOffer(plan.Choice.OfferId!);
        return current.State == DirectBarterState.Open && TownHearingProcedure.Digest(current) == TownHearingProcedure.Digest(plan.Offer);
    }

    internal static bool CanCollectMarketStock(IMarketStallWorld world, string actor, InventoryLot lot, MarketStallOccupancy? occupancy, InventoryCheckpoint inventory) =>
        (MarketTradeRules.IsLoose(lot) || lot.ItemKind == InventoryContainerRules.WaterJug &&
            world.VesselFits(lot, world.FreeCarryCapacity(actor)) && !world.HasActiveContainerReservation(inventory, lot.Id)) && OwnMarketGoods(world, actor, lot) &&
        (lot.OwnerId == actor || occupancy is null || occupancy.SellerHouseholdId != lot.OwnerId || occupancy.SellerAgentId == actor) &&
        world.PhysicalUnreservedQuantity(lot) > 0 && CanCarryMarketGoods(world, actor, lot);

    internal static MarketStockActionPlan PlanMarketStock(IMarketStallWorld world, string actor, MarketChoice choice)
    {
        var kind = choice.Kind == "load" ? MarketStockKind.Load : choice.Kind == "deposit" ? MarketStockKind.Deposit : MarketStockKind.Collect;
        var target = kind == MarketStockKind.Load ? world.HouseholdStockPosition(choice.Lot!) : MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var range = kind == MarketStockKind.Load ? world.HouseholdStockInteractionRange(choice.Lot!) : world.ResourceInteractionRange;
        return new(actor, new(kind, choice.Market.Id, choice.Stall.BuildingId, choice.Lot!.Id, choice.Quantity, choice.Occupancy?.Id,
            world.IsWithinInteractionRange(world.PhysicalAgents.Living[actor].Position, target, range)), choice.Town.Id, world.HouseholdFor(actor), choice.Market.Site,
            choice.Stall.SlotIndex, choice.Lot.OwnerId, choice.Lot.ItemKind, target, range, choice.Lot.CarrierId,
            choice.Lot.StorageBuildingId, choice.Lot.GroundPosition, choice.Occupancy,
            InventoryContainerRules.IsContainer(choice.Lot.ItemKind) ? GoodsPlanFamily.Capture(world.Society.Inventory, choice.Lot) : null,
            world.Reach(actor, target, ReachPurpose.MarketStock, range, 0, SwimmingPolicy.WhenCapable));
    }

    internal static bool MarketStockFamilyMatches(IMarketStallWorld world, InventoryLot lot, IReadOnlyList<PlannedStockMember>? family) =>
        GoodsPlanFamily.Matches(world.Society.Inventory, lot, family);

    internal static InventoryLot? CurrentMarketStock(IMarketStallWorld world, MarketStockActionPlan plan, TownMarketState market, MarketStallState stall, MarketStallOccupancy? occupancy)
    {
        if (plan.Choice.Kind == MarketStockKind.Collect)
        {
            var stock = MarketTradeRules.StockAt(market, stall.BuildingId, MarketContent.StallSite(market.Site, stall.SlotIndex), world.Society.Inventory)
                .SingleOrDefault(lot => lot.Id == plan.Choice.StockId);
            return stock is not null && CanCollectMarketStock(world, plan.Actor, stock, occupancy, world.Society.Inventory) &&
                Math.Min(MarketTradeRules.LoadQuantity, Math.Min(world.PhysicalUnreservedQuantity(stock), world.FreeCarryCapacity(plan.Actor))) >= plan.Choice.Quantity ? stock : null;
        }
        var goods = MarketGoodsFor(world, plan.Actor);
        var lot = goods.Matches.Select(match => match.Lot).SingleOrDefault(item => item.Id == plan.Choice.StockId);
        if (lot is null) return null;
        if (plan.Choice.Kind == MarketStockKind.Load)
            return MarketLoadSources(world, plan.Actor, goods).Any(item => item.Id == lot.Id) && !PersonalEquipmentRules.IsCarried(lot, plan.Actor) &&
                Math.Min(MarketTradeRules.LoadQuantity, Math.Min(MarketSurplus(world, plan.Actor, lot, goods), world.FreeCarryCapacity(plan.Actor))) >= plan.Choice.Quantity &&
                MarketStockQuantity(world, market, stall) + MarketIncomingPayment(world, market, stall) < MarketTradeRules.StallCapacity ? lot : null;
        var room = MarketTradeRules.StallCapacity - MarketStockQuantity(world, market, stall) - MarketIncomingPayment(world, market, stall);
        return MarketTradeRules.IsLoose(lot) && PersonalEquipmentRules.IsCarried(lot, plan.Actor) && !ProtectedMarketItem(world, plan.Actor, lot) &&
            Math.Min(MarketTradeRules.LoadQuantity, Math.Min(room, MarketSurplus(world, plan.Actor, lot, goods))) >= plan.Choice.Quantity ? lot : null;
    }

    internal static IEnumerable<MarketChoice> MarketChoices(IMarketStallWorld world, string actor)
    {
        if (!MarketAdult(world, actor)) yield break;
        var inventory = world.Society.Inventory;
        var ready = ReadyForMarket(world, actor);
        MarketGoodsBasis? goods = null;
        var offeredLoad = false;
        foreach (var town in world.Towns.OrderBy(item => item.Id, StringComparer.Ordinal))
            foreach (var market in town.Markets.OrderBy(item => item.Id, StringComparer.Ordinal))
                foreach (var stall in market.Stalls.OrderBy(item => item.SlotIndex))
                {
                    var position = MarketContent.StallSite(market.Site, stall.SlotIndex);
                    var occupancy = MarketOccupant(market, stall);
                    // Owners retain physical collection even after the building or borrowing ends.
                    foreach (var lot in MarketTradeRules.StockAt(market, stall.BuildingId, position, inventory)
                                 .Where(lot => CanCollectMarketStock(world, actor, lot, occupancy, inventory)))
                    {
                        var quantity = Math.Min(MarketTradeRules.LoadQuantity, Math.Min(world.PhysicalUnreservedQuantity(lot), world.FreeCarryCapacity(actor)));
                        if (quantity > 0 && world.CanReach(actor, position, ReachPurpose.MarketExchange, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable))
                            yield return new(MarketChoiceId("collect", market.Id, stall.BuildingId, lot.Id,
                                quantity.ToString(CultureInfo.InvariantCulture), MarketPlaceMode(world, actor, position, world.ResourceInteractionRange)),
                                "collect", town, market, stall, occupancy, lot, quantity);
                    }
                    if (!LiveMarketStall(world, town, market, stall)) continue;
                    goods ??= MarketGoodsFor(world, actor);
                    foreach (var trade in market.Trades.Where(item => IsOpenMarketTrade(item) && item.StallBuildingId == stall.BuildingId &&
                                 (item.SellerAgentId == actor || item.BuyerId == actor)).OrderBy(item => item.OfferId.Value, StringComparer.Ordinal))
                    {
                        if (inventory.GetOffer(trade.OfferId).State != DirectBarterState.Open) continue;
                        yield return new(MarketChoiceId("cancel", trade.OfferId), "cancel", town, market, stall, occupancy, Trade: trade);
                        if (!world.NeedsUrgentWarmth(world.PhysicalAgents.Living[actor]))
                            yield return new(MarketChoiceId("continue", trade.OfferId, MarketTradeMode(world, actor, trade)),
                                "continue", town, market, stall, occupancy, Trade: trade);
                    }
                    if (occupancy?.SellerAgentId == actor)
                    {
                        yield return new(MarketChoiceId("leave", occupancy.Id), "leave", town, market, stall, occupancy);
                        if (!ready || world.Society.GetAgent(actor).HouseholdId != occupancy.SellerHouseholdId ||
                            !world.CanReach(actor, position, ReachPurpose.MarketExchange, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable)) continue;
                        var room = MarketTradeRules.StallCapacity - MarketStockQuantity(world, market, stall) - MarketIncomingPayment(world, market, stall);
                        foreach (var lot in goods.Matches.Select(match => match.Lot).Where(lot => MarketTradeRules.IsLoose(lot) &&
                                     PersonalEquipmentRules.IsCarried(lot, actor) && !ProtectedMarketItem(world, actor, lot) && MarketSurplus(world, actor, lot, goods) > 0)
                                     .OrderBy(lot => lot.Id, StringComparer.Ordinal))
                        {
                            var quantity = Math.Min(MarketTradeRules.LoadQuantity, Math.Min(room, MarketSurplus(world, actor, lot, goods)));
                            if (quantity > 0)
                                yield return new(MarketChoiceId("deposit", occupancy.Id, lot.Id,
                                    quantity.ToString(CultureInfo.InvariantCulture), MarketPlaceMode(world, actor, position, world.ResourceInteractionRange)),
                                    "deposit", town, market, stall, occupancy, lot, quantity);
                        }
                    }
                    else if (occupancy is not null && MarketQuote(world, actor, market, stall, occupancy, goods) is { } quote)
                        yield return new(MarketChoiceId("buy", occupancy.Id, actor, quote.Goods.Id, quote.Payment.Id,
                            MarketPlaceMode(world, actor, position, world.ResourceInteractionRange)),
                            "buy", town, market, stall, occupancy, quote.Goods, 1, Payment: quote.Payment);
                    if (!ready || occupancy is not null || HasMarketOccupancy(world, actor) ||
                        world.Society.GetAgent(actor).HouseholdId is null ||
                        !world.CanReach(actor, position, ReachPurpose.MarketExchange, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable) ||
                        MarketStockQuantity(world, market, stall) + MarketIncomingPayment(world, market, stall) >= MarketTradeRules.StallCapacity)
                        continue;
                    var offeredBorrow = false;
                    foreach (var lot in MarketLoadSources(world, actor, goods))
                    {
                        if (PersonalEquipmentRules.IsCarried(lot, actor))
                        {
                            if (!offeredBorrow)
                            {
                                yield return new(MarketChoiceId("borrow", market.Id, stall.BuildingId, actor,
                                    MarketPlaceMode(world, actor, position, world.ResourceInteractionRange)),
                                    "borrow", town, market, stall);
                                offeredBorrow = true;
                            }
                            continue;
                        }
                        // One empty stall is enough to plan a load; the stall itself is borrowed on arrival.
                        if (offeredLoad) continue;
                        var quantity = Math.Min(MarketTradeRules.LoadQuantity, Math.Min(MarketSurplus(world, actor, lot, goods), world.FreeCarryCapacity(actor)));
                        if (quantity > 0 && world.CanReach(actor, world.HouseholdStockPosition(lot), ReachPurpose.MarketExchange, world.HouseholdStockInteractionRange(lot), 0, SwimmingPolicy.WhenCapable))
                            yield return new(MarketChoiceId("load", market.Id, stall.BuildingId, lot.Id,
                                quantity.ToString(CultureInfo.InvariantCulture), MarketPlaceMode(world, actor, world.HouseholdStockPosition(lot),
                                    world.HouseholdStockInteractionRange(lot))), "load", town, market, stall, Lot: lot, Quantity: quantity);
                    }
                    offeredLoad = true;
                }
    }

    internal static bool IsMarketFoodCandidate(IMarketStallWorld world, string actor, string candidateId) =>
        candidateId.StartsWith("market_collect:", StringComparison.Ordinal) && MarketChoices(world, actor).Any(choice =>
            choice.Id == candidateId && choice.Kind == "collect" && choice.Lot is { } lot && FoodRules.IsEdible(lot.ItemKind));

    internal static void PickUpMarketStock(IMarketStallWorld world, string actor, MarketChoice choice, GridPoint position)
    {
        world.PickUpMarketStock(actor, choice);
        world.AppendEvent(choice.Kind == "load" ? new MarketStockLoadedEventPayload(actor, choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, choice.Lot!.Id) : new MarketStockCollectedEventPayload(actor, choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, choice.Lot!.Id), position);
    }

    internal static void BorrowMarketStall(IMarketStallWorld world, string actor, MarketChoice choice, GridPoint position)
    {
        // Occupancy is claimed only by physical arrival, not a plan to arrive later.
        var occupancy = new MarketStallOccupancy(MarketTradeRules.Identity("market-occupancy:", choice.Market.Id,
            choice.Stall.BuildingId, actor, world.WorldTick.ToString(CultureInfo.InvariantCulture),
            choice.Market.Occupancies.Count.ToString(CultureInfo.InvariantCulture)),
            choice.Stall.BuildingId, actor, world.WorldTick)
        {
            SellerHouseholdId = world.Society.GetAgent(actor).HouseholdId!,
        };
        SetMarket(world, choice.Town.Id, choice.Market with { Occupancies = choice.Market.Occupancies.Append(occupancy).ToArray() });
        world.AppendEvent(new MarketStallBorrowedEventPayload(actor, choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, occupancy.Id), position);
    }

    /// <summary>A continued personal intention may walk, but physical arrival needs a new admitted choice.</summary>
    internal static void ContinueMarketWalk(IMarketStallWorld world, string actor, string id)
    {
        var choice = MarketChoices(world, actor).SingleOrDefault(item => item.Id == id);
        if (choice is null || choice.Kind is "leave" or "cancel") return;
        var destination = choice.Kind == "load" ? world.HouseholdStockPosition(choice.Lot!) :
            MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var range = choice.Kind == "load" ? world.HouseholdStockInteractionRange(choice.Lot!) : world.ResourceInteractionRange;
        if (!world.IsWithinInteractionRange(world.PhysicalAgents.Living[actor].Position, destination, range))
            world.MoveToward(actor, world.PhysicalAgents.Living[actor], destination, "market_" + choice.Kind, range);
    }

    internal static void DepositMarketStock(IMarketStallWorld world, string actor, MarketChoice choice, GridPoint position)
    {
        var lot = choice.Lot!;
        var receiptId = MarketTradeRules.ReceiptId(choice.Occupancy!.Id, lot.Id, lot.OwnerId, choice.Quantity,
            world.WorldTick, choice.Market.NextStockReceiptSequence);
        var operation = TownRecordIds.DepositOperation(receiptId);
        var movedId = lot.Quantity == choice.Quantity ? lot.Id : DerivedId.Create(lot.Id + "#move:" + operation);
        var eventId = world.DepositMarketLoad(actor, lot, choice.Quantity, operation, position);
        var receipt = new MarketStockReceipt(Identity.Parse(receiptId), choice.Occupancy.Id, actor, lot.OwnerId,
            lot.Id, movedId, lot.ItemKind, choice.Quantity, world.WorldTick, eventId)
        {
            Sequence = choice.Market.NextStockReceiptSequence,
        };
        SetMarket(world, choice.Town.Id, choice.Market with
        {
            StockReceipts = choice.Market.StockReceipts.Append(receipt).ToArray(),
            NextStockReceiptSequence = checked(choice.Market.NextStockReceiptSequence + 1),
        });
        world.AppendEvent(new MarketStockDeliveredEventPayload(actor, choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, receipt.Id), position);
    }

    internal static void OfferMarketTrade(IMarketStallWorld world, string buyer, MarketChoice choice, GridPoint position)
    {
        var occupancy = choice.Occupancy!;
        var seller = occupancy.SellerAgentId;
        var owner = occupancy.SellerHouseholdId;
        var id = MarketTradeRules.Identity(MarketTradeRules.OfferPrefix, occupancy.Id, buyer,
            choice.Lot!.Id, choice.Payment!.Id, world.WorldTick.ToString(CultureInfo.InvariantCulture),
            choice.Market.Trades.Count.ToString(CultureInfo.InvariantCulture));
        world.OpenMarketOffer(id, buyer, choice);
        var trade = new MarketTradeState(Identity.Parse(id), occupancy.Id, choice.Stall.BuildingId, seller,
            choice.Lot.OwnerId, owner, buyer, position, world.WorldTick, choice.Lot.ItemKind, choice.Payment.ItemKind)
        {
            StockReceiptSequence = choice.Market.StockReceipts.Last(receipt => receipt.OccupancyId == occupancy.Id &&
                receipt.OwnerId == choice.Lot.OwnerId && receipt.ItemKind == choice.Lot.ItemKind &&
                MarketTradeRules.IsReceiptLot(receipt, choice.Lot)).Sequence,
        };
        SetMarket(world, choice.Town.Id, choice.Market with { Trades = choice.Market.Trades.Append(trade).ToArray() });
        world.AppendEvent(new MarketTradeOfferedEventPayload(buyer, choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, seller, buyer, id), position);
    }

    internal static string? MarketTradeFailure(IMarketStallWorld world, TownRuntimeState town, TownMarketState market, MarketTradeState trade, DirectBarterOffer offer)
    {
        if (offer.ExpiryTick < world.WorldTick) return "The offer ran out of time.";
        var stall = market.Stalls.Single(item => item.BuildingId == trade.StallBuildingId);
        var occupancy = market.Occupancies.Single(item => item.Id == trade.OccupancyId);
        if (!LiveMarketStall(world, town, market, stall) || occupancy.EndedTick is not null ||
            MarketOccupant(market, stall)?.Id != occupancy.Id || !MarketAdult(world, trade.SellerAgentId) ||
            !MarketTradeRules.IsInside(market, world.PhysicalAgents.Living[trade.SellerAgentId].Position) ||
            world.Society.GetAgent(trade.SellerAgentId).HouseholdId != trade.PaymentOwnerId)
            return "The seller no longer has this stall or no longer belongs to the household being paid.";
        if (!MarketAdult(world, trade.BuyerId)) return "The buyer is no longer available.";
        var inventory = world.Society.Inventory;
        var goods = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.FirstLotId);
        var payment = inventory.Lots.SingleOrDefault(lot => lot.Id == offer.SecondLotId);
        if (goods is null || payment is null || goods.OwnerId != trade.GoodsOwnerId ||
            !MarketTradeRules.IsAt(goods, trade.Position) ||
            !MarketTradeRules.MaySell(market, occupancy, goods, world.Society, trade.SellerAgentId) ||
            goods.ConditionBasisPoints <= 0 || goods.FreshnessBasisPoints <= 0 || goods.Quantity < offer.FirstQuantity ||
            payment.OwnerId != trade.BuyerId || !MarketTradeRules.IsLoose(payment) ||
            !PersonalEquipmentRules.IsCarried(payment, trade.BuyerId) || payment.GroundPosition is not null ||
            payment.StorageBuildingId is not null || payment.ConditionBasisPoints <= 0 || payment.FreshnessBasisPoints <= 0 ||
            payment.Quantity < offer.SecondQuantity || ProtectedMarketItem(world, trade.BuyerId, payment) ||
            !MarketTradeValidation.HasExactOpenClaim(inventory, offer, true) ||
            !MarketTradeValidation.HasExactOpenClaim(inventory, offer, false))
            return "The goods set aside for this trade are no longer usable at the agreed place.";
        if (MarketStockQuantity(world, market, stall) + MarketIncomingPayment(world, market, stall) > MarketTradeRules.StallCapacity ||
            PersonalEquipmentRules.CarriedQuantity(inventory, trade.BuyerId, world.PhysicalAgents.Living[trade.BuyerId].Equipment) +
            world.ReservedShopCarrySpace(trade.BuyerId) > PersonalEquipmentRules.Capacity(inventory, trade.BuyerId, world.PhysicalAgents.Living[trade.BuyerId].Equipment) + world.HorseCargoCapacity(trade.BuyerId))
            return "There is no longer enough room to receive the goods.";
        if (!world.IsWithinInteractionRange(world.PhysicalAgents.Living[trade.BuyerId].Position, trade.Position, world.ResourceInteractionRange) &&
            world.Reach(trade.BuyerId, trade.Position, ReachPurpose.MarketExchange, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count == 0)
            return "The buyer can no longer reach the stall.";
        return null;
    }

    internal static void ContinueMarketTrade(IMarketStallWorld world, string actor, AgentState state, MarketChoice choice, bool cancel, DirectBarterOffer? originalOffer = null)
    {
        var trade = choice.Trade!;
        var offer = originalOffer ?? world.Society.Inventory.GetOffer(trade.OfferId);
        if (cancel || MarketTradeFailure(world, choice.Town, choice.Market, trade, offer) is { })
        {
            CancelMarketTrade(world, choice.Town.Id, choice.Market, trade, cancel
                ? "One of the traders cancelled the exchange." : MarketTradeFailure(world, choice.Town, choice.Market, trade, offer)!, cancel ? actor : null);
            return;
        }
        if (!world.IsWithinInteractionRange(state.Position, trade.Position, world.ResourceInteractionRange))
        {
            world.MoveToward(actor, state, trade.Position, "market_trade", world.ResourceInteractionRange);
            return;
        }
        if (actor != trade.SellerAgentId || !world.IsWithinInteractionRange(world.PhysicalAgents.Living[trade.BuyerId].Position,
                trade.Position, world.ResourceInteractionRange)) return;
        // The named seller's own admitted choice accepts for personal goods or their current household.
        var goods = world.Society.Inventory.GetLot(offer.FirstLotId);
        var payment = world.Society.Inventory.GetLot(offer.SecondLotId);
        var goodsId = goods.Quantity == offer.FirstQuantity ? goods.Id : DerivedId.Create(goods.Id + "#barter:" + offer.Id);
        var paymentId = payment.Quantity == offer.SecondQuantity ? payment.Id : DerivedId.Create(payment.Id + "#barter:" + offer.Id);
        var receiptId = MarketTradeRules.ReceiptId(trade.OccupancyId, paymentId, trade.PaymentOwnerId,
            offer.SecondQuantity, world.WorldTick, choice.Market.NextStockReceiptSequence, offer.Id);
        var eventId = world.SettleMarketOffer(actor, trade, offer, receiptId, goodsId, paymentId);
        var receipt = new MarketStockReceipt(Identity.Parse(receiptId), trade.OccupancyId, actor, trade.PaymentOwnerId,
            paymentId, paymentId, trade.PaymentKind, offer.SecondQuantity, world.WorldTick, eventId, offer.Id)
        {
            Sequence = choice.Market.NextStockReceiptSequence,
        };
        SetMarket(world, choice.Town.Id, choice.Market with
        {
            Trades = choice.Market.Trades.Select(item => item.OfferId == trade.OfferId ? trade with { SettledTick = world.WorldTick } : item).ToArray(),
            StockReceipts = choice.Market.StockReceipts.Append(receipt).ToArray(),
            NextStockReceiptSequence = checked(choice.Market.NextStockReceiptSequence + 1),
        });
        world.AppendEvent(new MarketTradeCompletedEventPayload(actor, choice.Town.Id, choice.Market.Id, choice.Stall.BuildingId, trade.BuyerId, trade.OfferId), trade.Position);
    }

    internal static void CancelMarketTrade(IMarketStallWorld world, string townId, TownMarketState market, MarketTradeState trade, string reason, string? actor = null)
    {
        var offer = world.Society.Inventory.GetOffer(trade.OfferId);
        if (offer.State == DirectBarterState.Open)
            world.CancelMarketOffer(offer);
        SetMarket(world, townId, market with
        {
            Trades = market.Trades.Select(item => item.OfferId == trade.OfferId
            ? trade with { CancellationReason = reason } : item).ToArray()
        });
        world.AppendEvent(new MarketTradeCancelledEventPayload(actor, townId, market.Id, trade.StallBuildingId, trade.SellerAgentId, trade.BuyerId, trade.OfferId), trade.Position);
    }

    internal static void EndMarketOccupancy(IMarketStallWorld world, string townId, TownMarketState market, MarketStallOccupancy occupancy, string reason, string? actor = null)
    {
        foreach (var trade in market.Trades.Where(item => IsOpenMarketTrade(item) && item.OccupancyId == occupancy.Id &&
                     world.Society.Inventory.GetOffer(item.OfferId).State == DirectBarterState.Open).ToArray())
        {
            CancelMarketTrade(world, townId, market, trade, reason, actor);
            market = world.Towns.Single(town => town.Id == townId).Markets.Single(item => item.Id == market.Id);
        }
        market = market with
        {
            Occupancies = market.Occupancies.Select(item => item.Id == occupancy.Id
            ? occupancy with { EndedTick = world.WorldTick, ReleaseReason = reason } : item).ToArray()
        };
        SetMarket(world, townId, market);
        var stall = market.Stalls.Single(item => item.BuildingId == occupancy.StallBuildingId);
        world.AppendEvent(new MarketStallLeftEventPayload(occupancy.SellerAgentId, townId, market.Id, stall.BuildingId, occupancy.Id), MarketContent.StallSite(market.Site, stall.SlotIndex));
    }

    internal static void MaintainMarkets(IMarketStallWorld world)
    {
        foreach (var originalTown in world.Towns.ToArray())
            foreach (var originalMarket in originalTown.Markets.ToArray())
            {
                var market = originalMarket;
                foreach (var occupancy in market.Occupancies.Where(item => item.EndedTick is null).ToArray())
                {
                    var stall = market.Stalls.Single(item => item.BuildingId == occupancy.StallBuildingId);
                    var reason = !LiveMarketStall(world, originalTown, market, stall) ? "The Market stall is no longer available." :
                        !MarketAdult(world, occupancy.SellerAgentId) ? "The seller is no longer available." :
                        world.Society.GetAgent(occupancy.SellerAgentId).HouseholdId != occupancy.SellerHouseholdId
                            ? "The seller's household changed." :
                        !MarketTradeRules.IsInside(market, world.PhysicalAgents.Living[occupancy.SellerAgentId].Position) ? "The seller left the Market." : null;
                    if (reason is null) continue;
                    EndMarketOccupancy(world, originalTown.Id, market, occupancy, reason);
                    market = world.Towns.Single(town => town.Id == originalTown.Id).Markets.Single(item => item.Id == market.Id);
                }
                foreach (var trade in market.Trades.Where(IsOpenMarketTrade).ToArray())
                {
                    var offer = world.Society.Inventory.GetOffer(trade.OfferId);
                    // The inventory closes an offer at its deadline before this check sees it.
                    var failure = offer.State == DirectBarterState.Open ? MarketTradeFailure(world, originalTown, market, trade, offer) :
                        offer.State != DirectBarterState.Cancelled ? null :
                        offer.ExpiryTick < world.WorldTick ? "The offer ran out of time." : "The offer was cancelled.";
                    if (failure is null) continue;
                    CancelMarketTrade(world, originalTown.Id, market, trade, failure);
                    market = world.Towns.Single(town => town.Id == originalTown.Id).Markets.Single(item => item.Id == market.Id);
                }
                // Removal ends borrowing but retains the stock history and owners' physical collection.
                var built = world.Buildings.Select(building => building.InstanceId).ToHashSet(StringComparer.Ordinal);
                if ((market.RemovedTick is not null || built.Contains(market.HallBuildingId)) &&
                    market.Stalls.All(stall => stall.RemovedTick is not null || built.Contains(stall.BuildingId)))
                    continue;
                SetMarket(world, originalTown.Id, market with
                {
                    RemovedTick = built.Contains(market.HallBuildingId) ? market.RemovedTick : market.RemovedTick ?? world.WorldTick,
                    Stalls = market.Stalls.Select(stall => built.Contains(stall.BuildingId)
                        ? stall : stall with { RemovedTick = stall.RemovedTick ?? world.WorldTick }).ToArray(),
                });
            }
        world.RetireMarketHistory();
    }
}
