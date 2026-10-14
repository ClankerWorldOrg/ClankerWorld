using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class SmithDeliveryActionPlanTests
{
    [Fact]
    public async Task NativePersonalStorageInvalidatesTheCapturedDirectSmithInputLoad()
    {
        var (state, actor, smith) = Prepared();
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == smith.HouseholdId &&
            building.InstanceId.StartsWith("first-town-house-", StringComparison.Ordinal));
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor),
            "smith-plan-stored-wood", "wood", actor, 8);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor
                ? person with { Position = house.Position } : person).ToArray(),
        };
        var choices = new OreChoices(actor, choose: false, candidateId: "haul_smith_input");
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<SmithDeliveryActionPlan>(choices.Plan);
        Assert.Equal((SmithDeliveryKind.Input, "smith-plan-stored-wood", true), (plan.Kind, plan.StockId, plan.DirectDelivery));
        Assert.True(world.CheckActionPlan(plan).Allowed);

        var receipt = world.SubmitInstruction(new("store-captured-smith-input", "owner:test", actor,
            OwnerInstructionKind.Order, "store wood in my House"));
        for (var tick = 0; tick < 8 && world.Society.Inventory.GetLot(plan.StockId).StorageBuildingId is null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal("finished", world.ExportState().Instructions!.Single(item => item.InstructionId == receipt.InstructionId).Task!.Status);
        var stored = world.Society.Inventory.GetLot(plan.StockId);
        Assert.Equal((actor, house.InstanceId, 8), (stored.OwnerId, stored.StorageBuildingId, stored.Quantity));
        Assert.False(PersonalEquipmentRules.IsCarried(stored, actor));
        Assert.Contains(world.ExportState().Events, item => item.Kind == "personal_goods_stored");
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False(world.CheckActionPlan(plan).Allowed);
        Assert.Equal(ActionRefusalReason.StockUnavailable, world.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes));
        Assert.False(reopened.CheckActionPlan(plan).Allowed);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(reopened.ExportState()));
        reopened.Validate();
    }

    [Fact]
    public async Task AnEarlierCollectedShipmentRefusesTheLaterLoadWhenTheRemainingInputNeedShrinks()
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new OreChoices("", choose: false));
        var state = generated.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var warehouse = state.WorldSimulation.Buildings.Single(building => building.InstanceId == "first-town-warehouse");
        var actors = state.Society.Society.GetHousehold(smith.HouseholdId!).MemberIds.Order(StringComparer.Ordinal).ToArray();
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Where(lot => lot.ItemKind != "wood" ||
                lot.OwnerId != smith.HouseholdId && lot.OwnerId != warehouse.TownId).ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "smith-plan-competing-wood", "wood", smith.HouseholdId!, 8);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = actors.Contains(person.AgentId, StringComparer.Ordinal) ? warehouse.Position : person.Position,
                FullnessBasisPoints = 9_000,
                Project = null,
                LastDecisionContext = null,
            }).ToArray(),
        };
        var providers = actors.ToDictionary(actor => actor, actor => new InputChoices(actor), StringComparer.Ordinal);
        using var world = WorldRuntime.Restore(state, actor => providers.TryGetValue(actor, out var provider)
            ? provider : new OreChoices("", choose: false));
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "choice_refused"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var first = Assert.IsType<SmithDeliveryActionPlan>(providers[actors[0]].Plan);
        var later = Assert.IsType<SmithDeliveryActionPlan>(providers[actors[1]].Plan);
        Assert.Equal(("smith-plan-competing-wood", 4), (first.StockId, first.Quantity));
        Assert.Equal((first.StockId, first.Quantity), (later.StockId, later.Quantity));
        Assert.Equal(4, world.Society.Inventory.GetLot(first.StockId).Quantity);
        Assert.Single(world.ExportState().Events, item => item.Kind == "smith_input_picked_up");
        var refusal = Assert.Single(world.ExportState().Events, item => item.Kind == "choice_refused");
        Assert.StartsWith(world.Society.GetAgent(actors[1]).Name + " didn't take supplies to the household Blacksmith:", refusal.Detail);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.OwnerId == actors[1] && lot.DeliveryBuildingId == smith.InstanceId);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Fact]
    public async Task OreDeliveryKeepsItsSelectedLotAndLoadWhenOtherOreAppears()
    {
        var (state, actor, smith) = Prepared();
        var choices = new OreChoices(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<SmithDeliveryActionPlan>(choices.Plan);
        Assert.Equal((SmithDeliveryKind.Ore, smith.InstanceId, "smith-plan-ore", 4, true),
            (plan.Kind, plan.SmithId, plan.StockId, plan.Quantity, plan.DirectDelivery));
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-smith-ore", "iron_ore", actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "smith-plan-other-work", actor, plan.StockId, 4, "other_work", inventory.WorldTick + 100);
        using var changed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal((smith.InstanceId, "smith-plan-ore", 4), (plan.SmithId, plan.StockId, plan.Quantity));
        changed.Validate();
    }

    [Fact]
    public async Task OreDeliveryUsesTheSamePlanThroughRollbackReplayAndStrictReload()
    {
        var (state, actor, smith) = Prepared();
        var choices = new OreChoices(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new OreChoices(actor, choose: true));
        var carriedPlan = false;
        for (var tick = 0; tick < 8 && !world.ExportState().Events.Any(item => item.Kind == "smith_ore_delivered"); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            carriedPlan |= result.Decisions.Any(decision => decision.AgentId == actor && ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(carriedPlan);
        var plan = Assert.IsType<SmithDeliveryActionPlan>(choices.Plan);
        Assert.Equal($"{actor}:{plan.StockId}:4:{smith.InstanceId}", Assert.Single(world.ExportState().Events,
            item => item.Kind == "smith_ore_delivered").Detail);
        Assert.Equal((smith.HouseholdId, smith.InstanceId, 4),
            (world.Society.Inventory.GetLot(plan.StockId).OwnerId, world.Society.Inventory.GetLot(plan.StockId).StorageBuildingId,
                world.Society.Inventory.GetLot(plan.StockId).Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private static (WorldRuntimeState State, string Actor, PlacedBuilding Smith) Prepared()
    {
        using var generated = NormalPathWorld.CreateGenerated("audit-town-invariants", _ => new OreChoices("", choose: false));
        var state = generated.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), "smith-plan-ore", "iron_ore", actor, 4);
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = smith.Position, Equipment = null, Project = null, FullnessBasisPoints = 9_000, LastDecisionContext = null } : person).ToArray(),
        };
        return (state, actor, smith);
    }

    private sealed class InputChoices(string actor) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "haul_smith_input") : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = candidate?.Id ?? "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }

    private sealed class OreChoices(string actor, bool choose, string candidateId = "deliver_smith_ore") : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item => item.Id == candidateId) : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
