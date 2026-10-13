using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WarmthActionPlanTests(HearthReachabilityTests.Fixture fixture)
    : IClassFixture<HearthReachabilityTests.Fixture>
{
    private const string Actor = "founder:00000000000000000000000000000001";
    private const string Fuel = "hearth-control-fuel";
    private const string House = "first-town-house-a";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFireKeepsItsFuelAndHearthAcrossPickupRollbackReplayAndReload(bool shared)
    {
        var state = fixture.CreateState(blocked: false);
        if (shared)
        {
            var household = state.Society.Society.GetAgent(Actor).HouseholdId!;
            var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "fire-plan-fixture", Actor,
                household, Fuel, 1, "test_setup");
            inventory = InventoryAuthority.Relocate(inventory, "fire-plan-store", Fuel, household, 1, storageBuildingId: House);
            state = FoodCapacityTestFixture.WithInventory(state, inventory);
        }
        var choices = new Choices("tend_fire", choose: true);
        using var world = Restore(state, choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = Restore(WorldRuntimeCodec.Decode(bytes), new("tend_fire", choose: true));
        var samePlan = false;
        FireActionPlan? first = null;
        for (var tick = 0; tick < 32 && world.ExportState().Survival!.Fires.Count == 0; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            first ??= choices.Plan as FireActionPlan;
            samePlan |= step.Decisions.Any(decision => decision.AgentId == Actor && choices.Plan is not null &&
                ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        Assert.NotNull(first);
        if (shared) Assert.IsType<SharedFireFuel>(first.Fuel);
        else Assert.Equal(Fuel, Assert.IsType<CarriedFireFuel>(first.Fuel).StockId);
        Assert.Equal(House, Assert.Single(world.ExportState().Survival!.Fires).BuildingId);
        Assert.Single(world.ExportState().Events, item => item.Kind == "fire_fuelled");
        var payment = Assert.Single(world.Society.Inventory.Reservations, item => item.Purpose == "heating_fuel");
        Assert.Equal((Actor, InventoryReservationState.Completed, 1), (payment.OwnerId, payment.State, payment.Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Fact]
    public async Task NewFuelCannotReplaceTheCapturedLotAndAnotherFireRefusesTheSameHearth()
    {
        var choices = new Choices("tend_fire", choose: false);
        using var world = Restore(fixture.CreateState(blocked: false), choices);
        await Capture(world, choices);
        var plan = Assert.IsType<FireActionPlan>(choices.Plan);
        Assert.Equal(Fuel, Assert.IsType<CarriedFireFuel>(plan.Fuel).StockId);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-new-firewood", "wood", Actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "fire-plan-other-work", Actor, Fuel, 1, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        using var lit = WorldRuntime.Restore(checkpoint with
        {
            Survival = checkpoint.Survival! with { Fires = [new(House, checkpoint.Society.Society.WorldTick + 120)] },
        });
        Assert.Equal(ActionRefusalReason.TargetUnavailable, lit.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(1, lit.Society.Inventory.GetLot(Fuel).Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirewoodHarvestRetainsItsSourceAndYieldAndRefusesDepletion(bool tree)
    {
        var state = fixture.CreateState(blocked: false);
        var inventory = state.Society.Society.Inventory;
        foreach (var lot in inventory.Lots.Where(lot => lot.ItemKind == "wood"))
        {
            var available = Math.Max(0, InventoryRules.AvailableQuantity(InventoryIndex.For(inventory), lot));
            if (available > 0) inventory = InventoryAuthority.Reserve(inventory, "fire-plan-hold:" + lot.Id, lot.OwnerId,
                lot.Id, available, "other_work", long.MaxValue);
        }
        if (tree)
        {
            inventory = InventoryAuthority.AddLot(inventory, "fire-plan-gathering-axe", "wooden_axe", Actor, 1);
            var unavailable = state.Map.Resources.Where(source => source.Kind == "wood" &&
                (!TreeGrowthRules.IsWoodTree(source.TreeKind) || state.WorldSystems!.Ecology.Resources.Single(item => item.Id == source.Id).Quantity <= 1))
                .Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
            state = state with
            {
                Resources = state.Resources.Select(item => unavailable.Contains(item.ResourceId)
                    ? item with { State = ResourceState.Depleted } : item).ToArray(),
                WorldSystems = state.WorldSystems! with
                {
                    Ecology = state.WorldSystems.Ecology with
                    {
                        Resources = state.WorldSystems.Ecology.Resources.Select(item => unavailable.Contains(item.Id) && item.Quantity > 0
                            ? EcologyRules.Harvest(item, item.Quantity).Resource! : item).ToArray(),
                    },
                },
            };
        }
        var choices = new Choices("tend_fire", choose: false);
        using var world = Restore(FoodCapacityTestFixture.WithInventory(state, inventory), choices);
        await Capture(world, choices);
        var plan = Assert.IsType<FireActionPlan>(choices.Plan);
        var fuel = Assert.IsType<GatheredFireFuel>(plan.Fuel);
        Assert.True(fuel.Harvest.Quantity > 0);
        var checkpoint = world.ExportState();
        var ecology = checkpoint.WorldSystems!.Ecology;
        var resource = ecology.Resources.Single(item => item.Id == fuel.Source.Id);
        if (tree)
        {
            Assert.Equal("fire-plan-gathering-axe", fuel.Harvest.ToolLotId);
            Assert.Equal(0, fuel.Harvest.TreeSeedQuantity);
            using var lastHarvest = WorldRuntime.Restore(checkpoint with
            {
                WorldSystems = checkpoint.WorldSystems with
                {
                    Ecology = ecology with
                    {
                        Resources = ecology.Resources.Select(item => item.Id == fuel.Source.Id
                            ? EcologyRules.Harvest(resource, resource.Quantity - 1).Resource! : item).ToArray(),
                    },
                },
            });
            Assert.Equal(ActionRefusalReason.StockUnavailable, lastHarvest.CheckActionPlan(plan).Refusal!.Reason);
        }
        using var depleted = WorldRuntime.Restore(checkpoint with
        {
            Resources = checkpoint.Resources.Select(item => item.ResourceId == fuel.Source.Id
                ? item with { State = ResourceState.Depleted } : item).ToArray(),
            WorldSystems = checkpoint.WorldSystems with
            {
                Ecology = ecology with
                {
                    Resources = ecology.Resources.Select(item => item.Id == fuel.Source.Id
                        ? EcologyRules.Harvest(resource, resource.Quantity).Resource! : item).ToArray(),
                },
            },
        });
        Assert.Equal(ActionRefusalReason.StockUnavailable, depleted.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Same(fuel, plan.Fuel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWarmthKeepsItsBuildingOrTreeCoverAcrossReplayAndReload(bool blocked)
    {
        var choices = new Choices("seek_warmth", choose: true);
        using var world = Restore(fixture.CreateState(blocked), choices);
        using var replay = Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())), new("seek_warmth", choose: true));
        WarmthActionPlan? first = null;
        var samePlan = false;
        for (var tick = 0; tick < 24; tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            first ??= choices.Plan as WarmthActionPlan;
            samePlan |= step.Decisions.Any(decision => decision.AgentId == Actor && choices.Plan is not null &&
                ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        Assert.NotNull(first);
        Assert.Equal(blocked ? WarmthDestinationKind.NaturalCover : WarmthDestinationKind.Building, first.Kind);
        Assert.Equal(first.Destination, world.Agents.Single(person => person.AgentId == Actor).Position);
        Assert.Empty(world.ExportState().Survival!.Fires);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Fact]
    public async Task BlockedWarmthRouteRefusesTheOriginalDestinationAndCurrentProtectionKeepsTheActorStill()
    {
        var choices = new Choices("seek_warmth", choose: false);
        using var world = Restore(fixture.CreateState(blocked: false), choices);
        await Capture(world, choices);
        var plan = Assert.IsType<WarmthActionPlan>(choices.Plan);
        Assert.Equal(House, plan.BuildingId);
        var checkpoint = fixture.WithCrowd(world.ExportState(), blocked: true);
        using var crowded = WorldRuntime.Restore(checkpoint);
        var bytes = WorldRuntimeCodec.Encode(crowded.ExportState());
        Assert.Equal(ActionRefusalReason.OutOfReach, crowded.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(crowded.ExportState()));
        checkpoint = checkpoint with
        {
            Agents = checkpoint.Agents.Select(person => person.AgentId == Actor
                ? person with { Position = fixture.HousePosition } : person).ToArray(),
        };
        using var protectedWorld = WorldRuntime.Restore(checkpoint);
        Assert.True(protectedWorld.CheckActionPlan(plan).Allowed);
    }

    private static WorldRuntime Restore(WorldRuntimeState state, Choices choices) =>
        WorldRuntime.Restore(state, id => id == Actor ? choices : new ActionCoverageRecorder(chooseIdle: true));

    private static async Task Capture(WorldRuntime world, Choices choices)
    {
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.NotNull(choices.Plan);
    }

    private sealed class Choices(string action, bool choose) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.Candidates.SingleOrDefault(item => item.Id == action);
            if (candidate is not null) Plan = Assert.IsAssignableFrom<ActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
