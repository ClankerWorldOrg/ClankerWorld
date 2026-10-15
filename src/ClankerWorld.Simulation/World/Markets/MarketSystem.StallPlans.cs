using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.World;

internal sealed partial class MarketSystem
{
    internal static ActorCapabilityPolicy MarketStockCapabilities(MarketStockActionPlan plan) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: plan.Choice.Kind == MarketStockKind.Collect && FoodRules.IsEdible(plan.ItemKind),
            AllowUrgentWarmth: plan.Choice.Kind == MarketStockKind.Collect, AllowActiveOrder: true);
    internal static ActorCapabilityPolicy MarketExchangeCapabilities(MarketExchangeKind kind) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: kind == MarketExchangeKind.Cancel, AllowActiveOrder: true);
    internal static ActorCapabilityPolicy MarketOccupancyCapabilities(bool leave) =>
        new(ActionLifeStages.Adults, AllowUrgentFood: false, AllowUrgentWarmth: leave, AllowActiveOrder: true);


    internal static MarketOccupancyActionPlan? PlanMarketOccupancy(IMarketStallWorld world, string actor, MarketChoice choice)
    {
        var leave = choice.Kind == "leave";
        var destination = MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var stock = leave ? null : MarketLoadSources(world, actor, MarketGoodsFor(world, actor)).FirstOrDefault(lot => PersonalEquipmentRules.IsCarried(lot, actor));
        if (!leave && stock is null) return null;
        return new(actor, new(leave, choice.Market.Id, choice.Stall.BuildingId, actor, choice.Occupancy?.Id,
            world.IsWithinInteractionRange(world.PhysicalAgents.Living[actor].Position, destination, world.ResourceInteractionRange)), choice.Town.Id,
            world.HouseholdFor(actor)!, choice.Market.HallBuildingId, choice.Market.Site, choice.Stall.SlotIndex, destination,
            choice.Occupancy, stock?.Id, stock?.OwnerId, stock?.ItemKind,
            world.Reach(actor, destination, ReachPurpose.MarketStall, world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    internal static ActionCheck CheckMarketOccupancy(IMarketStallWorld world, MarketOccupancyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MarketOccupancyCapabilities(plan.Choice.Leave));
        if (!capability.Allowed) return capability;
        if (!MarketAdult(world, plan.Actor) || world.Towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Markets.SingleOrDefault(market => market.Id == plan.Choice.MarketId) is not { } market ||
            market.HallBuildingId != plan.HallId || market.Site != plan.MarketSite ||
            market.Stalls.SingleOrDefault(stall => stall.BuildingId == plan.Choice.StallId) is not { } stall ||
            stall.SlotIndex != plan.Slot || !LiveMarketStall(world, town, market, stall))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Market stall has changed");
        var occupancy = MarketOccupant(market, stall);
        if (plan.Choice.Leave)
            return occupancy == plan.Occupancy && occupancy?.SellerAgentId == plan.Actor ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected stall borrowing has ended");
        if (!ReadyForMarket(world, plan.Actor) || world.HouseholdFor(plan.Actor) != plan.HouseholdId || occupancy is not null || HasMarketOccupancy(world, plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected empty stall or household authority is unavailable");
        if (MarketStockQuantity(world, market, stall) + MarketIncomingPayment(world, market, stall) >= MarketTradeRules.StallCapacity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected stall has no room for goods");
        var goods = MarketGoodsFor(world, plan.Actor);
        if (!MarketLoadSources(world, plan.Actor, goods).Any(lot => lot.Id == plan.StockId && lot.OwnerId == plan.StockOwnerId && lot.ItemKind == plan.ItemKind &&
                PersonalEquipmentRules.IsCarried(lot, plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained personal stall stock is no longer available");
        var atSite = world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Destination, world.ResourceInteractionRange);
        return plan.Choice.AtSite == atSite && (atSite || world.Reach(plan.Actor, plan.Destination, ReachPurpose.MarketStall,
            world.ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Market trip has ended or is out of reach");
    }

    internal static void ApplyCheckedMarketOccupancy(IMarketStallWorld world, MarketOccupancyActionPlan plan)
    {
        var town = world.Towns.Single(item => item.Id == plan.TownId);
        var market = town.Markets.Single(item => item.Id == plan.Choice.MarketId);
        var stall = market.Stalls.Single(item => item.BuildingId == plan.Choice.StallId);
        if (plan.Choice.Leave) { EndMarketOccupancy(world, town.Id, market, plan.Occupancy!, "The seller gave up the stall."); return; }
        if (!plan.Choice.AtSite) { world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.Destination, "market_borrow", world.ResourceInteractionRange); return; }
        BorrowMarketStall(world, plan.Actor, new(plan.CandidateId, "borrow", town, market, stall), plan.Destination);
    }

    internal static ActionCheck CheckMarketStock(IMarketStallWorld world, MarketStockActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MarketStockCapabilities(plan));
        if (!capability.Allowed) return capability;
        if (!MarketAdult(world, plan.Actor) || world.HouseholdFor(plan.Actor) != plan.HouseholdId ||
            world.Towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Markets.SingleOrDefault(market => market.Id == plan.Choice.MarketId) is not { } market || market.Site != plan.MarketSite ||
            market.Stalls.SingleOrDefault(stall => stall.BuildingId == plan.Choice.StallId) is not { } stall || stall.SlotIndex != plan.Slot)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Market, stall or household has changed");
        var occupancy = MarketOccupant(market, stall);
        if (plan.Choice.Kind != MarketStockKind.Collect && (!ReadyForMarket(world, plan.Actor) || !LiveMarketStall(world, town, market, stall)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Market stock work is unavailable");
        if (plan.Choice.Kind == MarketStockKind.Deposit && (occupancy != plan.Occupancy || occupancy?.SellerAgentId != plan.Actor ||
                occupancy.SellerHouseholdId != plan.HouseholdId) || plan.Choice.Kind == MarketStockKind.Load &&
            (occupancy is not null || HasMarketOccupancy(world, plan.Actor) || plan.HouseholdId is null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected stall borrowing has changed");
        var lot = CurrentMarketStock(world, plan, market, stall, occupancy);
        if (lot is null || lot.OwnerId != plan.OwnerId || lot.ItemKind != plan.ItemKind || lot.CarrierId != plan.CarrierId ||
            lot.StorageBuildingId != plan.StorageBuildingId || lot.GroundPosition != plan.GroundPosition ||
            !MarketStockFamilyMatches(world, lot, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained Market goods or exact load are unavailable");
        if (plan.Choice.Kind == MarketStockKind.Load && (world.HouseholdStockPosition(lot) != plan.Target || world.HouseholdStockInteractionRange(lot) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Market pickup location has changed");
        if (plan.Choice.Kind == MarketStockKind.Collect && world.NeedsUrgentFood(world.PhysicalAgents.Living[plan.Actor]) && world.AvailableLotQuantity(lot) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected food is no longer usable");
        var atSite = world.IsWithinInteractionRange(world.PhysicalAgents.Living[plan.Actor].Position, plan.Target, plan.InteractionRange);
        return plan.Choice.AtSite == atSite && (atSite || world.Reach(plan.Actor, plan.Target, ReachPurpose.MarketStock,
            plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Market stock trip has ended or is out of reach");
    }

    internal static void ApplyCheckedMarketStock(IMarketStallWorld world, MarketStockActionPlan plan)
    {
        if (!plan.Choice.AtSite) { world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.Target, "market_" + plan.Choice.Kind.ToString().ToLowerInvariant(), plan.InteractionRange); return; }
        var town = world.Towns.Single(item => item.Id == plan.TownId);
        var market = town.Markets.Single(item => item.Id == plan.Choice.MarketId);
        var stall = market.Stalls.Single(item => item.BuildingId == plan.Choice.StallId);
        var lot = CurrentMarketStock(world, plan, market, stall, MarketOccupant(market, stall))!;
        var kind = plan.Choice.Kind.ToString().ToLowerInvariant();
        var choice = new MarketChoice(plan.CandidateId, kind, town, market, stall,
            plan.Choice.Kind == MarketStockKind.Collect ? MarketOccupant(market, stall) : plan.Occupancy, lot, plan.Choice.Quantity);
        if (plan.Choice.Kind == MarketStockKind.Deposit) DepositMarketStock(world, plan.Actor, choice, plan.Target);
        else PickUpMarketStock(world, plan.Actor, choice, plan.Target);
    }

    internal static ActionCheck CheckMarketExchange(IMarketStallWorld world, MarketExchangeActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(world.CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MarketExchangeCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (!MarketAdult(world, plan.Actor) || world.Towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Markets.SingleOrDefault(market => market.Id == plan.MarketId) is not { } market || market.Site != plan.MarketSite ||
            market.Stalls.SingleOrDefault(stall => stall.BuildingId == plan.StallId) is not { } stall || stall.SlotIndex != plan.Slot ||
            !LiveMarketStall(world, town, market, stall) || MarketOccupant(market, stall) != plan.Occupancy)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Market exchange or stall borrowing has changed");
        if (plan.Choice.Kind == MarketExchangeKind.Buy)
        {
            if (MarketQuote(world, plan.Actor, market, stall, plan.Occupancy, selectedGoodsId: plan.Choice.GoodsId,
                    selectedPaymentId: plan.Choice.PaymentId) is not { } quote || quote.Goods.OwnerId != plan.GoodsOwnerId ||
                quote.Goods.ItemKind != plan.GoodsKind || quote.Payment.ItemKind != plan.PaymentKind)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Market goods or payment are unavailable");
            var mode = MarketPlaceMode(world, plan.Actor, plan.Destination, world.ResourceInteractionRange);
            return mode == plan.Choice.Mode ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected buying trip has ended");
        }
        if (market.Trades.SingleOrDefault(trade => trade.OfferId == plan.Choice.OfferId) is not { } current || current != plan.Trade ||
            !IsOpenMarketTrade(current) || current.SellerAgentId != plan.Actor && current.BuyerId != plan.Actor || !MarketOfferIsCurrent(world, plan))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected offer or its exact exchange terms have changed");
        if (plan.Choice.Kind == MarketExchangeKind.Cancel) return new();
        if (MarketTradeMode(world, plan.Actor, current) != plan.Choice.Mode)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected trader arrival phase has changed");
        return new();
    }

    internal static void ApplyCheckedMarketExchange(IMarketStallWorld world, MarketExchangeActionPlan plan)
    {
        var town = world.Towns.Single(item => item.Id == plan.TownId);
        var market = town.Markets.Single(item => item.Id == plan.MarketId);
        var stall = market.Stalls.Single(item => item.BuildingId == plan.StallId);
        if (plan.Choice.Kind != MarketExchangeKind.Buy)
        {
            ContinueMarketTrade(world, plan.Actor, world.PhysicalAgents.Living[plan.Actor], new(plan.CandidateId,
                plan.Choice.Kind == MarketExchangeKind.Cancel ? "cancel" : "continue", town, market, stall, plan.Occupancy,
                Trade: plan.Trade), plan.Choice.Kind == MarketExchangeKind.Cancel, plan.Offer);
            return;
        }
        if (plan.Choice.Mode == "walking") { world.MoveToward(plan.Actor, world.PhysicalAgents.Living[plan.Actor], plan.Destination, "market_buy", world.ResourceInteractionRange); return; }
        var quote = MarketQuote(world, plan.Actor, market, stall, plan.Occupancy, selectedGoodsId: plan.Choice.GoodsId, selectedPaymentId: plan.Choice.PaymentId)!.Value;
        OfferMarketTrade(world, plan.Actor, new(plan.CandidateId, "buy", town, market, stall, plan.Occupancy, quote.Goods, 1, Payment: quote.Payment), plan.Destination);
    }
}
