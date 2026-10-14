namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private MarketOccupancyActionPlan? PlanMarketOccupancy(string actor, MarketChoice choice)
    {
        var leave = choice.Kind == "leave";
        var destination = MarketContent.StallSite(choice.Market.Site, choice.Stall.SlotIndex);
        var stock = leave ? null : MarketLoadSources(actor, MarketGoodsFor(actor)).FirstOrDefault(lot => PersonalEquipmentRules.IsCarried(lot, actor));
        if (!leave && stock is null) return null;
        return new(actor, new(leave, choice.Market.Id, choice.Stall.BuildingId, actor, choice.Occupancy?.Id,
            IsWithinInteractionRange(agents[actor].Position, destination, ResourceInteractionRange)), choice.Town.Id,
            HouseholdFor(actor)!, choice.Market.HallBuildingId, choice.Market.Site, choice.Stall.SlotIndex, destination,
            choice.Occupancy, stock?.Id, stock?.OwnerId, stock?.ItemKind,
            Reach(actor, destination, ReachPurpose.MarketStall, ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable));
    }

    private ActionCheck CheckMarketOccupancy(MarketOccupancyActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MarketOccupancyCapabilities(plan.Choice.Leave));
        if (!capability.Allowed) return capability;
        if (!MarketAdult(plan.Actor) || towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Markets.SingleOrDefault(market => market.Id == plan.Choice.MarketId) is not { } market ||
            market.HallBuildingId != plan.HallId || market.Site != plan.MarketSite ||
            market.Stalls.SingleOrDefault(stall => stall.BuildingId == plan.Choice.StallId) is not { } stall ||
            stall.SlotIndex != plan.Slot || !LiveMarketStall(town, market, stall))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Market stall has changed");
        var occupancy = MarketOccupant(market, stall);
        if (plan.Choice.Leave)
            return occupancy == plan.Occupancy && occupancy?.SellerAgentId == plan.Actor ? new() :
                ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected stall borrowing has ended");
        if (!ReadyForMarket(plan.Actor) || HouseholdFor(plan.Actor) != plan.HouseholdId || occupancy is not null || HasMarketOccupancy(plan.Actor))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected empty stall or household authority is unavailable");
        if (MarketStockQuantity(market, stall) + MarketIncomingPayment(market, stall) >= MarketTradeRules.StallCapacity)
            return ActionCheck.Refuse(ActionRefusalReason.Capacity, "the selected stall has no room for goods");
        var goods = MarketGoodsFor(plan.Actor);
        if (!MarketLoadSources(plan.Actor, goods).Any(lot => lot.Id == plan.StockId && lot.OwnerId == plan.StockOwnerId && lot.ItemKind == plan.ItemKind &&
                PersonalEquipmentRules.IsCarried(lot, plan.Actor)))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained personal stall stock is no longer available");
        var atSite = IsWithinInteractionRange(agents[plan.Actor].Position, plan.Destination, ResourceInteractionRange);
        return plan.Choice.AtSite == atSite && (atSite || Reach(plan.Actor, plan.Destination, ReachPurpose.MarketStall,
            ResourceInteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Market trip has ended or is out of reach");
    }

    private void ApplyMarketOccupancy(MarketOccupancyActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedMarketOccupancy(plan);
    }

    private void ApplyCheckedMarketOccupancy(MarketOccupancyActionPlan plan)
    {
        var town = towns.Single(item => item.Id == plan.TownId);
        var market = town.Markets.Single(item => item.Id == plan.Choice.MarketId);
        var stall = market.Stalls.Single(item => item.BuildingId == plan.Choice.StallId);
        if (plan.Choice.Leave) { EndMarketOccupancy(town.Id, market, plan.Occupancy!, "The seller gave up the stall."); return; }
        if (!plan.Choice.AtSite) { MoveToward(plan.Actor, agents[plan.Actor], plan.Destination, "market_borrow", ResourceInteractionRange); return; }
        BorrowMarketStall(plan.Actor, new(plan.CandidateId, "borrow", town, market, stall), plan.Destination);
    }
}
