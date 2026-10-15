using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class RestaurantShopPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUrgentCustomersPersonalQuoteKeepsItsShopWhenAnOrderIsInterrupted(bool ordered)
    {
        var fixture = CreateFixture("porridge", kitchenReady: true);
        using var cooking = WorldRuntime.Restore(fixture.State, id => id == fixture.Cook
            ? fixture.CookProvider : new PipelineProvider("safe_idle"));
        await AdvanceUntil(cooking, () => cooking.WorldSimulation.ProductionJobs.Any(job =>
            job.RecipeId == fixture.Recipe.CanonicalId && job.State == WorldProductionJobState.Completed), limit: 80);
        var state = cooking.ExportState();
        var cooked = Assert.Single(cooking.Society.Inventory.Lots, lot => lot.ItemKind == "porridge");
        Assert.Equal((Beta, fixture.Restaurant.InstanceId, 2), (cooked.OwnerId, cooked.StorageBuildingId, cooked.Quantity));
        var definition = state.WorldContent!.Buildings.Where(item => item.Tags.Contains("store"))
            .OrderBy(item => item.Width * item.Height).First();
        var inventory = state.Society.Society.Inventory;
        foreach (var cost in definition.BuildCosts)
            inventory = InventoryAuthority.AddLot(inventory, "urgent-choice-build-" + cost.ResourceId,
                cost.ResourceId, Beta, cost.Amount, storageBuildingId: fixture.House.InstanceId);
        GridPoint? customerSite = null;
        const string otherShop = "a-urgent-choice-store";
        using (var placing = WorldRuntime.Restore(WithInventory(state, inventory), _ => new PipelineProvider("safe_idle")))
        {
            var occupied = state.WorldSimulation!.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
                state.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId), building))
                .Concat(state.Agents.Where(person => person.AgentId != fixture.Customer).Select(person => person.Position)).ToHashSet();
            foreach (var site in state.Map.Tiles.Select(tile => tile.Position)
                         .Where(point => state.Map.FootDistance(point, fixture.Restaurant.Position) == 2))
            {
                var footprint = (from y in Enumerable.Range(0, definition.Height)
                                 from x in Enumerable.Range(0, definition.Width)
                                 select new GridPoint(site.X + x, site.Y + y)).ToHashSet();
                var common = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.IsPassable(point) &&
                    state.Map.FootDistance(point, site) == 1 && state.Map.FootDistance(point, fixture.Restaurant.Position) == 1 &&
                    !occupied.Contains(point) && !footprint.Contains(point)).Cast<GridPoint?>().FirstOrDefault();
                if (common is null || !placing.PlaceBuilding(otherShop, definition.CanonicalId, site, Beta).Applied) continue;
                customerSite = common;
                break;
            }
            Assert.NotNull(customerSite);
            placing.Validate();
            state = placing.ExportState();
        }
        inventory = InventoryAuthority.Relocate(state.Society.Society.Inventory, "stock-urgent-choice-store",
            "restaurant-raw-berries", Beta, 2, storageBuildingId: otherShop);
        var foodSources = state.Map.Resources.Where(resource => resource.Kind is "food" or "fruit")
            .Select(resource => resource.Id).ToHashSet();
        state = WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == fixture.Customer ? person with
            {
                Position = customerSite!.Value, FullnessBasisPoints = 1_500, LastDecisionContext = null,
            } : person).ToArray(),
            Resources = state.Resources.Select(resource => foodSources.Contains(resource.ResourceId)
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource => foodSources.Contains(resource.Id)
                        ? resource with { Quantity = 0, State = EcologyResourceState.Depleted, NextRegenerationDay = 100 }
                        : resource).ToArray(),
                },
            },
        };
        var choice = new PersonalRestaurantChoice(fixture.Restaurant.InstanceId);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == fixture.Customer ? choice : new PipelineProvider("safe_idle"));
        var instruction = ordered ? world.SubmitInstruction(new("urgent-personal-shop", "owner:test", fixture.Customer,
            OwnerInstructionKind.Order, "keep gathering wood")) : null;
        var result = await world.AdvanceOneTickAsync();
        Assert.True(result.Advanced);
        var decision = Assert.Single(result.Decisions, item => item.AgentId == fixture.Customer);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        Assert.Null(decision.PlanRefusal);
        Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
        var plan = Assert.IsType<ShopBuyActionPlan>(decision.ActionPlan);
        Assert.Equal((ShopBuyKind.Quote, fixture.Restaurant.InstanceId), (plan.Kind, plan.BuildingId));
        Assert.Contains(choice.Offered, candidate => candidate.Id == "shop_buy:" + otherShop &&
            candidate.ActionPlan is ShopBuyActionPlan { Kind: ShopBuyKind.Quote });
        if (instruction is not null)
            Assert.Equal(instruction.InstructionId, decision.Admission.Intention.OperativeOrderInstructionId);
        var sale = Assert.Single(world.ShopTrades, trade => trade.BuyerId == fixture.Customer);
        Assert.Equal(plan.BuildingId, sale.BuildingInstanceId);
        var offer = world.Society.Inventory.GetOffer(sale.OfferId);
        Assert.Equal((plan.Quote!.Goods.Id, plan.Quote.GoodsQuantity, plan.Quote.Payment.Id, plan.Quote.PaymentQuantity),
            (offer.FirstLotId, offer.FirstQuantity, offer.SecondLotId, offer.SecondQuantity));
        Assert.Equal(DirectBarterState.Open, offer.State);
        Assert.Equal([fixture.Customer], offer.AcceptedBy);
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private sealed class PersonalRestaurantChoice(string shop) : IDecisionProvider
    {
        public IReadOnlyList<CognitionCandidate> Offered { get; private set; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Offered = request.Observation.Candidates;
            var selected = Offered.Single(candidate => candidate.Id == "shop_buy:" + shop);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1,
                Offered.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }

    // Existing meal tests do not exercise the order emergency filter. This
    // compares real cooking, payment and eating with only the order changed.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UrgentCustomerCanBuyCookedRestaurantFoodWithoutCancellingAnOrder(bool ordered)
    {
        var fixture = CreateFixture("porridge", kitchenReady: true);
        using var cooking = WorldRuntime.Restore(fixture.State, id => id == fixture.Cook
            ? fixture.CookProvider : new PipelineProvider("safe_idle"));
        await AdvanceUntil(cooking, () => cooking.WorldSimulation.ProductionJobs.Any(job =>
            job.RecipeId == fixture.Recipe.CanonicalId && job.State == WorldProductionJobState.Completed), limit: 80);
        var initial = cooking.ExportState();
        Assert.Equal(2, Assert.Single(cooking.Society.Inventory.Lots, lot => lot.ItemKind == "porridge").Quantity);
        var foodSources = initial.Map.Resources.Where(resource => resource.Kind is "food" or "fruit")
            .Select(resource => resource.Id).ToHashSet();
        initial = initial with
        {
            Agents = initial.Agents.Select(person => person.AgentId == fixture.Customer
                ? person with { FullnessBasisPoints = 1_500, LastDecisionContext = null } : person).ToArray(),
            Resources = initial.Resources.Select(resource => foodSources.Contains(resource.ResourceId)
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = initial.WorldSystems! with
            {
                Ecology = initial.WorldSystems.Ecology with
                {
                    Resources = initial.WorldSystems.Ecology.Resources.Select(resource => foodSources.Contains(resource.Id)
                        ? resource with { Quantity = 0, State = EcologyResourceState.Depleted, NextRegenerationDay = 100 }
                        : resource).ToArray(),
                },
            },
        };
        fixture = fixture with { State = initial };
        using var world = Restore(fixture);
        var receipt = ordered ? world.SubmitInstruction(new("restaurant-order-survival", "owner:test",
            fixture.Customer, OwnerInstructionKind.Order, "keep gathering wood")) : null;
        await Advance(world, 24);
        var sale = Assert.Single(world.ShopTrades, trade => trade.BuyerId == fixture.Customer);
        Assert.Equal("porridge", sale.GoodsKind);
        Assert.Equal(DirectBarterState.Settled, world.Society.Inventory.GetOffer(sale.OfferId).State);
        var customer = world.Agents.Single(person => person.AgentId == fixture.Customer);
        Assert.Equal("porridge", customer.Survival!.LastMealKind);
        Assert.True(customer.FullnessBasisPoints > 1_500);
        var payment = world.Society.Inventory.GetLot("restaurant-customer-payment");
        Assert.Equal((Beta, fixture.Restaurant.InstanceId, 1), (payment.OwnerId, payment.StorageBuildingId, payment.Quantity));
        if (receipt is not null)
        {
            var saved = world.ExportState();
            var order = Assert.Single(saved.Instructions!, item => item.InstructionId == receipt.InstructionId).Task!;
            Assert.True(order.RepeatUntilCancelled);
            Assert.DoesNotContain(receipt.InstructionId, saved.CompletedInstructionIds!);
            Assert.NotEqual("finished", order.Status);
            Assert.NotEqual("cancelled", order.Status);
        }
        await AssertStrictReplayAndDiscard(world, fixture);
    }
}
