using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData(AnimalWorkKind.Care)]
    [InlineData(AnimalWorkKind.Tame)]
    [InlineData(AnimalWorkKind.Saddle)]
    public async Task AnimalWorkKeepsExactInputsThroughRollbackReplayAndReload(AnimalWorkKind kind)
    {
        var (state, actor) = WorkPlanFixture(kind);
        var chooser = new WorkPlanChooser(actor, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new WorkPlanChooser(actor, kind, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsType<AnimalWorkActionPlan>(chooser.Plan);
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == actor).ActionPlan);
        Assert.Null(plan.SupplyPreparation);
        Assert.False(plan.ApproachOnly);
        Assert.Equal(kind == AnimalWorkKind.Saddle ? 1 : 2, plan.Inputs.Count);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var animal = Assert.Single(world.Animals);
        Assert.Single(step.Events, item => item.Kind == (kind == AnimalWorkKind.Care ? "animal_cared" : kind == AnimalWorkKind.Tame ? "animal_tamed" : "animal_saddled"));
        if (kind == AnimalWorkKind.Care) Assert.Equal(world.WorldTick + plan.CareTicks, animal.CareUntilTick);
        else if (kind == AnimalWorkKind.Tame)
        {
            Assert.Equal(8, plan.WorkTicks);
            Assert.Equal(plan.ReceivingHouseholdId, animal.HouseholdId);
            Assert.Equal(actor, animal.LeaderId);
            Assert.Null(animal.TamingWork);
        }
        else Assert.Equal("work-plan-saddle", animal.SaddleLotId);
        if (kind != AnimalWorkKind.Saddle) Assert.Equal(1, world.Society.Inventory.GetLot("work-plan-feed").Quantity);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(AnimalWorkKind.Care)]
    [InlineData(AnimalWorkKind.Tame)]
    [InlineData(AnimalWorkKind.Saddle)]
    public async Task AnimalWorkDoesNotSubstituteInputsWhenItsSelectedStockIsClaimed(AnimalWorkKind kind)
    {
        var (state, actor) = WorkPlanFixture(kind);
        var chooser = new WorkPlanChooser(actor, kind, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalWorkActionPlan>(chooser.Plan);
        var selected = plan.Inputs[0];
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-work-input", selected.ItemKind,
            selected.OwnerId, selected.Quantity, groundPosition: selected.Carried ? null : new(plan.Position.X, plan.Position.Y));
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "work-plan-other-work", selected.OwnerId, selected.StockId,
            inventory.GetLot(selected.StockId).Quantity, "other_work", long.MaxValue);
        using var reserved = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(reserved.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, reserved.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reserved.ExportState()));
    }

    [Fact]
    public async Task TamingWorkRefusesAChangedProgressCount()
    {
        var (state, actor) = WorkPlanFixture(AnimalWorkKind.Tame);
        var chooser = new WorkPlanChooser(actor, AnimalWorkKind.Tame, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalWorkActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        using var changed = WorldRuntime.Restore(checkpoint with
        { AnimalWorld = checkpoint.AnimalWorld! with { Animals = [checkpoint.AnimalWorld!.Animals.Single() with { TamingWork = new(actor, 6) }] } });
        Assert.Equal(ActionRefusalReason.TargetUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(3, changed.Society.Inventory.GetLot("work-plan-feed").Quantity);
    }

    private static (WorldRuntimeState State, string Actor) WorkPlanFixture(AnimalWorkKind kind)
    {
        var (state, actor, home, yard) = CreateYard("animal-work-plans");
        var day = state.WorldSystems!.Config.TicksPerDay;
        var species = kind == AnimalWorkKind.Saddle ? "horse" : "sheep";
        var animal = new AnimalState("work-plan-animal", "Férn 🌿", species, "female", -(long)AnimalRules.Definition(species).AdultDays * day,
            yard.Position, kind == AnimalWorkKind.Tame ? "wild-herd" : "household:" + home,
            kind == AnimalWorkKind.Tame ? null : home, kind == AnimalWorkKind.Tame ? null : yard.InstanceId,
            TamingWork: kind == AnimalWorkKind.Tame ? new(actor, 7) : null);
        var inventory = state.Society.Society.Inventory with { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        if (kind == AnimalWorkKind.Saddle) inventory = InventoryAuthority.AddLot(inventory, "work-plan-saddle", "saddle", home, 1,
            groundPosition: new(yard.Position.X, yard.Position.Y));
        else
        {
            inventory = InventoryAuthority.AddLot(inventory, "work-plan-feed", "grain", actor, 3);
            inventory = InventoryAuthority.AddLot(inventory, "work-plan-jug", "water_jug", actor, 1);
            inventory = InventoryAuthority.AddLot(inventory, "work-plan-water", "fresh_water", actor, 2, containerLotId: "work-plan-jug");
        }
        return (At(state, actor, yard.Position, inventory, [animal]), actor);
    }

    private sealed class WorkPlanChooser(string actor, AnimalWorkKind kind, bool choose) : IDecisionProvider
    {
        public AnimalWorkActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item =>
                item.ActionPlan is AnimalWorkActionPlan plan && plan.Choice.Kind == kind && plan.Choice.AnimalId == "work-plan-animal") : null;
            if (candidate is not null) Plan = Assert.IsType<AnimalWorkActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
