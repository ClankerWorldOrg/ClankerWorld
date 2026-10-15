using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HandcartRuntimeTests
{
    [Theory]
    [InlineData(HandcartCargoKind.Load, false)]
    [InlineData(HandcartCargoKind.Load, true)]
    [InlineData(HandcartCargoKind.Unload, false)]
    [InlineData(HandcartCargoKind.UnloadGround, false)]
    public async Task CargoKeepsItsStockCartAndQuantityAcrossRollbackReplayAndReload(HandcartCargoKind kind, bool shared)
    {
        var (state, actor) = await CargoFixture(shared);
        var chooser = new CargoChooser(actor, kind, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        var total = world.Society.Inventory.Lots.Sum(lot => lot.Quantity);
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new CargoChooser(actor, kind, true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<HandcartCargoActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal("cargo-plan-cart", plan.CartId);
        Assert.Equal(kind == HandcartCargoKind.Load ? 6 : 4, plan.Quantity);
        var moved = world.Society.Inventory.GetLot(plan.Choice.StockId);
        Assert.Equal((actor, plan.Quantity), (moved.OwnerId, moved.Quantity));
        Assert.Equal(kind == HandcartCargoKind.Load ? plan.CartId : null, moved.ContainerLotId);
        Assert.Equal(kind == HandcartCargoKind.UnloadGround ? new InventoryGroundPosition(plan.Position.X, plan.Position.Y) : null,
            moved.GroundPosition);
        Assert.Equal(total, world.Society.Inventory.Lots.Sum(lot => lot.Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == (kind == HandcartCargoKind.Load ? "handcart_loaded" : "handcart_unloaded"));
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CargoPlanKeepsItsSelectedCartAndRefusesAStockOrFamilyClaim(bool cartClaim)
    {
        var (state, actor) = await CargoFixture(false);
        var chooser = new CargoChooser(actor, HandcartCargoKind.Load, false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartCargoActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "0-earlier-cargo-cart", "handcart", actor, 1,
            groundPosition: new(plan.Position.X, plan.Position.Y));
        WorldRuntimeState WithInventory(InventoryCheckpoint value) => checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = value } } };
        using var added = WorldRuntime.Restore(WithInventory(inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "cargo-plan-other-work", actor,
            cartClaim ? "cargo-plan-inside" : plan.Choice.StockId, 1, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(WithInventory(inventory));
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
    }

    [Fact]
    public async Task APlanStillUnloadsSpoiledCargoFromABrokenCart()
    {
        var (state, actor) = await CargoFixture(false);
        var inventory = state.Society.Society.Inventory with
        {
            Lots = state.Society.Society.Inventory.Lots.Select(lot => lot.Id == "cargo-plan-cart" ? lot with { ConditionBasisPoints = 0 } :
                lot.Id == "cargo-plan-inside" ? lot with { ItemKind = "berries", FreshnessBasisPoints = 0 } : lot).ToArray(),
        };
        state = state with { Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } } };
        var chooser = new CargoChooser(actor, HandcartCargoKind.UnloadGround, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartCargoActionPlan>(chooser.Plan);
        var cargo = world.Society.Inventory.GetLot(plan.Choice.StockId);
        Assert.Equal((actor, 4, 0), (cargo.OwnerId, cargo.Quantity, cargo.FreshnessBasisPoints));
        Assert.Null(cargo.ContainerLotId);
        Assert.Equal(new InventoryGroundPosition(plan.Position.X, plan.Position.Y), cargo.GroundPosition);
        Assert.Equal(0, world.Society.Inventory.GetLot(plan.CartId).ConditionBasisPoints);
        world.Validate();
    }

    private static async Task<(WorldRuntimeState State, string Actor)> CargoFixture(bool shared)
    {
        var (state, actor, _) = await ControlFixture(HandcartControlKind.Give);
        var point = state.Agents.Single(person => person.AgentId == actor).Position;
        var owner = shared ? state.Society.Society.GetAgent(actor).HouseholdId! : actor;
        var inventory = InventoryAuthority.AddLot(FoodCapacityTestFixture.WithoutPersonalCargo(state, actor), "cargo-plan-cart", "handcart", actor, 1,
            groundPosition: new(point.X, point.Y));
        inventory = InventoryAuthority.AddLot(inventory, "cargo-plan-inside", "stone", actor, 4, containerLotId: "cargo-plan-cart");
        inventory = InventoryAuthority.AddLot(inventory, "cargo-plan-stock", "wood", owner, 6, groundPosition: new(point.X, point.Y));
        return (state with
        {
            Society = state.Society with { Society = state.Society.Society with { Inventory = inventory } },
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Equipment = null } : person).ToArray(),
        }, actor);
    }

    private sealed class CargoChooser(string actor, HandcartCargoKind kind, bool choose) : IDecisionProvider
    {
        public HandcartCargoActionPlan? Plan { get; private set; }
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var stock = kind == HandcartCargoKind.Load ? "cargo-plan-stock" : "cargo-plan-inside";
            var candidate = request.Observation.AgentId == actor ? request.Observation.Candidates.FirstOrDefault(item =>
                item.ActionPlan is HandcartCargoActionPlan plan && plan.Choice.Kind == kind && plan.Choice.StockId == stock) : null;
            if (candidate is not null) Plan = Assert.IsType<HandcartCargoActionPlan>(candidate.ActionPlan);
            var selected = choose ? candidate?.Id ?? "safe_idle" : "safe_idle";
            return ValueTask.FromResult(new CognitionDecisionResponse(request.RequestId, request.Observation.AgentId, Kind,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration, request.Observation.ObservationDigest,
                selected, 1, new Dictionary<string, double> { [selected] = 1 }));
        }
    }
}
