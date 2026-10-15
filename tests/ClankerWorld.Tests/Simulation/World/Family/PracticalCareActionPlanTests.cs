using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class CaregiverFoodRoutingTests
{
    [Fact]
    public async Task AChildCarePlanKeepsItsServingAcrossNativeRollbackReplayAndReload()
    {
        var (state, actor, child) = PreparedChildCareState("berries");
        var provider = new CareProvider(actor);
        using var world = WorldRuntime.Restore(state, _ => provider);
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(commitPermitted: () => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new CareProvider(actor));
        var step = await world.AdvanceOneTickAsync();
        Assert.True(step.Advanced);
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        var plan = Assert.IsType<PracticalCareActionPlan>(provider.Plan);
        Assert.Same(plan, step.Decisions.Single(decision => decision.AgentId == actor).ActionPlan);
        Assert.Equal(child, plan.Choice.DependentId);
        Assert.Equal("named-child-serving", Assert.IsType<ConsumeFoodActionPlan>(plan.Food).StockId);
        Assert.Single(world.ExportState().Events, item => item.Kind == "child_cared_for" && item.Detail == child);
        using var reopened = WorldRuntime.Restore(WorldRuntimeCodec.Decode(WorldRuntimeCodec.Encode(world.ExportState())));
        reopened.Validate();
    }

    [Fact]
    public async Task AConsumedChildServingIsNeverReplacedByAnotherAvailableLot()
    {
        var (state, actor, _) = PreparedChildCareState("berries");
        var provider = new CareProvider(actor);
        using var world = WorldRuntime.Restore(state, _ => provider);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var plan = Assert.IsType<PracticalCareActionPlan>(provider.Plan);
        var checkpoint = world.ExportState();
        var inventory = InventoryAuthority.AddLot(checkpoint.Society.Society.Inventory, "a-replacement-child-serving", "berries", actor, 1);
        using var changed = WorldRuntime.Restore(checkpoint with
        { Society = checkpoint.Society with { Society = checkpoint.Society.Society with { Inventory = inventory } } });
        var bytes = WorldRuntimeCodec.Encode(changed.ExportState());
        Assert.Equal(ActionRefusalReason.StockUnavailable, changed.CheckActionPlan(plan).Refusal!.Reason);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(changed.ExportState()));
        Assert.Equal(1, changed.Society.Inventory.GetLot("a-replacement-child-serving").Quantity);
    }
}
