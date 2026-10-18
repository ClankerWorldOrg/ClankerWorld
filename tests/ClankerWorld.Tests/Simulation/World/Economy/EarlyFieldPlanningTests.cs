using ClankerWorld.Simulation.World;
using Xunit.Abstractions;

namespace ClankerWorld.Tests;

public sealed class EarlyFieldPlanningTests(ITestOutputHelper output)
{
    [Fact]
    public async Task GeneratedTownPlantsEarlyThroughOrdinaryChoices()
    {
        var recorder = new ActionCoverageRecorder();
        using var world = NormalPathWorld.CreateGenerated("probe-a", _ => recorder);
        var initial = world.ExportState();
        var farmhouse = world.WorldSimulation.Buildings.Single(item => item.InstanceId == "first-town-farmhouse");
        var seedSite = initial.Map.Resources.Single(item => item.Id == "grain-seed-patch");
        Assert.True(initial.Map.IsReachableOnFoot(farmhouse.Position, seedSite.Position));
        Assert.InRange(initial.Map.FootDistance(farmhouse.Position, seedSite.Position), 0, 12);
        Assert.Empty(world.Fields);
        for (var tick = 0; tick < 720 && !world.ExportState().Events.Any(item => item.Kind == "field_planted"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True(world.ExportState().Events.Any(item => item.Kind == "field_planted"),
            $"Tick {world.WorldTick}; fields: {world.Fields.Count}; " +
            $"farm offers: {string.Join(", ", recorder.OfferedByAgent.Values.SelectMany(item => item.Keys).Where(id => id.StartsWith("farm:", StringComparison.Ordinal)).Distinct())}; " +
            $"chosen: {string.Join(", ", recorder.Chosen.OrderByDescending(item => item.Value).Take(15))}");
        Assert.Contains(world.ExportState().Events, item => item.Kind == "field_prepared");
        Assert.Contains(recorder.Chosen.Keys, id => id.StartsWith("farm:Till:", StringComparison.Ordinal));
        Assert.Contains(recorder.Chosen.Keys, id => id.StartsWith("farm:Plant:", StringComparison.Ordinal));
        output.WriteLine($"First field planted at tick {world.WorldTick}; grain seed site is " +
            $"{initial.Map.FootDistance(farmhouse.Position, seedSite.Position)} tiles from the Farmhouse.");
        var bytes = WorldRuntimeCodec.Encode(world.ExportState());
        Assert.False((await world.AdvanceOneTickAsync(() => false)).Advanced);
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(world.ExportState()));
        using var replay = WorldRuntime.Restore(WorldRuntimeCodec.Decode(bytes), _ => new ActionCoverageRecorder());
        Assert.Equal(bytes, WorldRuntimeCodec.Encode(replay.ExportState()));
        for (var tick = 0; tick < 4; tick++)
        {
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.True((await replay.AdvanceOneTickAsync()).Advanced);
            Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()), WorldRuntimeCodec.Encode(replay.ExportState()));
        }
    }
}
