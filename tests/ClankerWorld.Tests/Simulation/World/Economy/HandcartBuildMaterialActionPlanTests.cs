using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HandcartRuntimeTests
{
    [Fact]
    public async Task CartBuildMaterialKeepsItsRecipeStockAndQuantityAcrossRollbackReplayAndReload()
    {
        var (state, actor) = BuildMaterialFixture(true);
        var chooser = new BuildMaterialChooser(actor, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new BuildMaterialChooser(actor, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<HandcartBuildMaterialActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal("build-material-fittings", plan.Pickup.StockId);
        Assert.Equal(2, plan.Quantity);
        Assert.Equal(state.WorldContent!.Recipes.Single(recipe => recipe.LocalId == "handcart").CanonicalId, plan.RecipeId);
        Assert.Equal("first-town-blacksmith", plan.SmithId);
        var collected = world.Society.Inventory.GetLot(plan.Pickup.StockId);
        Assert.Equal((actor, 2), (collected.OwnerId, collected.Quantity));
        Assert.True(ToolProgressionRules.IsTopLevelCarriedLot(collected, actor));
        Assert.Equal(4, world.Society.Inventory.GetLot("build-material-wood").Quantity);
        Assert.Equal(1, world.Society.Inventory.GetLot("build-material-rope").Quantity);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.ItemKind == "handcart");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task NewCartBuildStockDoesNotReplaceThePlanAndAClaimRefusesTheOriginal()
    {
        var (state, actor) = BuildMaterialFixture(true);
        var chooser = new BuildMaterialChooser(actor, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartBuildMaterialActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-earlier-build-fittings", "iron_fittings", plan.HouseholdId, 2,
            groundPosition: new(plan.Pickup.Position.X, plan.Pickup.Position.Y));
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "build-material-other-work", plan.HouseholdId, plan.Pickup.StockId, 2,
            "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
    }

    [Fact]
    public async Task ACartBuildStillOffersNoMaterialPickupWhenItsSetCannotBeFinished()
    {
        var (state, actor) = BuildMaterialFixture(false);
        var chooser = new BuildMaterialChooser(actor, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.NotEmpty(chooser.Offered);
        Assert.DoesNotContain(chooser.Offered, id => id.StartsWith(HandcartBuildMaterialChoice.Prefix, StringComparison.Ordinal));
        Assert.Null(chooser.Plan);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    private static (WorldRuntimeState State, string Actor) BuildMaterialFixture(bool fittings)
    {
        using var setup = NormalPathWorld.CreateGenerated("cart-set-unfinished", _ => new CartChooser("safe_idle"));
        var state = setup.ExportState();
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var actor = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        Assert.DoesNotContain(state.Society.Society.Inventory.Lots, lot => lot.ItemKind == "iron_fittings");
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), "build-material-wood", "wood", actor, 4);
        inventory = InventoryAuthority.AddLot(inventory, "build-material-rope", "rope", actor, 1);
        if (fittings) inventory = InventoryAuthority.AddLot(inventory, "build-material-fittings", "iron_fittings", smith.HouseholdId!, 2,
            groundPosition: new(smith.Position.X, smith.Position.Y));
        state = AtPosition(state, actor, smith.Position, inventory) with
        {
            JevEnabled = false,
            RoutineHelper = RoutineHelperSettings.Off,
            Agents = AtPosition(state, actor, smith.Position, inventory).Agents.Select(person => person.AgentId == actor ? person with { Equipment = null } : person).ToArray(),
        };
        return (WeatherTestFixture.WithWeather(state, WeatherKind.Clear), actor);
    }

    private sealed class BuildMaterialChooser(string actor, bool choose) : IDecisionProvider
    {
        public HandcartBuildMaterialActionPlan? Plan { get; private set; }
        public HashSet<string> Offered { get; } = new(StringComparer.Ordinal);
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Observation.AgentId == actor) Offered.UnionWith(request.Observation.Candidates.Select(item => item.Id));
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is HandcartBuildMaterialActionPlan plan && plan.Choice.ItemKind == "iron_fittings") : null;
            if (candidate is not null) Plan = Assert.IsType<HandcartBuildMaterialActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
