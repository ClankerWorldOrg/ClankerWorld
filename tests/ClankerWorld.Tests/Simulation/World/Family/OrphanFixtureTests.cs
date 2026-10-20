using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed partial class ParenthoodTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrphanSetupsKeepIndependentMutableStateAndNativeContinuations(bool olderChild)
    {
        var first = await OrphanState(olderChild);
        var expected = WorldRuntimeCodec.Encode(first);
        var second = await OrphanState(olderChild);
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second));
        Assert.NotSame(first.Agents, second.Agents);
        Assert.NotSame(first.Society.Society.Agents, second.Society.Society.Agents);
        Assert.NotSame(first.Society.Society.Inventory.Lots, second.Society.Society.Inventory.Lots);
        Assert.NotSame(first.Map.Tiles, second.Map.Tiles);
        var child = Assert.Single(second.Society.Society.Births).ChildId;
        Assert.Equal(olderChild ? SocietyAgeBand.Child : SocietyAgeBand.Infant,
            second.Society.Society.GetAgent(child).AgeBand);
        Assert.Null(second.Society.Society.GetAgent(child).PrimaryCaregiverId);

        var agents = Assert.IsAssignableFrom<IList<AgentState>>(first.Agents);
        agents[0] = agents[0] with { FullnessBasisPoints = 1 };
        var tiles = Assert.IsAssignableFrom<IList<TerrainTile>>(first.Map.Tiles);
        tiles[0] = tiles[0] with { Terrain = TerrainKind.Ocean };
        Assert.Equal(expected, WorldRuntimeCodec.Encode(second));
        var third = await OrphanState(olderChild);
        Assert.Equal(expected, WorldRuntimeCodec.Encode(third));

        using var world = WorldRuntime.Restore(second, _ => new ParentProvider("safe_idle"));
        using var replay = WorldRuntime.Restore(third, _ => new ParentProvider("safe_idle"));
        var before = WorldRuntimeCodec.Encode(replay.ExportState());
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(before, WorldRuntimeCodec.Encode(replay.ExportState()));
        Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        world.Validate();
        replay.Validate();
    }
}
