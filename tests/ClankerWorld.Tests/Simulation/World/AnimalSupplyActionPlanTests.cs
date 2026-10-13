using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class AnimalPipelineTests
{
    [Theory]
    [InlineData("grain")]
    [InlineData("water_jug")]
    [InlineData("saddle")]
    public async Task SupplyPickupRetainsLoadAndFamilyAcrossRollbackReplayAndReload(string kind)
    {
        var (state, actor) = SupplyPlanFixture(kind);
        var chooser = new SupplyPlanChooser(actor, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new SupplyPlanChooser(actor, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsType<AnimalSupplyActionPlan>(chooser.Plan);
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == actor).ActionPlan);
        Assert.Equal("supply-plan-stock", plan.Choice.StockId);
        Assert.Equal(kind == "grain" ? 2 : 1, plan.Quantity);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var trip = Assert.Single(world.ExportState().AnimalWorld!.SupplyTrips);
        var load = world.Society.Inventory.GetLot(trip.LotId);
        Assert.Equal((plan.OwnerId, plan.Quantity, actor), (load.OwnerId, load.Quantity, load.CarrierId));
        Assert.Contains(new InventoryHold(InventoryHoldPurpose.SupplyTrip, actor), load.Holds!);
        if (kind == "grain") Assert.Equal(2, world.Society.Inventory.GetLot(plan.Choice.StockId).Quantity);
        if (kind == "water_jug")
        {
            Assert.Equal(2, world.Society.Inventory.GetLot("supply-plan-water").Quantity);
            Assert.Equal(actor, world.Society.Inventory.GetLot("supply-plan-water").CarrierId);
            Assert.Equal(2, plan.Family!.Count);
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData("grain")]
    [InlineData("water_jug")]
    [InlineData("saddle")]
    public async Task SupplySelectionSurvivesAnotherLotAndRefusesItsOwnReservation(string kind)
    {
        var (state, actor) = SupplyPlanFixture(kind);
        var chooser = new SupplyPlanChooser(actor, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalSupplyActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-animal-supply", kind, plan.OwnerId, 1,
            groundPosition: new(plan.Pickup.X, plan.Pickup.Y));
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        var claimedStock = kind == "water_jug" ? "supply-plan-water" : plan.Choice.StockId;
        inventory = InventoryAuthority.Reserve(inventory, "supply-plan-other-work", plan.OwnerId, claimedStock,
            inventory.GetLot(claimedStock).Quantity, "other_work", long.MaxValue);
        using var reserved = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(reserved.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, reserved.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reserved.ExportState()));
    }

    [Fact]
    public async Task ChangedWaterFamilyIsRefusedBeforePickup()
    {
        var (state, actor) = SupplyPlanFixture("water_jug");
        var chooser = new SupplyPlanChooser(actor, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<AnimalSupplyActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.Reserve(checkpoint.Society.Society.Inventory, "supply-plan-drink", plan.OwnerId,
            "supply-plan-water", 1, "drink", long.MaxValue);
        inventory = InventoryAuthority.ConsumeReservation(inventory, "supply-plan-drink");
        using var changed = WorldRuntime.Restore(checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } });
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(1, changed.Society.Inventory.GetLot("supply-plan-water").Quantity);
        Assert.Empty(changed.ExportState().AnimalWorld!.SupplyTrips);
    }

    private static (WorldRuntimeState State, string Actor) SupplyPlanFixture(string kind)
    {
        var (state, actor, home, yard) = CreateYard("animal-supply-plans");
        var position = state.Agents.Single(person => person.AgentId == actor).Position;
        var day = state.WorldSystems!.Config.TicksPerDay;
        var species = kind == "saddle" ? "horse" : "sheep";
        var animal = new AnimalState("supply-plan-animal", "Férn 🌿", species, "female", -(long)AnimalRules.Definition(species).AdultDays * day,
            yard.Position, "household:" + home, home, yard.InstanceId);
        var inventory = state.Society.Society.Inventory with { Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.OwnerId != actor).ToArray() };
        inventory = InventoryAuthority.AddLot(inventory, "supply-plan-stock", kind, home, kind == "grain" ? 4 : 1,
            groundPosition: new(position.X, position.Y));
        if (kind == "water_jug") inventory = InventoryAuthority.AddLot(inventory, "supply-plan-water", "fresh_water", home, 2, containerLotId: "supply-plan-stock");
        return (At(state, actor, position, inventory, [animal]), actor);
    }

    private sealed class SupplyPlanChooser(string actor, bool choose) : IDecisionProvider
    {
        public AnimalSupplyActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is AnimalSupplyActionPlan plan && plan.Choice.StockId == "supply-plan-stock" && plan.Choice.AnimalId == "supply-plan-animal") : null;
            if (candidate is not null) Plan = Assert.IsType<AnimalSupplyActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
