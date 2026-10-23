namespace ClankerWorld.Simulation.World;

public sealed partial class WorldRuntime
{
    private static ActorCapabilityPolicy ShopCapabilities => MarketSystem.ShopCapabilities;
    private static ActorCapabilityPolicy PersonalBarterCapabilities => MarketSystem.PersonalBarterCapabilities;
    private static ActorCapabilityPolicy MarketStockCapabilities(MarketStockActionPlan plan) => MarketSystem.MarketStockCapabilities(plan);

    private static ActionDefinition<ShopBuyChoice, ShopBuyActionPlan> ShopBuyDefinition => MarketActions.ShopBuy;
    private static ActionDefinition<ShopResponseChoice, ShopResponseActionPlan> ShopResponseDefinition => MarketActions.ShopResponse;
    private static ActionDefinition<DirectBarterChoice, DirectBarterActionPlan> DirectBarterDefinition => MarketActions.Barter;
    private static ActionDefinition<MarketActionRequest, MarketOccupancyActionPlan> MarketOccupancyDefinition => MarketActions.Occupancy;
    private static ActionDefinition<MarketActionRequest, MarketStockActionPlan> MarketStockDefinition => MarketActions.Stock;
    private static ActionDefinition<MarketActionRequest, MarketExchangeActionPlan> MarketExchangeDefinition => MarketActions.Exchange;

    private TPlan? PlanMarketRequest<TPlan>(string actor, MarketActionRequest request) where TPlan : ActionPlan
    {
        if (MarketChoices(actor).SingleOrDefault(item => item.Id == request.CandidateId) is not { } choice) return null;
        ActionPlan? plan = request.Family switch
        {
            MarketActionFamily.Occupancy => MarketSystem.PlanMarketOccupancy(MarketStallWorld, actor, choice),
            MarketActionFamily.Stock => MarketSystem.PlanMarketStock(MarketStallWorld, actor, choice),
            _ => MarketSystem.PlanMarketExchange(MarketStallWorld, actor, choice),
        };
        return plan as TPlan;
    }

    private static class MarketActions
    {
        internal static readonly ActionDefinition<ShopBuyChoice, ShopBuyActionPlan> ShopBuy = new(
            DecodeReferenceChoice(ShopBuyChoice.Decode), static choice => choice.Encode(), ShopCapabilities,
            static (world, actor, choice, _) => world.worldSimulation.Buildings.SingleOrDefault(item => item.InstanceId == choice.BuildingId) is { } shop
                ? world.marketSystem.PlanShopBuy(world.MarketShopPlanningWorld, actor, shop, null, null) : null,
            static (world, plan, policy) => world.marketSystem.CheckShopBuy(world.MarketShopPlanningWorld, plan, policy),
            static (world, plan, _, _) => { world.marketSystem.ApplyCheckedShopBuy(world.MarketShopPlanningWorld, plan); return true; });

        internal static readonly ActionDefinition<ShopResponseChoice, ShopResponseActionPlan> ShopResponse = new(
            DecodeReferenceChoice(ShopResponseChoice.Decode), static choice => choice.Encode(), ShopCapabilities,
            static (world, actor, choice, _) => world.marketSystem.PlanShopResponse(world.MarketShopWorld, actor, choice.Cancel, choice.OfferId),
            static (world, plan, policy) => world.marketSystem.CheckShopResponse(world.MarketShopPlanningWorld, plan, policy),
            static (world, plan, _, _) => { world.marketSystem.ApplyCheckedShopResponse(world.MarketShopPlanningWorld, plan); return true; });

        internal static readonly ActionDefinition<DirectBarterChoice, DirectBarterActionPlan> Barter = new(
            DecodeReferenceChoice(DirectBarterChoice.Decode), static choice => choice.Encode(), PersonalBarterCapabilities,
            static (world, actor, choice, _) => MarketSystem.PlanDirectBarter(world.MarketBarterWorld, actor, choice, null, null),
            static (world, plan, policy) => MarketSystem.CheckDirectBarter(world.MarketBarterWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedDirectBarter(world.MarketBarterWorld, plan); return true; });

        private static ChoiceDecoder<MarketActionRequest> DecodeFamily(MarketActionFamily family) =>
            DecodeReferenceChoice(candidate => MarketActionRequest.Decode(candidate) is { } request && request.Family == family ? request : null);

        internal static readonly ActionDefinition<MarketActionRequest, MarketOccupancyActionPlan> Occupancy = new(
            DecodeFamily(MarketActionFamily.Occupancy), static request => request.Encode(), MarketSystem.MarketOccupancyCapabilities(false),
            static (world, actor, request, _) => world.PlanMarketRequest<MarketOccupancyActionPlan>(actor, request),
            static (world, plan, policy) => MarketSystem.CheckMarketOccupancy(world.MarketStallWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedMarketOccupancy(world.MarketStallWorld, plan); return true; },
            PlanCapabilities: static plan => MarketSystem.MarketOccupancyCapabilities(plan.Choice.Leave));

        internal static readonly ActionDefinition<MarketActionRequest, MarketStockActionPlan> Stock = new(
            DecodeFamily(MarketActionFamily.Stock), static request => request.Encode(), PersonalBarterCapabilities,
            static (world, actor, request, _) => world.PlanMarketRequest<MarketStockActionPlan>(actor, request),
            static (world, plan, policy) => world.CheckMarketStock(plan, policy),
            static (world, plan, _, _) => { world.ApplyCheckedMarketStock(plan); return true; },
            PlanCapabilities: MarketStockCapabilities);

        internal static readonly ActionDefinition<MarketActionRequest, MarketExchangeActionPlan> Exchange = new(
            DecodeFamily(MarketActionFamily.Exchange), static request => request.Encode(), MarketSystem.MarketExchangeCapabilities(MarketExchangeKind.Buy),
            static (world, actor, request, _) => world.PlanMarketRequest<MarketExchangeActionPlan>(actor, request),
            static (world, plan, policy) => MarketSystem.CheckMarketExchange(world.MarketStallWorld, plan, policy),
            static (world, plan, _, _) => { MarketSystem.ApplyCheckedMarketExchange(world.MarketStallWorld, plan); return true; },
            PlanCapabilities: static plan => MarketSystem.MarketExchangeCapabilities(plan.Choice.Kind));
    }
}
