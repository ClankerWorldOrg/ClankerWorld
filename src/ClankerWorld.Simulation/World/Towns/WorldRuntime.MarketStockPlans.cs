namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckMarketStock(MarketStockActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MarketStockCapabilities(plan));
        if (!capability.Allowed) return capability;
        if (!MarketAdult(plan.Actor) || HouseholdFor(plan.Actor) != plan.HouseholdId ||
            towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Markets.SingleOrDefault(market => market.Id == plan.Choice.MarketId) is not { } market || market.Site != plan.MarketSite ||
            market.Stalls.SingleOrDefault(stall => stall.BuildingId == plan.Choice.StallId) is not { } stall || stall.SlotIndex != plan.Slot)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Market, stall or household has changed");
        var occupancy = MarketOccupant(market, stall);
        if (plan.Choice.Kind != MarketStockKind.Collect && (!ReadyForMarket(plan.Actor) || !LiveMarketStall(town, market, stall)))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "their selected Market stock work is unavailable");
        if (plan.Choice.Kind == MarketStockKind.Deposit && (occupancy != plan.Occupancy || occupancy?.SellerAgentId != plan.Actor ||
                occupancy.SellerHouseholdId != plan.HouseholdId) || plan.Choice.Kind == MarketStockKind.Load &&
            (occupancy is not null || HasMarketOccupancy(plan.Actor) || plan.HouseholdId is null))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected stall borrowing has changed");
        var lot = CurrentMarketStock(plan, market, stall, occupancy);
        if (lot is null || lot.OwnerId != plan.OwnerId || lot.ItemKind != plan.ItemKind || lot.CarrierId != plan.CarrierId ||
            lot.StorageBuildingId != plan.StorageBuildingId || lot.GroundPosition != plan.GroundPosition ||
            !MarketStockFamilyMatches(lot, plan.Family))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the retained Market goods or exact load are unavailable");
        if (plan.Choice.Kind == MarketStockKind.Load && (HouseholdStockPosition(lot) != plan.Target || HouseholdStockInteractionRange(lot) != plan.InteractionRange))
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Market pickup location has changed");
        if (plan.Choice.Kind == MarketStockKind.Collect && NeedsUrgentFood(agents[plan.Actor]) && AvailableLotQuantity(lot) <= 0)
            return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected food is no longer usable");
        var atSite = IsWithinInteractionRange(agents[plan.Actor].Position, plan.Target, plan.InteractionRange);
        return plan.Choice.AtSite == atSite && (atSite || Reach(plan.Actor, plan.Target, ReachPurpose.MarketStock,
            plan.InteractionRange, 0, SwimmingPolicy.WhenCapable).Count > 0) ? new() :
            ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected Market stock trip has ended or is out of reach");
    }

    private void ApplyMarketStock(MarketStockActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedMarketStock(plan);
    }

    private void ApplyCheckedMarketStock(MarketStockActionPlan plan)
    {
        if (!plan.Choice.AtSite) { MoveToward(plan.Actor, agents[plan.Actor], plan.Target, "market_" + plan.Choice.Kind.ToString().ToLowerInvariant(), plan.InteractionRange); return; }
        var town = towns.Single(item => item.Id == plan.TownId);
        var market = town.Markets.Single(item => item.Id == plan.Choice.MarketId);
        var stall = market.Stalls.Single(item => item.BuildingId == plan.Choice.StallId);
        var lot = CurrentMarketStock(plan, market, stall, MarketOccupant(market, stall))!;
        var kind = plan.Choice.Kind.ToString().ToLowerInvariant();
        var choice = new MarketChoice(plan.CandidateId, kind, town, market, stall,
            plan.Choice.Kind == MarketStockKind.Collect ? MarketOccupant(market, stall) : plan.Occupancy, lot, plan.Choice.Quantity);
        if (plan.Choice.Kind == MarketStockKind.Deposit) DepositMarketStock(plan.Actor, choice, plan.Target);
        else PickUpMarketStock(plan.Actor, choice, plan.Target);
    }
}
