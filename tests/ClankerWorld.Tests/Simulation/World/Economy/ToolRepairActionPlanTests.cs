using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class ToolRepairActionPlanTests
{
    [Fact]
    public async Task RepairPreparationRefusesWarehouseStockAfterItsAbandonedTownIsResettled()
    {
        var (state, actor) = await Fixture(borrowed: false, needsWood: true);
        var warehouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var townId = warehouse.TownId!;
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.Id != "repair-plan-shared-wood").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "repair-plan-warehouse-wood", "wood", townId, 1,
            storageBuildingId: warehouse.InstanceId);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Towns = state.Towns!.Select(town => town.Id == townId ? town with
            {
                ResidentIds = [],
                Council = TownCouncilState.Create([]),
                Government = TownGovernmentState.Create(),
                AbandonedSinceTick = state.Society.Society.WorldTick,
            } : town).ToArray(),
        };
        var choices = new Choices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ToolRepairActionPlan>(choices.Plan);
        Assert.Equal("repair-plan-warehouse-wood", Assert.Single(plan.Inputs).StockId);
        Assert.True(world.CheckActionPlan(plan).Allowed);
        var checkpoint = world.ExportState();
        var settler = checkpoint.Society.Society.Agents.First(person => person.HouseholdId != plan.HouseholdId).Id;
        var resettled = checkpoint with
        {
            Towns = checkpoint.Towns!.Select(town => town.Id == townId ? town with
            {
                ResidentIds = [settler],
                Council = TownCouncilState.Create([settler]),
                AbandonedSinceTick = null,
            } : town).ToArray(),
        };
        using var changed = WorldRuntime.Restore(resettled);
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(checkpoint.Society.Society.Inventory.GetLot("repair-plan-warehouse-wood"),
            changed.Society.Inventory.GetLot("repair-plan-warehouse-wood"));
        changed.Validate();
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(changed.ExportState())));
        Assert.Equal(ActionRefusalReason.StockUnavailable, reopened.CheckActionPlan(plan).Refusal!.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepairPreparationKeepsItsSelectedStockOrGatheringToolAndRefusesANewClaim(bool gathering)
    {
        var (state, actor) = await Fixture(borrowed: false, needsWood: true, gathering: gathering);
        var choices = new Choices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ToolRepairActionPlan>(choices.Plan);
        var input = Assert.Single(plan.Inputs);
        Assert.Equal(gathering ? "repair-plan-harvest-axe" : "repair-plan-shared-wood", gathering ? input.Harvest!.ToolLotId : input.StockId);
        var checkpoint = world.ExportState();
        var inventory = gathering
            ? InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-better-repair-axe", "iron_axe", actor, 1)
            : InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-repair-stock", "wood", plan.HouseholdId, 1, storageBuildingId: plan.SmithId);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "repair-plan-preparation-claim", gathering ? actor : plan.HouseholdId,
            gathering ? input.Harvest!.ToolLotId! : input.StockId!, 1, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Same(input, Assert.Single(plan.Inputs));
        claimed.Validate();
    }

    [Fact]
    public async Task RepairKeepsItsSelectedCarriedMaterialAndRefusesANewClaim()
    {
        var (state, actor) = await Fixture(borrowed: false);
        var choices = new Choices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<ToolRepairActionPlan>(choices.Plan);
        Assert.Equal("repair-plan-iron", Assert.Single(plan.Materials).StockId);
        Assert.Empty(plan.Inputs);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-repair-iron", "iron", actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "repair-plan-other-work", actor, "repair-plan-iron", 3, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal("repair-plan-iron", Assert.Single(plan.Materials).StockId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(claimed.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(claimed.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRepairUsesTheSamePlanForOwnedAndBorrowedToolStacksAcrossRollbackReplayAndReload(bool borrowed)
    {
        var (state, actor) = await Fixture(borrowed);
        var choices = new Choices(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new Choices(actor, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "tool_repaired"); tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && choices.Plan is not null &&
                ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        var plan = Assert.IsType<ToolRepairActionPlan>(choices.Plan);
        var repaired = Assert.Single(world.Society.Inventory.Lots, lot => lot.ProvenanceLotId == plan.Choice.ToolId && lot.ConditionBasisPoints == 10_000);
        Assert.Equal((plan.ToolOwnerId, 1), (repaired.OwnerId, repaired.Quantity));
        Assert.Equal((1, 3_000), (world.Society.Inventory.GetLot(plan.Choice.ToolId).Quantity,
            world.Society.Inventory.GetLot(plan.Choice.ToolId).ConditionBasisPoints));
        Assert.Equal(2, world.Society.Inventory.GetLot("repair-plan-iron").Quantity);
        var reservation = Assert.Single(world.Society.Inventory.Reservations, item => item.Purpose == "equipment_repair");
        Assert.Equal((plan.ToolOwnerId, InventoryReservationState.Completed, 1),
            (reservation.OwnerId, reservation.State, reservation.Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == "tool_repaired");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private static async Task<(WorldRuntimeState State, string Actor)> Fixture(bool borrowed, bool needsWood = false, bool gathering = false)
    {
        using var initial = NormalPathWorld.CreateGenerated("tool-repair-plans", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var household = smith.HouseholdId!;
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == household).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory with { Lots = [], Reservations = [] },
            "repair-plan-target", needsWood ? "wooden_axe" : "iron_knife", borrowed ? household : actor,
            needsWood ? 1 : 2, conditionBasisPoints: 3_000);
        if (borrowed) inventory = InventoryAuthority.Relocate(inventory, "repair-plan-borrow", "repair-plan-target", household, 2, carrierId: actor);
        if (!needsWood) inventory = InventoryAuthority.AddLot(inventory, "repair-plan-iron", "iron", actor, 3);
        if (needsWood)
        {
            inventory = InventoryAuthority.AddLot(inventory, "repair-plan-harvest-axe", "wooden_axe", actor, 1);
            inventory = InventoryAuthority.AddLot(inventory, "repair-plan-basket", "basket", actor, 1);
            if (!gathering) inventory = InventoryAuthority.AddLot(inventory, "repair-plan-shared-wood", "wood", household, 1, storageBuildingId: smith.InstanceId);
        }
        if (gathering)
        {
            var loose = state.Map.Resources.Where(resource => resource.Kind == "wood" || resource.NaturalObjectKind == "fallen_wood")
                .Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
            state = state with
            {
                Resources = state.Resources.Select(resource => loose.Contains(resource.ResourceId)
                    ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
                WorldSystems = state.WorldSystems! with
                {
                    Ecology = state.WorldSystems.Ecology with
                    {
                        Resources = state.WorldSystems.Ecology.Resources.Select(resource => loose.Contains(resource.Id) && resource.Quantity > 0
                            ? EcologyRules.Harvest(resource, resource.Quantity).Resource! : resource).ToArray(),
                    },
                },
            };
        }
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = smith.Position,
                Equipment = needsWood ? new(CarryAidLotId: "repair-plan-basket") : null,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 9_000,
                Survival = new SurvivalCondition(),
            } : person).ToArray(),
        };
        return (state, actor);
    }

    private sealed class Choices(string actor, bool choose) : IDecisionProvider
    {
        public ToolRepairActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "repair_tool:repair-plan-target") : null;
            if (candidate is not null) Plan = Assert.IsType<ToolRepairActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
