using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ToolMakingRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWaitingFinishedToolQuoteKeepsItsLotsAcrossNativeInventoryProcessing(bool foodPayment)
    {
        var (state, buyer, seller, shop) = Prepared();
        using var placing = Restore(state, buyer, seller, new RequestChoices("tool_request_place:"), new RequestChoices());
        await Until(placing, world => world.ToolMakingRequests.Count == 1, 96);
        using var working = Restore(Roundtrip(placing), buyer, seller, new RequestChoices(),
            new RequestChoices("tool_request_accept:", "tool_request_work:"));
        await Until(working, world => Assert.Single(world.ToolMakingRequests).Status == ToolMakingRequestStatus.Ready, 160);
        state = Roundtrip(working);
        if (foodPayment)
        {
            var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "quote-food-payment", "food", buyer, 3);
            state = state with
            {
                Society = state.Society with { Society = state.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
            };
        }
        var provider = new WaitingToolCollectionProvider();
        using var world = WorldRuntime.Restore(state, actor => actor == buyer ? provider : new RequestChoices());
        world.SubmitInstruction(new("held-tool-quote-guidance", "owner:test", buyer,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var plan = await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var quote = Assert.IsType<PlannedShopQuote>(plan.Quote);
            Assert.Equal(foodPayment ? "food" : "wood", quote.Payment.ItemKind);
            Assert.Empty(world.Society.Inventory.Offers);
            for (var tick = 0; tick < 2; tick++)
                Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var goods = world.Society.Inventory.GetLot(quote.Goods.Id);
            var payment = world.Society.Inventory.GetLot(quote.Payment.Id);
            Assert.True(goods.LastProcessedTick > quote.Goods.LastProcessedTick);
            Assert.True(payment.LastProcessedTick > quote.Payment.LastProcessedTick);
            Assert.Equal((quote.Goods.Id, quote.Goods.OwnerId, quote.Goods.ItemKind, quote.Goods.Quantity),
                (goods.Id, goods.OwnerId, goods.ItemKind, goods.Quantity));
            Assert.Equal((quote.Payment.Id, quote.Payment.OwnerId, quote.Payment.ItemKind, quote.Payment.Quantity),
                (payment.Id, payment.OwnerId, payment.ItemKind, payment.Quantity));
            if (foodPayment) Assert.True(payment.FreshnessBasisPoints < quote.Payment.FreshnessBasisPoints);
            Assert.True(world.CheckActionPlan(plan).Allowed);

            // Another usable lot of the same kind cannot replace the selected payment.
            var inventory = InventoryAuthority.Transfer(world.Society.Inventory, "move-selected-payment", buyer,
                shop.HouseholdId!, payment.Id, payment.Quantity, "put the selected payment away", shop.InstanceId);
            inventory = InventoryAuthority.AddLot(inventory, "replacement-quote-payment", payment.ItemKind, buyer, payment.Quantity);
            var changedState = world.ExportState();
            using var changed = WorldRuntime.Restore(changedState with
            {
                Society = changedState.Society with
                { Society = changedState.Society.Society with { Inventory = StockHoldTestState.Custody(inventory) } },
            });
            var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
            Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
            Assert.Empty(changed.Society.Inventory.Offers);

            provider.Release.TrySetResult();
            bytes = WorldRuntimeCodec.Encode(world.ExportState());
            await GrownAgentHelperMemoryTests.WaitForRequests(world);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
            var step = await world.AdvanceOneTickNonBlockingAsync();
            var decision = Assert.Single(step.Decisions, item => item.AgentId == buyer);
            Assert.Same(plan, decision.ActionPlan);
            Assert.True(decision.Admission.Accepted);
            Assert.False(decision.Admission.FellBack);
            Assert.Null(decision.PlanRefusal);
            var offer = Assert.Single(world.Society.Inventory.Offers);
            Assert.Equal((quote.Goods.Id, quote.GoodsQuantity, quote.Payment.Id, quote.PaymentQuantity),
                (offer.FirstLotId, offer.FirstQuantity, offer.SecondLotId, offer.SecondQuantity));
            Assert.Equal(ToolMakingRequestStatus.Offered, Assert.Single(world.ToolMakingRequests).Status);
            _ = Roundtrip(world);
        }
        finally { provider.Release.TrySetResult(); }
    }

    [Fact]
    public async Task AFinishedToolQuoteRefusesPaymentNewlyPromisedByANativeAnimalSupplyPickup()
    {
        var (state, buyer, seller, shop) = Prepared();
        using var placing = Restore(state, buyer, seller, new RequestChoices("tool_request_place:"), new RequestChoices());
        await Until(placing, world => world.ToolMakingRequests.Count == 1, 96);
        using var working = Restore(Roundtrip(placing), buyer, seller, new RequestChoices(),
            new RequestChoices("tool_request_accept:", "tool_request_work:"));
        await Until(working, world => Assert.Single(world.ToolMakingRequests).Status == ToolMakingRequestStatus.Ready, 160);
        state = Roundtrip(working);
        var home = state.Society.Society.GetAgent(buyer).HouseholdId!;
        var position = state.Agents.Single(person => person.AgentId == buyer).Position;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "quote-yard-wood", "wood", home, 8,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "quote-yard-rope", "rope", home, 2,
            groundPosition: new(position.X, position.Y));
        inventory = InventoryAuthority.AddLot(inventory, "quoted-greens", "cultivated_greens", buyer, 2);
        using var building = WorldRuntime.Restore(state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
        }, _ => new RequestChoices());
        var definition = building.WorldContent.Buildings.Single(item => item.Tags.Contains("animal-yard"));
        foreach (var tile in state.Map.Tiles.OrderBy(tile => state.Map.FootDistance(tile.Position, position)))
            if (building.PlaceBuilding("quote-payment-yard", definition.CanonicalId, tile.Position, home).Applied) break;
        var yard = Assert.Single(building.WorldSimulation.Buildings, item => item.InstanceId == "quote-payment-yard");
        state = Roundtrip(building);
        var sheep = new AnimalState("quote-payment-sheep", "Quote payment sheep", "sheep", "female",
            -(long)AnimalRules.Definition("sheep").AdultDays * state.WorldSystems!.Config.TicksPerDay,
            yard.Position, "household:" + home, home, yard.InstanceId);
        state = state with { AnimalWorld = state.AnimalWorld! with { Animals = state.AnimalWorld.Animals.Append(sheep).ToArray() } };
        var provider = new WaitingToolCollectionProvider();
        using var world = WorldRuntime.Restore(state, actor => actor == buyer ? provider : new RequestChoices());
        world.SubmitInstruction(new("held-greens-quote-guidance", "owner:test", buyer,
            OwnerInstructionKind.Suggestion, "Think about your next task."));
        try
        {
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
            var plan = await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var quote = Assert.IsType<PlannedShopQuote>(plan.Quote);
            Assert.Equal("quoted-greens", quote.Payment.Id);
            var supplier = new QuotedPaymentSupplyChooser(quote.Payment.Id);
            using var supplied = WorldRuntime.Restore(world.ExportState(), actor => actor == buyer ? supplier : new RequestChoices());
            supplied.SubmitInstruction(new("quoted-feed-guidance", "owner:test", buyer,
                OwnerInstructionKind.Suggestion, "Think about your next task."));
            Assert.True((await supplied.AdvanceOneTickAsync()).Advanced);
            var pickup = Assert.IsType<AnimalSupplyActionPlan>(supplier.Plan);
            Assert.True(pickup.Carried);
            Assert.Equal(quote.Payment.Quantity, pickup.Quantity);
            Assert.Contains(supplied.ExportState().AnimalWorld!.SupplyTrips, trip => trip.ActorId == buyer && trip.LotId == quote.Payment.Id);
            var held = supplied.Society.Inventory.GetLot(quote.Payment.Id);
            Assert.Equal((quote.Payment.Id, quote.Payment.OwnerId, quote.Payment.ItemKind, quote.Payment.Quantity),
                (held.Id, held.OwnerId, held.ItemKind, held.Quantity));
            Assert.Contains(new InventoryHold(InventoryHoldPurpose.SupplyTrip, buyer), held.Holds!);
            using var fresh = WorldRuntime.Restore(supplied.ExportState(), actor => actor == buyer
                ? new ToolCollectionChooser(buyer) : new RequestChoices());
            fresh.SubmitInstruction(new("held-stock-quote-guidance", "owner:test", buyer,
                OwnerInstructionKind.Suggestion, "Think about your next task."));
            Assert.True((await fresh.AdvanceOneTickAsync()).Advanced);
            Assert.Empty(fresh.Society.Inventory.Offers);
            var bytes = WorldRuntimeCodec.Encode(supplied.ExportState());
            var check = supplied.CheckActionPlan(plan);
            Assert.False(check.Allowed);
            Assert.Equal(ActionRefusalReason.StockUnavailable, check.Refusal!.Reason);
            Assert.Equal(bytes, WorldRuntimeCodec.Encode(supplied.ExportState()));
            Assert.Empty(supplied.Society.Inventory.Offers);
            _ = Roundtrip(supplied);
        }
        finally { provider.Release.TrySetResult(); }
    }

    private sealed class QuotedPaymentSupplyChooser(string lot) : IDecisionProvider
    {
        public AnimalSupplyActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is AnimalSupplyActionPlan plan &&
                plan.Choice.StockId == lot && plan.Carried && plan.Quantity == 2);
            if (candidate is not null) Plan = Assert.IsType<AnimalSupplyActionPlan>(candidate.ActionPlan);
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                request.ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }

    private sealed class WaitingToolCollectionProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public TaskCompletionSource<ToolRequestCollectionActionPlan> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = Assert.Single(request.Observation.Candidates, item => item.ActionPlan is ToolRequestCollectionActionPlan { Quote: not null });
            Started.TrySetResult(Assert.IsType<ToolRequestCollectionActionPlan>(candidate.ActionPlan));
            await Release.Task.WaitAsync(cancellationToken);
            return new(request.RequestId, request.Observation.AgentId, Kind, request.ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                candidate.Id, 1, new Dictionary<string, double> { [candidate.Id] = 1 });
        }
    }
}
