namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private ActionCheck CheckMarketExchange(MarketExchangeActionPlan plan, ActorCapabilityPolicy? policy = null)
    {
        var capability = ActorCapabilities.Check(CapabilitiesFor(plan.Actor), plan.CandidateId,
            policy ?? MarketExchangeCapabilities(plan.Choice.Kind));
        if (!capability.Allowed) return capability;
        if (!MarketAdult(plan.Actor) || towns.SingleOrDefault(town => town.Id == plan.TownId) is not { } town ||
            town.Markets.SingleOrDefault(market => market.Id == plan.MarketId) is not { } market || market.Site != plan.MarketSite ||
            market.Stalls.SingleOrDefault(stall => stall.BuildingId == plan.StallId) is not { } stall || stall.SlotIndex != plan.Slot ||
            !LiveMarketStall(town, market, stall) || MarketOccupant(market, stall) != plan.Occupancy)
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected Market exchange or stall borrowing has changed");
        if (plan.Choice.Kind == MarketExchangeKind.Buy)
        {
            if (MarketQuote(plan.Actor, market, stall, plan.Occupancy, selectedGoodsId: plan.Choice.GoodsId,
                    selectedPaymentId: plan.Choice.PaymentId) is not { } quote || quote.Goods.OwnerId != plan.GoodsOwnerId ||
                quote.Goods.ItemKind != plan.GoodsKind || quote.Payment.ItemKind != plan.PaymentKind)
                return ActionCheck.Refuse(ActionRefusalReason.StockUnavailable, "the selected Market goods or payment are unavailable");
            var mode = MarketPlaceMode(plan.Actor, plan.Destination, ResourceInteractionRange);
            return mode == plan.Choice.Mode ? new() : ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected buying trip has ended");
        }
        if (market.Trades.SingleOrDefault(trade => trade.OfferId == plan.Choice.OfferId) is not { } current || current != plan.Trade ||
            !IsOpenMarketTrade(current) || current.SellerAgentId != plan.Actor && current.BuyerId != plan.Actor || !MarketOfferIsCurrent(plan))
            return ActionCheck.Refuse(ActionRefusalReason.TargetUnavailable, "the selected offer or its exact exchange terms have changed");
        if (plan.Choice.Kind == MarketExchangeKind.Cancel) return new();
        if (MarketTradeMode(plan.Actor, current) != plan.Choice.Mode)
            return ActionCheck.Refuse(ActionRefusalReason.OutOfReach, "the selected trader arrival phase has changed");
        return new();
    }

    private void ApplyMarketExchange(MarketExchangeActionPlan plan)
    {
        if (CheckActionPlan(plan).Refusal is { } refusal) { RefuseChoice(plan, refusal); return; }
        ApplyCheckedMarketExchange(plan);
    }

    private void ApplyCheckedMarketExchange(MarketExchangeActionPlan plan)
    {
        var town = towns.Single(item => item.Id == plan.TownId);
        var market = town.Markets.Single(item => item.Id == plan.MarketId);
        var stall = market.Stalls.Single(item => item.BuildingId == plan.StallId);
        if (plan.Choice.Kind != MarketExchangeKind.Buy)
        {
            ContinueMarketTrade(plan.Actor, agents[plan.Actor], new(plan.CandidateId,
                plan.Choice.Kind == MarketExchangeKind.Cancel ? "cancel" : "continue", town, market, stall, plan.Occupancy,
                Trade: plan.Trade), plan.Choice.Kind == MarketExchangeKind.Cancel, plan.Offer);
            return;
        }
        if (plan.Choice.Mode == "walking") { MoveToward(plan.Actor, agents[plan.Actor], plan.Destination, "market_buy", ResourceInteractionRange); return; }
        var quote = MarketQuote(plan.Actor, market, stall, plan.Occupancy, selectedGoodsId: plan.Choice.GoodsId, selectedPaymentId: plan.Choice.PaymentId)!.Value;
        OfferMarketTrade(plan.Actor, new(plan.CandidateId, "buy", town, market, stall, plan.Occupancy, quote.Goods, 1, Payment: quote.Payment), plan.Destination);
    }
}
