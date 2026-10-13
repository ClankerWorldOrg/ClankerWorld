using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FoodActionPlanTests
{
    [Theory]
    [InlineData("consume_food", false)]
    [InlineData("collect_shared_food", false)]
    [InlineData("collect_shared_food", true)]
    public async Task NativeServingRetainsItsLotAcrossRollbackReplayAndReload(string action, bool pot)
    {
        var (state, actor) = await Fixture(action, pot);
        var choices = new Choices(actor, action, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices(actor, action, choose: true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        var plan = Assert.IsAssignableFrom<FoodActionPlan>(choices.Plan);
        Assert.Same(plan, Assert.Single(step.Decisions, item => item.AgentId == actor).ActionPlan);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.Equal(2, world.Society.Inventory.GetLot("food-plan-stock").Quantity);
        if (action == "consume_food")
        {
            Assert.Equal("food-plan-stock", Assert.IsType<ConsumeFoodActionPlan>(plan).StockId);
            Assert.Single(step.Events, item => item.Kind == "food_consumed" && item.Detail == actor);
        }
        else
        {
            var shared = Assert.IsType<SharedFoodActionPlan>(plan);
            Assert.Equal("food-plan-stock", shared.StockId);
            Assert.Equal(pot ? "food-plan-pot" : shared.StockId, shared.RootId);
            Assert.Single(step.Events, item => item.Kind == "household_food_collected" && item.Detail == $"{actor}:food-plan-stock:1");
            if (pot) Assert.Single(step.Events, item => item.Kind == "food_taken_from_pot");
        }
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData("consume_food", false)]
    [InlineData("collect_shared_food", false)]
    [InlineData("collect_shared_food", true)]
    public async Task NewServingDoesNotReplaceTheSelectionAndAClaimRefusesIt(string action, bool pot)
    {
        var (state, actor) = await Fixture(action, pot);
        var choices = new Choices(actor, action, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsAssignableFrom<FoodActionPlan>(choices.Plan);
        var owner = action == "consume_food" ? actor : FoodCapacityTestFixture.Household;
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-food-serving", "fruit", owner, 1,
            storageBuildingId: action == "consume_food" ? null : FoodCapacityTestFixture.House);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "food-plan-other-work", owner, "food-plan-stock", 3, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FoodResourceChoiceKeepsTheExactSourceAndRefusesDepletion(bool harvest)
    {
        var (state, actor, house) = await FoodCapacityTestFixture.Generated("food-resource-action-plan");
        var source = state.Map.Resources.Where(item => item.Kind == "food" &&
            state.Resources.Single(resource => resource.ResourceId == item.Id).State == ResourceState.Available &&
            state.Map.IsPassable(item.Position) && state.Agents.All(person => person.AgentId == actor || person.Position != item.Position))
            .OrderBy(item => state.Map.FootDistance(house, item.Position)).First();
        var position = harvest ? source.Position : state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsPassable(point) && state.Map.IsReachableOnFoot(house, point) &&
            state.Agents.All(person => person.AgentId == actor || person.Position != point) &&
            state.Map.Resources.Where(item => item.Kind is "food" or "fruit").All(item => state.Map.FootDistance(point, item.Position) > 1));
        state = FoodCapacityTestFixture.WithFullness(FoodCapacityTestFixture.WithInventory(state,
            FoodCapacityTestFixture.WithoutPersonalCargo(state, actor)), actor, 1_000) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = position, FullnessBasisPoints = 1_000 } : person).ToArray(),
        };
        var choices = new Choices(actor, harvest ? "harvest_food" : "seek_food", choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<FoodResourceActionPlan>(choices.Plan);
        if (harvest) Assert.Equal(source.Id, plan.Source.Id);
        Assert.Equal(4, plan.Quantity);
        Assert.Equal(0, plan.SeedQuantity);
        var checkpoint = world.ExportState();
        var resource = checkpoint.WorldSystems!.Ecology.GetResource(plan.Source.Id);
        using var depleted = WorldRuntime.Restore(checkpoint with
        {
            Resources = checkpoint.Resources.Select(item => item.ResourceId == plan.Source.Id
                ? item with { State = ResourceState.Depleted } : item).ToArray(),
            WorldSystems = checkpoint.WorldSystems with
            {
                Ecology = checkpoint.WorldSystems.Ecology with
                {
                    Resources = checkpoint.WorldSystems.Ecology.Resources.Select(item => item.Id == plan.Source.Id
                        ? EcologyRules.Harvest(resource, resource.Quantity).Resource! : item).ToArray(),
                },
            },
        });
        var bytes = WorldRuntimeCodec.Encode(depleted.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, depleted.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(depleted.ExportState()));
    }

    private static async Task<(WorldRuntimeState State, string Actor)> Fixture(string action, bool pot)
    {
        var (state, actor, _) = await FoodCapacityTestFixture.Generated("food-action-plan");
        var inventory = FoodCapacityTestFixture.WithoutPersonalCargo(state, actor);
        if (action != "consume_food") inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => lot.OwnerId != FoodCapacityTestFixture.Household || !InventoryContainerRules.IsFood(lot.ItemKind)).ToArray(),
        };
        var owner = action == "consume_food" ? actor : FoodCapacityTestFixture.Household;
        inventory = InventoryAuthority.AddLot(inventory, "food-plan-stock", "berries", owner, 3,
            storageBuildingId: action == "consume_food" ? null : FoodCapacityTestFixture.House);
        if (pot)
        {
            inventory = InventoryAuthority.AddLot(inventory, "food-plan-pot", InventoryContainerRules.StoragePot, owner, 1,
                storageBuildingId: FoodCapacityTestFixture.House);
            inventory = InventoryAuthority.PutIntoContainer(inventory, "food-plan-pot-fill", owner, "food-plan-pot", "food-plan-stock", 3);
        }
        state = FoodCapacityTestFixture.WithFullness(FoodCapacityTestFixture.WithInventory(state, inventory), actor, 1_000);
        return (state, actor);
    }

    private sealed class Choices(string actor, string action, bool choose) : IDecisionProvider
    {
        public FoodActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == action) : null;
            if (candidate is not null) Plan = Assert.IsAssignableFrom<FoodActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
