using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Fact]
    public async Task PreparedParenthoodCopiesKeepIndependentPeopleAndStock()
    {
        var first = await PreparedState();
        var expected = WorldRuntimeCodec.Encode(first);
        var second = await PreparedState();
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second));
        Assert.NotSame(first.Agents, second.Agents);
        Assert.NotSame(first.Society.Society.Inventory.Lots, second.Society.Society.Inventory.Lots);

        var people = Assert.IsAssignableFrom<IList<AgentState>>(first.Agents);
        people[0] = people[0] with { FullnessBasisPoints = 1 };
        var stock = Assert.IsAssignableFrom<IList<InventoryLot>>(first.Society.Society.Inventory.Lots);
        stock[0] = stock[0] with { ConditionBasisPoints = 9_999 };
        Assert.NotEqual(expected, WorldRuntimeCodec.Encode(first));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(await PreparedState()));
        using var restored = WorldRuntime.Restore(second, _ => new ParentProvider("safe_idle"));
        Assert.Equal(expected, WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public async Task RepeatedParenthoodSetupFitsItsAllocationBudget()
    {
        _ = await PreparedState();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var prepared = PreparedState();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(prepared.IsCompletedSuccessfully);
        var state = await prepared;
        Assert.InRange(allocated, 1, 8L * 1024 * 1024);
        GC.KeepAlive(state);
    }
}
