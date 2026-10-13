using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(AnimalAgreementKind.GrantCare)]
    [InlineData(AnimalAgreementKind.GrantRide)]
    [InlineData(AnimalAgreementKind.OfferGift)]
    [InlineData(AnimalAgreementKind.OfferSale)]
    public async Task AnimalAgreementKeepsRecipientYardAndPaymentThroughRollbackReplayAndReload(AnimalAgreementKind kind)
    {
        var (state, seller, buyer, _) = AnimalAgreementFixture(false, false, false);
        var chooser = new AgreementPlanChooser(seller, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AgreementPlanChooser(seller, kind, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsType<AnimalAgreementActionPlan>(chooser.Plan);
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == seller).ActionPlan);
        Assert.Equal(buyer, plan.Choice.OtherId);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var animal = world.Animals.Single();
        if (kind == AnimalAgreementKind.GrantCare) Assert.Contains(buyer, animal.CarePermissions);
        else if (kind == AnimalAgreementKind.GrantRide) Assert.Contains(buyer, animal.RidingPermissions);
        else
        {
            var offer = Assert.Single(world.ExportState().AnimalWorld!.Offers);
            Assert.Equal(plan.ReceivingYardId, offer.ReceivingYardId);
            Assert.Equal(kind == AnimalAgreementKind.OfferSale ? plan.Choice.StockId : null, offer.PaymentLotId);
            Assert.Equal(kind == AnimalAgreementKind.OfferSale ? 1 : 0, offer.PaymentQuantity);
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AClaimOnTheSelectedPaymentDoesNotSubstituteAnotherLot()
    {
        var (state, seller, buyer, _) = AnimalAgreementFixture(false, false, false);
        var chooser = new AgreementPlanChooser(seller, AnimalAgreementKind.OfferSale, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalAgreementActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-animal-payment", plan.PaymentKind!, buyer, 2);
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "agreement-plan-other-work", buyer, plan.Choice.StockId!,
            inventory.GetLot(plan.Choice.StockId!).Quantity, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
    }

    [Theory]
    [InlineData(AnimalAgreementKind.Accept)]
    [InlineData(AnimalAgreementKind.Decline)]
    public async Task AnimalReplyKeepsTheExactNativeOfferAcrossReplayAndReload(AnimalAgreementKind kind)
    {
        var (state, seller, buyer, _) = AnimalAgreementFixture(false, false, false);
        using (var offered = WorldRuntime.Restore(state, _ => new AgreementPlanChooser(seller, AnimalAgreementKind.OfferSale, true)))
        {
            Assert.True((await offered.AdvanceOneTickAsync()).Advanced);
            Assert.Single(offered.ExportState().AnimalWorld!.Offers);
            state = offered.ExportState() with { Agents = offered.Agents.Select(person => person with { LastDecisionContext = null }).ToArray() };
        }
        var offer = state.AnimalWorld!.Offers.Single();
        var chooser = new AgreementPlanChooser(buyer, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new AgreementPlanChooser(buyer, kind, true));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(offer, Assert.IsType<AnimalAgreementActionPlan>(chooser.Plan).Offer);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Empty(world.ExportState().AnimalWorld!.Offers);
        Assert.Equal(kind == AnimalAgreementKind.Accept ? offer.ReceivingHouseholdId : state.AnimalWorld.Animals.Single().HouseholdId,
            world.Animals.Single().HouseholdId);
        Assert.Equal(kind == AnimalAgreementKind.Accept ? 1 : 2, world.Society.Inventory.GetLot("agreement-payment").Quantity);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    private sealed class AgreementPlanChooser(string actor, AnimalAgreementKind kind, bool choose) : IDecisionProvider
    {
        public AnimalAgreementActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is AnimalAgreementActionPlan plan && plan.Choice.Kind == kind) : null;
            if (candidate is not null) Plan = Assert.IsType<AnimalAgreementActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
