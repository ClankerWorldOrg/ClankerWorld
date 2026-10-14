using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class WorkstationSupplyActionPlanTests
{
    [Fact]
    public async Task CarriedSupplyKeepsItsLotLoadAndWorkstationWhenOtherInputsAppear()
    {
        var (state, shopId) = TailorTestWorld.Create("workstation-destination-audit", 0);
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == shopId);
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == shop.HouseholdId).Id;
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor),
            "supply-plan-fiber", "fiber", actor, 3);
        state = Prepared(state, actor, inventory, shop.Position);
        var choices = new Choices(actor, "supply_workstation:fiber");
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<WorkstationSupplyActionPlan>(choices.Plan);
        Assert.Equal((WorkstationSupplyKind.Carried, shopId, "supply-plan-fiber", 3),
            (plan.Kind, plan.BuildingId, plan.StockId, plan.Quantity));
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-carried-fiber", "fiber", actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "supply-plan-other-work", actor, plan.StockId!, 1, "other_work", inventory.WorldTick + 100);
        using var changed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal((shopId, "supply-plan-fiber", 3), (plan.BuildingId, plan.StockId, plan.Quantity));
        changed.Validate();
    }

    [Fact]
    public async Task GatheringKeepsItsOriginalSourceToolAndYieldWhenABetterToolAppears()
    {
        var (state, shopId) = TailorTestWorld.Create("workstation-destination-audit", 0);
        var shop = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == shopId);
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == shop.HouseholdId).Id;
        var inventory = FoodCapacityTestFixture.WithoutPersonalCargo(state, actor);
        foreach (var lot in inventory.Lots.Where(lot => lot.ItemKind == "wood" && lot.OwnerId == shop.HouseholdId &&
                     InventoryRules.AvailableQuantity(InventoryIndex.For(inventory), lot) > 0).ToArray())
            inventory = InventoryAuthority.Reserve(inventory, "supply-plan-protect:" + lot.Id, lot.OwnerId, lot.Id,
                InventoryRules.AvailableQuantity(InventoryIndex.For(inventory), lot), "other_work", long.MaxValue,
                holdAccess: StockHoldTestState.Access(inventory, lot.Id));
        inventory = InventoryAuthority.AddLot(inventory, "supply-plan-axe", "wooden_axe", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "supply-plan-basket", "basket", actor, 1);
        inventory = InventoryAuthority.AddLot(inventory, "supply-plan-shop-fiber", "fiber", shop.HouseholdId!, 6, storageBuildingId: shopId);
        var loose = state.Map.Resources.Where(resource => resource.Kind == "wood" || resource.NaturalObjectKind == "fallen_wood")
            .Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        state = Prepared(state, actor, inventory, shop.Position) with
        {
            Resources = state.Resources.Select(resource => loose.Contains(resource.ResourceId) ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = shop.Position, FullnessBasisPoints = 9_000, Equipment = new(CarryAidLotId: "supply-plan-basket"), LastDecisionContext = null } : person).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource => loose.Contains(resource.Id) && resource.Quantity > 0
                        ? EcologyRules.Harvest(resource, resource.Quantity).Resource! : resource).ToArray(),
                },
            },
        };
        var choices = new Choices(actor, "supply_workstation:wood");
        using var world = WorldRuntime.Restore(state, _ => choices);
        for (var tick = 0; tick < 8 && choices.Plan is null; tick++) Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<WorkstationSupplyActionPlan>(choices.Plan);
        Assert.Equal(WorkstationSupplyKind.Gather, plan.Kind);
        Assert.Equal("supply-plan-axe", plan.Harvest!.ToolLotId);
        var selected = (plan.Source!.Id, plan.Harvest.ToolLotId, plan.Quantity);
        var checkpoint = world.ExportState();
        inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-new-better-axe", "iron_axe", actor, 1);
        using var added = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "supply-plan-tool-work", actor, plan.Harvest.ToolLotId!, 1, "other_work", inventory.WorldTick + 100);
        using var changed = WorldRuntime.Restore(FoodCapacityTestFixture.WithInventory(checkpoint, inventory));
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(selected, (plan.Source.Id, plan.Harvest.ToolLotId, plan.Quantity));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(changed.ExportState())));
        Assert.Equal(WorldRuntimeCodec.Encode(changed.ExportState()), WorldRuntimeCodec.Encode(reopened.ExportState()));
    }

    private static WorldRuntimeState Prepared(WorldRuntimeState state, string actor, InventoryCheckpoint inventory, GridPoint position) =>
        FoodCapacityTestFixture.WithInventory(WeatherTestFixture.WithWeather(state, WeatherKind.Clear), inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with
            { Position = position, FullnessBasisPoints = 9_000, Equipment = null, Project = null, LastDecisionContext = null } : person).ToArray(),
        };

    private sealed class Choices(string actor, string wanted) : IDecisionProvider
    {
        public ActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.SingleOrDefault(item => item.Id == wanted) : null;
            if (candidate is not null) Plan = candidate.ActionPlan;
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId,
                Kind, ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, "safe_idle", 1,
                request.Observation.Candidates.ToDictionary(item => item.Id, item => item.Id == "safe_idle" ? 1d : 0d, StringComparer.Ordinal)));
        }
    }
}
