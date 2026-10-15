using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class VesselActionPlanTests
{
    [Theory]
    [InlineData(VesselActionKind.CollectJug, "water_jug_collected")]
    [InlineData(VesselActionKind.FillJug, "water_jug_filled")]
    [InlineData(VesselActionKind.FillJug, "water_jug_filled", true)]
    [InlineData(VesselActionKind.ReturnJug, "water_jug_returned")]
    [InlineData(VesselActionKind.StoreFood, "food_stored_in_pot")]
    [InlineData(VesselActionKind.TakeFood, "food_taken_from_pot")]
    [InlineData(VesselActionKind.ReturnEmpty, "empty_vessel_picked_up")]
    public async Task NativeVesselActionsUseTheOfferedPlanAcrossRollbackReplayAndReload(VesselActionKind kind, string eventKind, bool longVesselId = false)
    {
        var (state, actor) = await Fixture(kind, longVesselId);
        var choices = new Choices(actor, kind, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices(actor, kind, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == eventKind); tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && choices.Plan is not null && ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        var plan = Assert.IsType<VesselActionPlan>(choices.Plan);
        Assert.Equal(longVesselId ? new string('v', DerivedId.MaximumLength) : "a-vessel-plan-root", plan.VesselId);
        Assert.Single(world.ExportState().Events, item => item.Kind == eventKind);
        var vessel = world.Society.Inventory.GetLot(plan.VesselId);
        if (kind == VesselActionKind.FillJug)
        {
            Assert.Equal(plan.Quantity, InventoryIndex.For(world.Society.Inventory).ContentsQuantity(vessel.Id));
            var water = Assert.Single(world.Society.Inventory.Lots, lot => lot.ContainerLotId == vessel.Id);
            Assert.InRange(water.Id.Length, 1, DerivedId.MaximumLength);
            Assert.Equal(vessel.Id, water.ContainerLotId);
        }
        else if (kind == VesselActionKind.ReturnJug)
            Assert.Equal((plan.HouseholdId, plan.HouseId, 4), (vessel.OwnerId, vessel.StorageBuildingId, InventoryIndex.For(world.Society.Inventory).ContentsQuantity(vessel.Id)));
        else if (kind == VesselActionKind.StoreFood)
            Assert.Equal(plan.Quantity, InventoryIndex.For(world.Society.Inventory).ContentsQuantity(vessel.Id));
        else if (kind == VesselActionKind.TakeFood)
            Assert.Equal(3 - plan.Quantity, InventoryIndex.For(world.Society.Inventory).ContentsQuantity(vessel.Id));
        else Assert.Equal(actor, vessel.OwnerId);
        if (kind == VesselActionKind.ReturnEmpty) Assert.Equal(plan.HouseId, vessel.DeliveryBuildingId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(VesselActionKind.ReturnJug)]
    [InlineData(VesselActionKind.StoreFood)]
    public async Task NewStockDoesNotReplaceTheSelectedFamilyOrFoodAndANewClaimRefusesIt(VesselActionKind kind)
    {
        var (state, actor) = await Fixture(kind);
        var choices = new Choices(actor, kind, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<VesselActionPlan>(choices.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-vessel-stock",
            kind == VesselActionKind.ReturnJug ? InventoryContainerRules.WaterJug : "food", actor, kind == VesselActionKind.ReturnJug ? 1 : 3);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "vessel-plan-other-work", actor,
            kind == VesselActionKind.ReturnJug ? "a-vessel-plan-water" : plan.Food!.StockId,
            kind == VesselActionKind.ReturnJug ? 1 : 2, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
        Assert.Equal("a-vessel-plan-root", plan.VesselId);
        if (kind == VesselActionKind.StoreFood) Assert.Equal("a-vessel-plan-food", plan.Food!.StockId);
    }

    private static async Task<(WorldRuntimeState State, string Actor)> Fixture(VesselActionKind kind, bool longVesselId = false)
    {
        using var initial = NormalPathWorld.CreateGenerated("vessel-action-plan", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == house.HouseholdId).Id;
        var pot = kind is VesselActionKind.StoreFood or VesselActionKind.TakeFood or VesselActionKind.ReturnEmpty;
        var carried = kind is VesselActionKind.FillJug or VesselActionKind.ReturnJug;
        var source = kind == VesselActionKind.ReturnEmpty
            ? state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-farmhouse") : house;
        var vesselId = longVesselId ? new string('v', DerivedId.MaximumLength) : "a-vessel-plan-root";
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), vesselId,
            pot ? InventoryContainerRules.StoragePot : InventoryContainerRules.WaterJug, carried ? actor : house.HouseholdId!, 1,
            storageBuildingId: carried ? null : source.InstanceId);
        if (kind == VesselActionKind.ReturnJug)
            inventory = InventoryAuthority.AddLot(inventory, "a-vessel-plan-water", InventoryContainerRules.FreshWater, actor, 4, containerLotId: vesselId);
        if (kind is VesselActionKind.StoreFood or VesselActionKind.TakeFood)
            inventory = InventoryAuthority.AddLot(inventory, "a-vessel-plan-food", "food", kind == VesselActionKind.StoreFood ? actor : house.HouseholdId!, 3,
                storageBuildingId: kind == VesselActionKind.TakeFood ? house.InstanceId : null,
                containerLotId: kind == VesselActionKind.TakeFood ? "a-vessel-plan-root" : null);
        var position = source.Position;
        if (kind == VesselActionKind.FillJug)
            position = state.Map.Tiles.Select(tile => tile.Position).Where(point => state.Map.HydrologyAt(point) == WaterKind.Land &&
                    state.Map.IsPassable(point) && state.Map.IsReachableOnFoot(house.Position, point) &&
                    state.Agents.All(person => person.AgentId == actor || person.Position != point) &&
                    new[] { new GridPoint(point.X - 1, point.Y), new GridPoint(point.X + 1, point.Y), new GridPoint(point.X, point.Y - 1), new GridPoint(point.X, point.Y + 1) }
                        .Any(neighbor => state.Map.Contains(neighbor) && state.Map.HydrologyAt(neighbor) is WaterKind.River or WaterKind.Lake))
                .OrderBy(point => state.Map.FootDistance(house.Position, point)).First();
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? position : person.Position,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = person.AgentId == actor && kind == VesselActionKind.TakeFood ? 3_000 : 10_000,
                Survival = new SurvivalCondition(),
            }).ToArray(),
        };
        return (state, actor);
    }

    private sealed class Choices(string actor, VesselActionKind kind, bool choose) : IDecisionProvider
    {
        public VesselActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item => item.ActionPlan is VesselActionPlan plan && plan.Choice.Kind == kind) : null;
            if (candidate is not null) Plan = Assert.IsType<VesselActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
