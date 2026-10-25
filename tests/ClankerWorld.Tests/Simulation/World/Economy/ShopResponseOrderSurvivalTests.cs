using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class RestaurantShopPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUrgentCustomersPersonalResponseSurvivesAnInterruptedOrder(bool ordered)
    {
        var fixture = CreateFixture("porridge", kitchenReady: true);
        WorldRuntimeState state;
        using (var offering = Restore(fixture))
        {
            await AdvanceUntil(offering, () => offering.ShopTrades.Any(trade => trade.BuyerId == fixture.Customer));
            Assert.Contains(offering.WorldSimulation.ProductionJobs, job =>
                job.RecipeId == fixture.Recipe.CanonicalId && job.State == WorldProductionJobState.Completed);
            state = offering.ExportState();
        }
        var trade = Assert.Single(state.ShopTrades!, item => item.BuyerId == fixture.Customer);
        var original = state.Society.Society.Inventory.GetOffer(trade.OfferId);
        Assert.Equal(DirectBarterState.Open, original.State);
        Assert.Equal("porridge", trade.GoodsKind);
        Assert.Equal([fixture.Customer], original.AcceptedBy);
        const string foodId = "response-order-personal-food";
        state = WithInventory(state, InventoryAuthority.AddLot(state.Society.Society.Inventory,
            foodId, "berries", fixture.Customer, 1)) with
        {
            Agents = state.Agents.Select(person => person.AgentId == fixture.Customer ? person with
            {
                FullnessBasisPoints = 1_500,
                LastDecisionContext = null,
            } : person).ToArray(),
        };
        var goods = state.Society.Society.Inventory.GetLot(original.FirstLotId);
        var payment = state.Society.Society.Inventory.GetLot(original.SecondLotId);
        var choice = new PersonalShopChoice("shop_continue:" + original.Id);
        using var world = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(state)),
            id => id == fixture.Customer ? choice : new PipelineProvider("safe_idle"));
        var instruction = ordered ? world.SubmitInstruction(new("urgent-personal-response", "owner:test", fixture.Customer,
            OwnerInstructionKind.Order, "keep gathering wood")) : null;
        var result = await world.AdvanceOneTickAsync();
        Assert.True(result.Advanced);
        var decision = Assert.Single(result.Decisions, item => item.AgentId == fixture.Customer);
        Assert.True(decision.Admission.Accepted);
        Assert.False(decision.Admission.FellBack);
        Assert.Null(decision.PlanRefusal);
        Assert.Equal(DecisionProviderKind.LargeLanguageModel, decision.Admission.Intention!.Provider);
        var plan = Assert.IsType<ShopResponseActionPlan>(decision.ActionPlan);
        Assert.Equal(fixture.Customer, plan.Actor);
        Assert.False(plan.Cancel);
        Assert.Equal(trade, plan.Trade);
        Assert.Equal(TownHearingProcedure.Digest(original), TownHearingProcedure.Digest(plan.Offer));
        Assert.Contains(choice.Offered, candidate => candidate.Id == "consume_food" && candidate.DeterministicPriority == 0);
        Assert.Contains(choice.Offered, candidate => candidate.Id == plan.CandidateId && candidate.DeterministicPriority == 12);
        if (instruction is not null)
        {
            Assert.Equal(instruction.InstructionId, decision.Admission.Intention.OperativeOrderInstructionId);
            var order = Assert.Single(world.ExportState().Instructions!, item => item.InstructionId == instruction.InstructionId).Task!;
            Assert.True(order.RepeatUntilCancelled);
            Assert.Equal("interrupted", order.Status);
            Assert.DoesNotContain(instruction.InstructionId, world.ExportState().CompletedInstructionIds!);
        }
        Assert.Equal(1, world.Society.Inventory.Lots.Where(lot => lot.Id == foodId).Sum(lot => lot.Quantity));
        var current = world.Society.Inventory.GetOffer(original.Id);
        Assert.Equal(TownHearingProcedure.Digest(original), TownHearingProcedure.Digest(current));
        Assert.Equal(DirectBarterState.Open, current.State);
        Assert.Equal([fixture.Customer], current.AcceptedBy);
        Assert.Equal(trade, Assert.Single(world.ShopTrades));
        Assert.Equal((goods.OwnerId, goods.StorageBuildingId, goods.Quantity),
            (world.Society.Inventory.GetLot(goods.Id).OwnerId, world.Society.Inventory.GetLot(goods.Id).StorageBuildingId,
                world.Society.Inventory.GetLot(goods.Id).Quantity));
        Assert.Equal((payment.OwnerId, payment.CarrierId, payment.Quantity),
            (world.Society.Inventory.GetLot(payment.Id).OwnerId, world.Society.Inventory.GetLot(payment.Id).CarrierId,
                world.Society.Inventory.GetLot(payment.Id).Quantity));
        Assert.All(new[] { DerivedId.Create(original.Id + ":first"), DerivedId.Create(original.Id + ":second") },
            id => Assert.Equal(InventoryReservationState.Reserved, world.Society.Inventory.GetReservation(id).State));
        world.Validate();
        var saved = WorldRuntimeCodec.Encode(world.ExportState());
        using var reload = WorldRuntime.Restore(WorldRuntimeCodec.Decode(saved));
        Assert.Equal(saved, WorldRuntimeCodec.Encode(reload.ExportState()));
    }

    private sealed class PersonalShopChoice(string candidateId) : IDecisionProvider
    {
        public IReadOnlyList<CognitionCandidate> Offered { get; private set; } = [];
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Offered = request.Observation.Candidates;
            var selected = Offered.Single(candidate => candidate.Id == candidateId);
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected.Id, 1,
                Offered.ToDictionary(candidate => candidate.Id, candidate => candidate.Id == selected.Id ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
