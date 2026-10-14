using ClankerWorld.Simulation;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class FieldActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedSeedPickupUsesTheSelectedLotAndItsOwnReplantingClaim(bool reserved)
    {
        var (state, actor, point) = await Fixture(FarmWorkKind.Plant);
        var field = Assert.Single(state.Fields!);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.Id != "field-plan-seed").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "field-plan-shared-seed", FarmFieldRules.GrainSeed,
            field.HouseholdId, 1, groundPosition: new(point.X, point.Y));
        if (reserved) inventory = InventoryAuthority.Reserve(inventory, "field-plan-replanting", field.HouseholdId,
            "field-plan-shared-seed", 1, "field_replanting", long.MaxValue);
        state = FarmFieldTests.WithInventory(state, inventory) with
        {
            Fields = [new(point, field.HouseholdId, FarmFieldStage.Harvested, FarmFieldRules.Grain,
                ReadyTick: 1, Tended: true, ReplantingReservationId: reserved ? Identity.Parse("field-plan-replanting") : null)],
        };
        var choices = new Choices(actor, FarmWorkKind.Plant, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var samePlan = false;
        for (var tick = 0; tick < 8 && world.Society.Inventory.GetLot("field-plan-shared-seed").OwnerId != actor; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && ReferenceEquals(decision.ActionPlan, choices.Plan));
        }
        var plan = Assert.IsType<FieldActionPlan>(choices.Plan);
        Assert.True(samePlan);
        Assert.Equal("field-plan-shared-seed", plan.Seed!.StockId);
        Assert.Equal(reserved ? "field-plan-replanting" : null, plan.Seed.ReplantingReservationId);
        Assert.Equal(actor, world.Society.Inventory.GetLot(plan.Seed.StockId).OwnerId);
        Assert.Null(Assert.Single(world.Fields).ReplantingReservationId);
        if (reserved) Assert.Equal(InventoryReservationState.Released, world.Society.Inventory.Reservations.Single(item => item.Id == "field-plan-replanting").State);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(FarmWorkKind.Till)]
    [InlineData(FarmWorkKind.Plant)]
    [InlineData(FarmWorkKind.Tend)]
    [InlineData(FarmWorkKind.Harvest)]
    public async Task NativeFieldWorkUsesTheOfferedPlanAcrossRollbackReplayAndReload(FarmWorkKind kind)
    {
        var (state, actor, _) = await Fixture(kind);
        var choices = new Choices(actor, kind, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices(actor, kind, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 8 && !world.Fields.Any(field => field.Work?.WorkerId == actor); tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && choices.Plan is not null &&
                ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        var plan = Assert.IsType<FieldActionPlan>(choices.Plan);
        var field = Assert.Single(world.Fields, field => field.Work?.WorkerId == actor);
        Assert.Equal((kind, plan.Choice.Position, plan.Hoe?.Work.ToolLotId, plan.Sickle?.Work.ToolLotId),
            (field.Work!.Kind, field.Position, field.Work.HoeLotId, field.Work.SickleLotId));
        if (kind == FarmWorkKind.Plant)
            Assert.Equal(plan.Seed!.StockId, world.Society.Inventory.Reservations.Single(item => item.Id == field.Work.SeedReservationId).LotId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(FarmWorkKind.Plant)]
    [InlineData(FarmWorkKind.Tend)]
    [InlineData(FarmWorkKind.Harvest)]
    public async Task NewStockOrToolsDoNotReplaceTheSelectionAndLosingItRefusesThePlan(FarmWorkKind kind)
    {
        var (state, actor, _) = await Fixture(kind);
        var choices = new Choices(actor, kind, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<FieldActionPlan>(choices.Plan);
        var selected = kind == FarmWorkKind.Plant ? plan.Seed!.StockId : kind == FarmWorkKind.Tend ? plan.Hoe!.Work.ToolLotId : plan.Sickle!.Work.ToolLotId;
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-field-selection",
            kind == FarmWorkKind.Plant ? FarmFieldRules.GrainSeed : kind == FarmWorkKind.Tend ? "iron_hoe" : "iron_sickle", actor, 1);
        using var added = WorldRuntime.Restore(FarmFieldTests.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "field-selection-lost", actor, selected, 1, "other_work", long.MaxValue);
        using var lost = WorldRuntime.Restore(FarmFieldTests.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(lost.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, lost.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(lost.ExportState()));
        Assert.Equal(selected, kind == FarmWorkKind.Plant ? plan.Seed!.StockId : kind == FarmWorkKind.Tend ? plan.Hoe!.Work.ToolLotId : plan.Sickle!.Work.ToolLotId);
    }

    private static async Task<(WorldRuntimeState State, string Actor, GridPoint Point)> Fixture(FarmWorkKind kind)
    {
        var (state, actor, household, point) = await FarmFieldTests.ReadyFarmer("field-plan-" + kind);
        state = FarmFieldTests.FeedFarmTownFromAvailableStock(state, household);
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "field-plan-seed", FarmFieldRules.GrainSeed, actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "field-plan-sickle", "wooden_sickle", actor, 1);
        state = FarmFieldTests.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Fields = kind switch
            {
                FarmWorkKind.Till => [],
                FarmWorkKind.Plant => [new(point, household, FarmFieldStage.Prepared)],
                FarmWorkKind.Tend => [new(point, household, FarmFieldStage.Growing, FarmFieldRules.Grain, ReadyTick: 100)],
                _ => state.Fields,
            },
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(),
            } : person).ToArray(),
        };
        return (state, actor, point);
    }

    private sealed class Choices(string actor, FarmWorkKind kind, bool choose) : IDecisionProvider
    {
        public FieldActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is FieldActionPlan plan && plan.Choice.Kind == kind && (kind != FarmWorkKind.Plant || plan.Choice.Crop == FarmFieldRules.Grain)) : null;
            if (candidate is not null) Plan = Assert.IsType<FieldActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
