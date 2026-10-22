using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class HandcartRuntimeTests
{
    [Fact]
    public async Task PullingKeepsItsCartAndDestinationAcrossRollbackReplayAndReload()
    {
        var (state, actor, _) = await ControlFixture(HandcartControlKind.Park);
        var chooser = new PullChooser(actor, choose: true);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new PullChooser(actor, choose: true));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<HandcartPullActionPlan>(chooser.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal("control-cart", plan.CartId);
        Assert.True(plan.Route.Count > 1);
        var position = world.Agents.Single(person => person.AgentId == actor).Position;
        Assert.Equal(plan.Route[1], position);
        var cart = world.Society.Inventory.GetLot(plan.CartId);
        Assert.Equal(new InventoryGroundPosition(position.X, position.Y), cart.GroundPosition);
        Assert.True(cart.ConditionBasisPoints < 10_000);
        Assert.Equal((actor, plan.CartId, 4), (world.Society.Inventory.GetLot("control-cargo").OwnerId,
            world.Society.Inventory.GetLot("control-cargo").ContainerLotId, world.Society.Inventory.GetLot("control-cargo").Quantity));
        Assert.Single(world.ExportState().Events, item => item.Kind == "handcart_moved");
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task ABlockedPullRemainsAnOfferedNativeWaitWithTheSameCartAndCargo()
    {
        var (state, actor, _) = await ControlFixture(HandcartControlKind.Park);
        var smith = state.WorldSimulation!.Buildings.Single(building => building.InstanceId == "first-town-blacksmith");
        var owner = state.Society.Society.Agents.First(person => person.HouseholdId != smith.HouseholdId).Id;
        var obstacle = state.Society.Society.Agents.First(person => person.HouseholdId == smith.HouseholdId).Id;
        var origin = state.Agents.Single(person => person.AgentId == owner).Position;
        var inventory = owner == actor ? state.Society.Society.Inventory :
            InventoryAuthority.Transfer(state.Society.Society.Inventory, "pull-fixture-owner", actor, owner,
                "control-cart", 1, "fixture", destinationGroundPosition: new(origin.X, origin.Y));
        state = AtPosition(state, owner, origin, inventory) with
        {
            HandcartHitches = [new("control-cart", owner)],
            Agents = state.Agents.Select(person => person.AgentId == obstacle ? person with { Position = smith.Position } : person).ToArray(),
        };
        var chooser = new PullChooser(owner, choose: true, smith.InstanceId);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartPullActionPlan>(chooser.Plan);
        Assert.Empty(plan.Route);
        Assert.Contains("blocked", chooser.Description, StringComparison.Ordinal);
        Assert.True(world.CheckActionPlan(plan).Allowed);
        Assert.Single(world.ExportState().Events, item => item.Kind == "handcart_blocked");
        Assert.Equal(new InventoryGroundPosition(origin.X, origin.Y), world.Society.Inventory.GetLot(plan.CartId).GroundPosition);
        Assert.Equal(4, world.Society.Inventory.GetLot("control-cargo").Quantity);
        Assert.Single(world.ExportState().HandcartHitches!);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task PullingRefusesChangedAttachmentsOrCargoPromisesWithoutMovingThem()
    {
        var (state, actor, _) = await ControlFixture(HandcartControlKind.Park);
        var chooser = new PullChooser(actor, choose: false);
        using var world = WorldRuntime.Restore(state, _ => chooser);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<HandcartPullActionPlan>(chooser.Plan);
        var checkpoint = world.ExportState();
        using var detached = WorldRuntime.Restore(checkpoint with { HandcartHitches = [] });
        Assert.Equal(ActionRefusalReason.TargetUnavailable, detached.CheckActionPlan(plan).Refusal!.Reason);
        var inventory = InventoryAuthority.Reserve(checkpoint.Society.Society.Inventory, "pull-promised-cargo", actor,
            "control-cargo", 4, "other_work", long.MaxValue);
        using var claimed = WorldRuntime.Restore(checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } });
        var bytes = WorldRuntimeCodec.Encode(claimed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, claimed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(claimed.ExportState()));
    }

    private sealed class PullChooser(string actor, bool choose, string? building = null) : CartPlanChooser<HandcartPullActionPlan>(actor, choose)
    {
        protected override bool Select(HandcartPullActionPlan plan) => building is null ? plan.Route.Count > 1 : plan.Choice.BuildingId == building;
    }
}
