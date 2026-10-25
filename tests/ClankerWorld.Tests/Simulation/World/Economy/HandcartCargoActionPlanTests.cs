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
        using var added = WorldRuntime.Restore(RawInventoryTestState.WithInventory(checkpoint, inventory));
        Assert.True(added.CheckActionPlan(plan).Allowed);
        inventory = InventoryAuthority.Reserve(inventory, "cargo-plan-other-work", actor,
            cartClaim ? "cargo-plan-inside" : plan.Choice.StockId, 1, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(RawInventoryTestState.WithInventory(checkpoint, inventory));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnhousedCargoRequiresPersonalCollectionBeforeLoadingStoredStock(bool stored)
    {
        var (state, actor) = await CargoFixture(false);
        var household = state.Society.Society.GetAgent(actor).HouseholdId!;
        var house = state.WorldSimulation!.Buildings.First(building => building.HouseholdId == household &&
            state.WorldContent!.Buildings.Single(definition => definition.CanonicalId == building.DefinitionId).Tags.Contains("house"));
        using (var departure = WorldRuntime.Restore(state))
        {
            Assert.True(departure.DisplaceAdult(actor));
            state = departure.ExportState();
        }
        Assert.Null(state.Society.Society.GetAgent(actor).HouseholdId);
        var point = house.Position;
        var inventory = InventoryAuthority.MoveHandcart(state.Society.Society.Inventory, actor,
            "cargo-plan-cart", new(point.X, point.Y), 1);
        inventory = InventoryAuthority.Relocate(inventory, "unhoused-cargo-stock", "cargo-plan-stock", actor, 6,
            storageBuildingId: stored ? house.InstanceId : null,
            groundPosition: stored ? null : new(point.X, point.Y));
        state = RawInventoryTestState.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Position = point } : person).ToArray(),
        };
        var chooser = new CargoChooser(actor, HandcartCargoKind.Load, true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes),
            _ => new CargoChooser(actor, HandcartCargoKind.Load, true));
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var stock = world.Society.Inventory.GetLot("cargo-plan-stock");
        Assert.Equal(actor, stock.OwnerId);
        Assert.Equal(6, stock.Quantity);
        if (stored)
        {
            Assert.Null(chooser.Plan);
            Assert.Equal(house.InstanceId, stock.StorageBuildingId);
            Assert.Null(stock.ContainerLotId);
            Assert.DoesNotContain(world.ExportState().Events, item => item.Kind == "handcart_loaded");
        }
        else
        {
            Assert.IsType<HandcartCargoActionPlan>(chooser.Plan);
            Assert.Equal("cargo-plan-cart", stock.ContainerLotId);
            Assert.Null(stock.StorageBuildingId);
            Assert.Single(world.ExportState().Events, item => item.Kind == "handcart_loaded");
        }
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
        return (RawInventoryTestState.WithInventory(state, inventory) with
        {
            Agents = state.Agents.Select(person => person.AgentId == actor ? person with { Equipment = null } : person).ToArray(),
        }, actor);
    }

    private sealed class CargoChooser(string actor, HandcartCargoKind kind, bool choose) : CartPlanChooser<HandcartCargoActionPlan>(actor, choose)
    {
        protected override bool Select(HandcartCargoActionPlan plan) => plan.Choice.Kind == kind &&
            plan.Choice.StockId == (kind == HandcartCargoKind.Load ? "cargo-plan-stock" : "cargo-plan-inside");
    }
}
