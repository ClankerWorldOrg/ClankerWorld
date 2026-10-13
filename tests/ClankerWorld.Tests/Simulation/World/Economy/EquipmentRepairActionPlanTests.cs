using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class EquipmentRepairActionPlanTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NativeRepairKeepsItsTargetAndMaterialsAcrossPickupRollbackReplayAndReload(bool basket, bool shared)
    {
        var (state, actor) = Fixture(basket, shared);
        var choices = new Choices(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices(actor, choose: true));
        var samePlan = false;
        EquipmentRepairWork? work = null;
        for (var tick = 0; tick < 24 && !world.ExportState().Events.Any(item => item.Kind == "equipment_repaired"); tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && choices.Plan is not null &&
                ReferenceEquals(decision.ActionPlan, choices.Plan));
            work ??= world.Agents.Single(person => person.AgentId == actor).Equipment?.Repair;
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        Assert.NotNull(work);
        Assert.Equal("equipment-repair-plan-target", work.LotId);
        Assert.Single(world.ExportState().Events, item => item.Kind == "equipment_repaired");
        Assert.InRange(world.Society.Inventory.GetLot(work.LotId).ConditionBasisPoints, 8_900, 9_000);
        Assert.All(work.MaterialReservationIds, id => Assert.Equal(InventoryReservationState.Completed,
            world.Society.Inventory.GetReservation(id).State));
        foreach (var kind in basket ? new[] { "fiber", "rope" } : ["cloth"])
            Assert.Equal(1, world.Society.Inventory.GetLot("equipment-repair-plan-" + kind).Quantity);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
        reopened.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewMaterialsDoNotReplaceTheSelectedLotAndAClaimRefusesIt(bool shared)
    {
        var (state, actor) = Fixture(basket: false, shared);
        var choices = new Choices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<EquipmentRepairActionPlan>(choices.Plan);
        var stock = shared ? Assert.Single(plan.Inputs).Pickup.StockId : Assert.Single(plan.Materials).StockId;
        var owner = shared ? plan.HouseholdId : actor;
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-repair-cloth", "cloth", owner, 2,
            storageBuildingId: shared ? plan.BuildingId : null);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "equipment-repair-plan-other-work", owner, stock, 2, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
        Assert.Equal("equipment-repair-plan-cloth", stock);
    }

    [Fact]
    public async Task AReservedTargetRefusesThePlanAndBrokenEquipmentRemainsRepairable()
    {
        var (state, actor) = Fixture(basket: false, shared: false);
        var choices = new Choices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<EquipmentRepairActionPlan>(choices.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.WearSingleUnit(checkpoint.Society.Society.Inventory, plan.TargetId, 3_000);
        using var broken = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(broken.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(checkpoint.Society.Society.Inventory, "equipment-repair-plan-target-claim", actor,
            plan.TargetId, 1, "other_work", long.MaxValue);
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.AgentId == actor
                ? person with { Equipment = null } : person).ToArray(),
        };
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
    }

    private static (WorldRuntimeState State, string Actor) Fixture(bool basket, bool shared)
    {
        var (state, tailorId) = TailorTestWorld.Create("equipment-repair-action-plan", 0);
        var tailor = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == tailorId);
        var site = basket ? state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-house-a") : tailor;
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == site.HouseholdId).Id;
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory with { Lots = [], Reservations = [] },
            "equipment-repair-plan-target", basket ? "basket" : "padded_coat", actor, 1, conditionBasisPoints: 3_000);
        foreach (var kind in basket ? new[] { "fiber", "rope" } : ["cloth"])
            inventory = InventoryAuthority.AddLot(inventory, "equipment-repair-plan-" + kind, kind,
                shared ? site.HouseholdId! : actor, 2, storageBuildingId: shared ? site.InstanceId : null);
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? site.Position : person.Position,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new(),
                Equipment = person.AgentId == actor ? basket ? new(CarryAidLotId: "equipment-repair-plan-target")
                    : new(ClothingLotId: "equipment-repair-plan-target") : null,
            }).ToArray(),
        };
        return (state, actor);
    }

    private sealed class Choices(string actor, bool choose) : IDecisionProvider
    {
        public EquipmentRepairActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "repair_equipment") : null;
            if (candidate is not null) Plan = Assert.IsType<EquipmentRepairActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
