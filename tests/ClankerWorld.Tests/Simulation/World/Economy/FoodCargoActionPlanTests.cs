using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FoodCargoActionPlanTests(OrchardSeedCargoFixture fixture) : IClassFixture<OrchardSeedCargoFixture>
{
    [Fact]
    public async Task NewOrdinaryCargoDoesNotReplaceTheSelectedSeedOrItsReservationRelease()
    {
        var choices = new Choices(fixture.Actor, choose: false);
        using var world = WorldRuntime.Restore(fixture.CreateState(), _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<FoodCargoActionPlan>(choices.Plan);
        var selected = Assert.Single(plan.Cargo);
        Assert.Equal((1, 1), (selected.Quantity, selected.ReleaseQuantity));
        Assert.NotNull(selected.OrchardReservationId);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-spare-cargo", "stone", fixture.Actor, 1);
        using var changed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(changed.CheckActionPlan(plan).Allowed);
        Assert.Equal(selected, Assert.Single(plan.Cargo));
        inventory = InventoryAuthority.ReleaseReservationQuantity(inventory, selected.OrchardReservationId!, 1, "other_work");
        using var released = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, released.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(selected, Assert.Single(plan.Cargo));
        released.Validate();
    }

    [Fact]
    public async Task CapturedCargoAndReleaseSurviveRollbackReplayAndStrictReload()
    {
        var choices = new Choices(fixture.Actor, choose: true);
        using var world = WorldRuntime.Restore(fixture.CreateState(), _ => choices);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new Choices(fixture.Actor, choose: true));
        var carriedPlanAccepted = false;
        for (var tick = 0; tick < 60 && !world.ExportState().Events.Any(item => item.Kind == "spare_cargo_stored"); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            carriedPlanAccepted |= result.Decisions.Any(decision => decision.AgentId == fixture.Actor &&
                ReferenceEquals(decision.ActionPlan, choices.LastPlan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(carriedPlanAccepted);
        Assert.Single(world.ExportState().Events, item => item.Kind == "spare_cargo_stored");
        Assert.Equal(4, world.Society.Inventory.Lots.Where(lot => lot.ItemKind == "orchard_seed" && lot.OwnerId == fixture.Actor).Sum(lot => lot.Quantity));
        Assert.Single(world.Society.Inventory.Reservations, reservation => reservation.Purpose == "orchard_replanting" &&
            reservation.State == InventoryReservationState.Released);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private sealed class Choices(string actor, bool choose) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public ActionPlan? LastPlan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;

        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "make_room_for_food") : null;
            if (candidate is not null) { Plan ??= candidate.ActionPlan; LastPlan = candidate.ActionPlan; }
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
