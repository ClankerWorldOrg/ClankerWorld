using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(AnimalTripStep.Deliver)]
    [InlineData(AnimalTripStep.Work)]
    [InlineData(AnimalTripStep.Release)]
    public async Task RetainedTripKeepsItsLoadAndStepThroughRollbackReplayAndReload(AnimalTripStep step)
    {
        var (state, actor) = TripPlanFixture(step);
        var chooser = new TripPlanChooser(actor, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var original = world.Society.Inventory.GetLot(state.AnimalWorld!.SupplyTrips.Single().LotId);
        var request = new GoodsRequest(GoodsUse.AnimalSupplyTrip, actor,
            GoodsOwners.One(original.OwnerId), GoodsKinds.One(original.ItemKind), Explain: true);
        Assert.Equal(original.Id, Assert.Single(world.FindGoods(request).Matches).Lot.Id);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new TripPlanChooser(actor, true));
        var result = await world.AdvanceOneTickAsync();
        Assert.True(result.Advanced);
        var plan = Assert.IsType<AnimalTripActionPlan>(chooser.Plan);
        Assert.Same(plan, Assert.Single(result.Decisions, item => item.AgentId == actor).ActionPlan);
        Assert.Equal(step, plan.Step);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Empty(world.ExportState().AnimalWorld!.SupplyTrips);
        var load = world.Society.Inventory.GetLot(plan.Trip.LotId);
        Assert.DoesNotContain(new InventoryHold(InventoryHoldPurpose.SupplyTrip, actor), load.Holds ?? []);
        if (step == AnimalTripStep.Deliver)
        {
            Assert.Equal(plan.Trip.YardId, load.StorageBuildingId);
            Assert.Equal(plan.HouseholdId, load.OwnerId);
            var supplied = Assert.Single(result.Events, item => item.Kind == "animal_yard_supplied");
            var payload = Assert.IsType<AnimalYardSuppliedEventPayload>(supplied.Payload);
            Assert.Equal((actor, plan.Trip.YardId), (payload.ActorId, payload.YardInstanceId));
        }
        else if (step == AnimalTripStep.Work)
        {
            Assert.Equal(1, load.Quantity);
            Assert.True(world.Animals.Single().CareUntilTick > world.WorldTick);
            Assert.Single(result.Events, item => item.Kind == "animal_cared");
        }
        else
        {
            Assert.Equal(3, load.Quantity);
            Assert.Equal((original.OwnerId, original.CarrierId, original.StorageBuildingId,
                    original.GroundPosition, original.ContainerLotId, original.Quantity),
                (load.OwnerId, load.CarrierId, load.StorageBuildingId,
                    load.GroundPosition, load.ContainerLotId, load.Quantity));
            var released = WorldRuntimeCodec.Encode(world.ExportState());
            var answer = world.FindGoods(request);
            Assert.Empty(answer.Matches);
            Assert.Contains((load.Id, GoodsReason.Reservation), answer.Excluded);
            Assert.Equal(released, WorldRuntimeCodec.Encode(world.ExportState()));
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AlreadyCompletedCareReleasesTheTripWithoutSpendingItsLoad()
    {
        var (state, actor) = TripPlanFixture(AnimalTripStep.Work);
        var animal = state.AnimalWorld!.Animals.Single();
        state = state with { AnimalWorld = state.AnimalWorld with { Animals = [animal with { CareUntilTick = state.Society.Society.WorldTick + state.WorldSystems!.Config.TicksPerDay }] } };
        var chooser = new TripPlanChooser(actor, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(AnimalTripStep.Release, Assert.IsType<AnimalTripActionPlan>(chooser.Plan).Step);
        Assert.Empty(world.ExportState().AnimalWorld!.SupplyTrips);
        Assert.Equal(3, world.Society.Inventory.GetLot("work-plan-feed").Quantity);
        Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "animal_cared");
        world.Validate();
    }

    [Fact]
    public async Task ClaimingPlannedWorkInputRefusesTheSameTripReadOnly()
    {
        var (state, actor) = TripPlanFixture(AnimalTripStep.Work);
        var chooser = new TripPlanChooser(actor, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalTripActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.Reserve(checkpoint.Society.Society.Inventory, "trip-plan-other-work", actor,
            "work-plan-water", 2, "other_work", long.MaxValue);
        using var changed = WorldRuntime.Restore(checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Single(changed.ExportState().AnimalWorld!.SupplyTrips);
    }

    private static (WorldRuntimeState State, string Actor) TripPlanFixture(AnimalTripStep step)
    {
        var (state, actor) = WorkPlanFixture(AnimalWorkKind.Care);
        var animal = state.AnimalWorld!.Animals.Single();
        var lotId = "work-plan-feed";
        var inventory = state.Society.Society.Inventory;
        if (step == AnimalTripStep.Release)
        {
            inventory = InventoryAuthority.Reserve(inventory, "trip-plan-no-water", actor, "work-plan-water", 2, "drink", long.MaxValue);
            inventory = InventoryAuthority.ConsumeReservation(inventory, "trip-plan-no-water");
        }
        if (step == AnimalTripStep.Deliver)
        {
            lotId = "trip-plan-yard-feed";
            inventory = InventoryAuthority.AddLot(inventory, lotId, "grain", animal.HouseholdId!, 3);
            inventory = InventoryAuthority.Relocate(inventory, "trip-plan-carry", lotId, animal.HouseholdId!, 3, carrierId: actor);
        }
        inventory = InventoryAuthority.HoldStock(inventory, lotId, new(InventoryHoldPurpose.SupplyTrip, actor));
        state = state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            AnimalWorld = state.AnimalWorld with
            {
                SupplyTrips = [new(actor, lotId, animal.YardId!, step == AnimalTripStep.Deliver ? null : animal.Id,
                step == AnimalTripStep.Deliver ? null : "care")]
            },
        };
        return (state, actor);
    }

    private sealed class TripPlanChooser(string actor, bool choose) : IDecisionProvider
    {
        public AnimalTripActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item => item.ActionPlan is AnimalTripActionPlan) : null;
            if (candidate is not null) Plan = Assert.IsType<AnimalTripActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
