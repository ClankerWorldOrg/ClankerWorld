using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class SmithGatherActionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GatheringAndPreparationCarryTheSamePlanAcrossRollbackReplayAndReload(bool prepareCargo)
    {
        var (state, actor, _) = await Fixture();
        if (prepareCargo) state = FoodCapacityTestFixture.WithInventory(state,
            InventoryAuthority.AddLot(state.Society.Society.Inventory, "gather-plan-ballast", "stone", actor, 4));
        var choices = new Choices(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => choices);
        var baseline = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(baseline, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(baseline), _ => new Choices(actor, choose: true));
        var eventKind = prepareCargo ? "spare_cargo_stored" : "material_gathered";
        var samePlan = false;
        for (var tick = 0; tick < 24 && !world.ExportState().Events.Any(item => item.Kind == eventKind); tick++)
        {
            var result = await world.AdvanceOneTickAsync();
            Assert.True(result.Advanced);
            samePlan |= result.Decisions.Any(decision => decision.AgentId == actor &&
                choices.Plan is not null && ReferenceEquals(decision.ActionPlan, choices.Plan));
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
        Assert.True(samePlan);
        Assert.Contains(world.ExportState().Events, item => item.Kind == eventKind);
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GatheringKeepsItsOriginalSourceAndToolAndRefusesTheirLoss(bool sourceLost)
    {
        var (state, actor, tree) = await Fixture(basket: true);
        var choices = new Choices(actor);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<SmithGatherActionPlan>(choices.Plan);
        Assert.Equal((tree.Id, "gather-plan-axe", 6), (plan.Source.Id, plan.Harvest!.ToolLotId, plan.Harvest.Quantity));
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-better-gather-axe", "iron_axe", actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        var changed = sourceLost ? checkpoint with
        {
            Resources = checkpoint.Resources.Select(item => item.ResourceId == tree.Id ? item with { State = ResourceState.Depleted } : item).ToArray(),
            WorldSystems = checkpoint.WorldSystems! with
            {
                Ecology = checkpoint.WorldSystems.Ecology with
                {
                    Resources = checkpoint.WorldSystems.Ecology.Resources.Select(item => item.Id == tree.Id
                        ? EcologyRules.Harvest(item, item.Quantity).Resource! : item).ToArray(),
                },
            },
        } : FoodCapacityTestFixture.WithInventory(checkpoint, InventoryAuthority.Reserve(inventory,
            "gather-plan-tool-claim", actor, "gather-plan-axe", 1, "other_work", long.MaxValue));
        using var unavailable = WorldRuntime.Restore(changed);
        Assert.Equal(ActionRefusalReason.StockUnavailable, unavailable.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal((tree.Id, "gather-plan-axe", 6), (plan.Source.Id, plan.Harvest.ToolLotId, plan.Harvest.Quantity));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(unavailable.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(unavailable.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task PreparationCollectsItsSelectedSharedToolAndRefusesANewClaimOnIt()
    {
        var (state, actor, tree) = await Fixture();
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var inventory = InventoryAuthority.Transfer(state.Society.Society.Inventory, "gather-plan-shared-tool",
            actor, household, "gather-plan-axe", 1, "fixture", destinationGroundPosition: new(tree.Position.X, tree.Position.Y));
        state = FoodCapacityTestFixture.WithInventory(state, inventory);
        var choices = new Choices(actor);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<SmithGatherActionPlan>(choices.Plan);
        var pickup = Assert.IsType<PlannedEquipmentPickup>(plan.ToolPickup);
        Assert.Equal("gather-plan-axe", pickup.StockId);
        Assert.Null(plan.Harvest);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.Reserve(checkpoint.Society.Society.Inventory, "gather-plan-shared-claim",
            household, pickup.StockId, 1, "other_work", long.MaxValue);
        using var unavailable = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, unavailable.CheckActionPlan(plan).Refusal!.Reason);
        using var collecting = WorldRuntime.Restore(state, _ => new Choices(actor, choose: true));
        for (var tick = 0; tick < 16 && !collecting.ExportState().Events.Any(item => item.Kind == "equipment_collected" &&
                 item.Detail == actor + ":" + pickup.ItemKind); tick++)
            Assert.True((await collecting.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(collecting.ExportState().Events, item => item.Kind == "equipment_collected" && item.Detail == actor + ":" + pickup.ItemKind);
        Assert.Equal((household, actor), (collecting.Society.Inventory.GetLot(pickup.StockId).OwnerId,
            collecting.Society.Inventory.GetLot(pickup.StockId).CarrierId));
        using var reloaded = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(collecting.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(collecting.ExportState()), WorldRuntimeCodec.Encode(reloaded.ExportState()));
    }

    [Fact]
    public async Task CargoPreparationKeepsItsOriginalLotQuantityAndStoragePlace()
    {
        var (state, actor, _) = await Fixture();
        var inventory = InventoryAuthority.AddLot(state.Society.Society.Inventory, "gather-plan-ballast", "stone", actor, 4);
        state = FoodCapacityTestFixture.WithInventory(state, inventory);
        var choices = new Choices(actor);
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<SmithGatherActionPlan>(choices.Plan);
        var cargo = Assert.IsType<PlannedCargoStorage>(plan.CargoPreparation);
        var move = Assert.Single(cargo.Cargo);
        Assert.Equal(("gather-plan-ballast", 4), (move.StockId, move.Quantity));
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-ballast", "stone", actor, 1, groundPosition: new(0, 0));
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "gather-plan-cargo-claim", actor, move.StockId, 4, "other_work", long.MaxValue);
        using var unavailable = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, unavailable.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Same(cargo, plan.CargoPreparation);
        unavailable.Validate();
    }

    internal static async Task<(WorldRuntimeState State, string Actor, MapResource Tree)> Fixture(bool basket = false)
    {
        using var setup = NormalPathWorld.CreateGenerated("smith-whole-load", _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.True((await setup.AdvanceOneTickAsync()).Advanced);
        var state = setup.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        var tree = state.Map.Resources.Where(resource => TreeGrowthRules.IsWoodTree(resource.TreeKind) &&
                state.Map.IsPassable(resource.Position) && state.Map.IsReachableOnFoot(smith.Position, resource.Position) &&
                state.Agents.All(person => person.AgentId == actor || person.Position != resource.Position))
            .OrderBy(resource => state.Map.FootDistance(smith.Position, resource.Position)).ThenBy(resource => resource.Id, StringComparer.Ordinal).First();
        var inventory = state.Society.Society.Inventory with { Lots = [], Reservations = [] };
        inventory = InventoryAuthority.AddLot(inventory, "gather-plan-axe", "wooden_axe", actor, 1);
        if (basket) inventory = InventoryAuthority.AddLot(inventory, "gather-plan-basket", "basket", actor, 1);
        foreach (var input in state.WorldContent!.Recipes.Where(recipe => recipe.WorkstationBuildingId == smith.DefinitionId &&
                     !recipe.Outputs.Any(output => output.ResourceId == InventoryContainerRules.Handcart))
                     .SelectMany(recipe => recipe.Inputs).Where(input => input.ResourceId != "wood").GroupBy(input => input.ResourceId))
            inventory = InventoryAuthority.AddLot(inventory, "gather-plan-input-" + input.Key, input.Key, smith.HouseholdId!,
                input.Max(item => item.Amount) * 2, storageBuildingId: smith.InstanceId);
        state = FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            {
                Position = tree.Position,
                FullnessBasisPoints = 10_000,
                Equipment = basket ? new(CarryAidLotId: "gather-plan-basket") : null,
                Project = null,
                LastDecisionContext = null,
                Survival = new SurvivalCondition(),
            } : person).ToArray(),
        };
        return (state, actor, tree);
    }

    private sealed class Choices(string actor, bool choose = false) : IDecisionProvider
    {
        public SmithGatherActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor
                ? request.Observation.Candidates.SingleOrDefault(item => item.Id == "gather_smith_input:wood") : null;
            if (candidate is not null)
                Plan = Assert.IsType<SmithGatherActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == selected ? 1d : 0d)));
        }
    }
}
