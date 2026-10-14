using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class PrivateEquipmentActionPlanTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NativeEquippingKeepsTheSelectedUnitAndDisplacedItemAcrossRollbackReplayAndReload(bool carryAid, bool shared)
    {
        var (state, actor) = await Fixture(carryAid, shared);
        var choices = new Choices(actor, carryAid, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var quantity = world.Society.Inventory.Lots.Sum(lot => lot.Quantity);
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices(actor, carryAid, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 12 && !world.ExportState().Events.Any(item => item.Kind == "equipment_equipped"); tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && choices.Plan is not null && ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        var plan = Assert.IsType<PrivateEquipmentActionPlan>(choices.Plan);
        Assert.Equal("a-equipping-plan-stock", plan.StockId);
        var equipment = world.Agents.Single(person => person.AgentId == actor).Equipment!;
        var equipped = world.Society.Inventory.GetLot((carryAid ? equipment.CarryAidLotId : equipment.ClothingLotId)!);
        Assert.Equal((actor, plan.ItemKind, 1), (equipped.OwnerId, equipped.ItemKind, equipped.Quantity));
        Assert.Equal(1, world.Society.Inventory.GetLot(plan.StockId).Quantity);
        Assert.Equal((actor, 1), (world.Society.Inventory.GetLot("equipping-plan-old").OwnerId, world.Society.Inventory.GetLot("equipping-plan-old").Quantity));
        Assert.Equal(quantity, world.Society.Inventory.Lots.Sum(lot => lot.Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == "equipment_equipped");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewEquipmentDoesNotReplaceTheSelectionAndAClaimRefusesIt(bool carryAid)
    {
        var (state, actor) = await Fixture(carryAid, shared: false);
        var choices = new Choices(actor, carryAid, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<PrivateEquipmentActionPlan>(choices.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-equipping-stock", carryAid ? "leather_sack" : plan.ItemKind, actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "equipping-plan-other-work", actor, plan.StockId, 2, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
        Assert.Equal("a-equipping-plan-stock", plan.StockId);
    }

    private static async Task<(WorldRuntimeState State, string Actor)> Fixture(bool carryAid, bool shared)
    {
        using var initial = NormalPathWorld.CreateGenerated("equipping-action-plan", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var house = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-house-a");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == house.HouseholdId).Id;
        var occupied = state.WorldSimulation.Buildings.SelectMany(building => WorldContentSimulationRules.Footprint(
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId), building)).ToHashSet();
        var position = state.Map.Tiles.Select(tile => tile.Position).Where(point => !occupied.Contains(point) &&
                state.Map.IsPassable(point) && state.Map.IsReachableOnFoot(house.Position, point) &&
                state.Agents.All(person => person.Position != point))
            .OrderBy(point => state.Map.FootDistance(house.Position, point)).First();
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), "equipping-plan-old",
            carryAid ? "basket" : "clothing", actor, 1);
        if (!carryAid) inventory = InventoryAuthority.WearSingleUnit(inventory, "equipping-plan-old", 9_000);
        inventory = InventoryAuthority.AddLot(inventory, "a-equipping-plan-stock", carryAid ? "sack" : "clothing",
            shared ? house.HouseholdId! : actor, 2, storageBuildingId: shared ? house.InstanceId : null);
        if (carryAid && shared) inventory = InventoryAuthority.AddLot(inventory, "equipping-plan-full-cargo", "stone", actor, 16);
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, carryAid ? WeatherKind.Clear : WeatherKind.Snow), inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? position : person.Position,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(),
                Equipment = person.AgentId == actor ? carryAid ? new(CarryAidLotId: "equipping-plan-old") : new(ClothingLotId: "equipping-plan-old") : person.Equipment,
            }).ToArray(),
        };
        return (state, actor);
    }

    private sealed class Choices(string actor, bool carryAid, bool choose) : IDecisionProvider
    {
        public PrivateEquipmentActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item => item.Id == (carryAid ? "equip_carry_aid" : "wear_clothing")) : null;
            if (candidate is not null) Plan = Assert.IsType<PrivateEquipmentActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
