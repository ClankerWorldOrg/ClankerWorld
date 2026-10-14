using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TreeActionPlanTests
{
    [Fact]
    public async Task CompetingSharedSeedChoicesRefuseTheOriginalPlanOnceAfterTheFirstPickup()
    {
        var (state, actor) = await Fixture(TreeChoice.Plant);
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var other = state.Society.Society.Agents.First(person => person.HouseholdId == household && person.Id != actor).Id;
        var farmhouse = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-farmhouse");
        Assert.Equal(household, farmhouse.HouseholdId);
        var inventory = FoodCapacityTestFixture.WithoutPersonalCargo(state, other);
        inventory = inventory with
        {
            Lots = inventory.Lots.Where(lot => lot.Id != "tree-plan-seed").ToArray(),
        };
        inventory = InventoryAuthority.AddLot(inventory, "tree-plan-shared-seed", TreeGrowthRules.TreeSeedItem, household, 1,
            storageBuildingId: farmhouse.InstanceId);
        state = FoodCapacityTestFixture.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person with
            {
                Position = person.AgentId == actor ? farmhouse.Position : person.AgentId == other ? state.Map.FootNeighbors(farmhouse.Position).First() : person.Position,
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(),
            }).ToArray(),
        };
        var choices = new Choices(actor, TreeChoice.Plant, choose: true, secondActor: other);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.Equal(2, choices.Plans.Count);
        Assert.All(choices.Plans.Values, plan => Assert.Equal("tree-plan-shared-seed", plan.Seed.StockId));
        var refusedActor = new[] { actor, other }.Single(id => id != world.Society.Inventory.GetLot("tree-plan-shared-seed").OwnerId);
        var refused = Assert.Single(step.Decisions, decision => decision.AgentId == refusedActor);
        Assert.Same(choices.Plans[refused.AgentId], refused.ActionPlan);
        Assert.Equal(ActionRefusalReason.StockUnavailable, world.CheckActionPlan(refused.ActionPlan!).Refusal!.Reason);
        var refusal = Assert.Single(step.Events, item => item.Kind == "choice_refused");
        Assert.Contains(world.Society.GetAgent(refusedActor).Name + " didn't plant the tree seed", refusal.Detail, StringComparison.Ordinal);
        Assert.Single(step.Events, item => item.Kind == "equipment_collected");
        Assert.Null(world.ExportState().Society.Cognition.Runtimes.Single(runtime => runtime.AgentId == refused.AgentId).CurrentIntention);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Fact]
    public async Task RegrowthRefusesTheSelectedStumpWithoutChoosingAnother()
    {
        var (state, actor) = await Fixture(TreeChoice.Replant);
        var choices = new Choices(actor, TreeChoice.Replant, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<TreeActionPlan>(choices.Plan);
        state = world.ExportState();
        state = state with
        {
            Resources = state.Resources.Select(item => item.ResourceId == plan.StumpId ? item with { State = ResourceState.Available } : item).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(item => item.Id == plan.StumpId
                        ? item with { Quantity = item.Capacity, State = EcologyResourceState.Available } : item).ToArray(),
                },
            },
        };
        using var regrown = WorldRuntime.Restore(state);
        var bytes = WorldRuntimeCodec.Encode(regrown.ExportState());
        Assert.Equal(ActionRefusalReason.TargetUnavailable, regrown.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(regrown.ExportState()));
    }

    [Theory]
    [InlineData(TreeChoice.Plant)]
    [InlineData(TreeChoice.Orchard)]
    [InlineData(TreeChoice.Replant)]
    public async Task NativeTreeWorkKeepsTheOfferedSelectionAcrossRollbackReplayAndReload(TreeChoice choice)
    {
        var (state, actor) = await Fixture(choice);
        var choices = new Choices(actor, choice, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new Choices(actor, choice, choose: true));
        var samePlan = false;
        for (var tick = 0; tick < 24 && !world.ExportState().Events.Any(item => item.Kind is "tree_planted" or "tree_replanted"); tick++)
        {
            var step = await world.AdvanceOneTickAsync();
            Assert.True(step.Advanced);
            samePlan |= step.Decisions.Any(decision => decision.AgentId == actor && choices.Plan is not null && ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        var plan = Assert.IsType<TreeActionPlan>(choices.Plan);
        var treeId = plan.StumpId ?? TreeGrowthRules.PlantedTreeId(plan.Position);
        Assert.Equal((plan.Position, plan.Species), (world.ExportState().Map.GetResource(treeId).Position, world.ExportState().Map.GetResource(treeId).TreeKind));
        Assert.True(world.WorldSystems.Ecology.GetResource(treeId).IsPlanted);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == plan.Seed.StockId);
        Assert.Single(world.ExportState().Events, item => item.Kind == (choice == TreeChoice.Replant ? "tree_replanted" : "tree_planted"));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    [Theory]
    [InlineData(TreeChoice.Plant)]
    [InlineData(TreeChoice.Orchard)]
    [InlineData(TreeChoice.Replant)]
    public async Task NewSeedDoesNotReplaceTheSelectedSeedAndANewClaimRefusesIt(TreeChoice choice)
    {
        var (state, actor) = await Fixture(choice);
        var choices = new Choices(actor, choice, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<TreeActionPlan>(choices.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-tree-seed", plan.Seed.ItemKind, actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "tree-plan-other-work", actor, plan.Seed.StockId, 1, "other_work", long.MaxValue);
        using var lost = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        var bytes = WorldRuntimeCodec.Encode(lost.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, lost.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(lost.ExportState()));
        Assert.Equal("tree-plan-seed", plan.Seed.StockId);
    }

    [Fact]
    public async Task OrchardPlanAdmitsItsSelectedReplantingClaimAndRefusesItsReplacement()
    {
        var (state, actor) = await Fixture(TreeChoice.Orchard);
        var inventory = InventoryAuthority.Reserve(state.Society.Society.Inventory, "orchard-replant:tree-plan-seed", actor,
            "tree-plan-seed", 1, "orchard_replanting", long.MaxValue);
        state = FoodCapacityTestFixture.WithInventory(state, inventory);
        var choices = new Choices(actor, TreeChoice.Orchard, choose: false);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<TreeActionPlan>(choices.Plan);
        Assert.Equal(new("orchard-replant:tree-plan-seed", 1), plan.Claim);
        Assert.Equal(1, Assert.Single(world.FindGoods(new(GoodsUse.TreePlanting, actor, GoodsOwners.One(actor), GoodsKinds.One(plan.Seed.ItemKind))).Matches).Quantity);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.ReleaseReservation(checkpoint.Society.Society.Inventory, plan.Claim!.ReservationId);
        using var released = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, released.CheckActionPlan(plan).Refusal!.Reason);
    }

    private static async Task<(WorldRuntimeState State, string Actor)> Fixture(TreeChoice choice)
    {
        using var initial = NormalPathWorld.CreateGenerated("tree-action-plan", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await initial.AdvanceOneTickAsync()).Advanced);
        var state = initial.ExportState();
        var actor = state.Agents[0].AgentId;
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), "tree-plan-seed",
            choice == TreeChoice.Orchard ? TreeGrowthRules.OrchardSeedItem : TreeGrowthRules.TreeSeedItem, actor, 1);
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Project = null,
                LastDecisionContext = null,
                FullnessBasisPoints = 10_000,
                Survival = new SurvivalCondition(),
            } : person).ToArray(),
        };
        if (choice == TreeChoice.Replant)
        {
            var stump = state.Map.Resources.Where(item => item.IsRenewable && TreeGrowthRules.IsWoodTree(item.TreeKind) &&
                    state.Map.IsReachableOnFoot(state.Agents[0].Position, item.Position))
                .OrderBy(item => state.Map.FootDistance(state.Agents[0].Position, item.Position)).First();
            var stand = state.Map.FootNeighbors(stump.Position).First(point => state.Agents.All(person => person.AgentId == actor || person.Position != point));
            state = state with
            {
                Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = stand } : person).ToArray(),
                Resources = state.Resources.Select(item => item.ResourceId == stump.Id ? item with { State = ResourceState.Depleted } : item).ToArray(),
                WorldSystems = state.WorldSystems! with
                {
                    Ecology = state.WorldSystems.Ecology with
                    {
                        Resources = state.WorldSystems.Ecology.Resources.Select(item => item.Id == stump.Id ? EcologyRules.Harvest(item, item.Quantity).Resource! : item).ToArray(),
                    },
                },
            };
        }
        return (state, actor);
    }

    private sealed class Choices(string actor, TreeChoice choice, bool choose, string? secondActor = null) : IDecisionProvider
    {
        public TreeActionPlan? Plan { get; private set; }
        public Dictionary<string, TreeActionPlan> Plans { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor || request.Observation.AgentId == secondActor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == TreeActionPlan.Candidate(choice)) : null;
            if (candidate is not null)
            {
                Plans[request.Observation.AgentId] = Assert.IsType<TreeActionPlan>(candidate.ActionPlan);
                if (request.Observation.AgentId == actor) Plan = Plans[request.Observation.AgentId];
            }
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
