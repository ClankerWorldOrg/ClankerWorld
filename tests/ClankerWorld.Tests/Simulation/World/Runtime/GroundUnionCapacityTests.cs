using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class GroundUnionCapacityTests
{
    [Fact]
    public void NativeGroundUnionFitsItsAllocationBudget()
    {
        const string seed = "ground-union-native";
        using var world = new WorldRuntime(seed, startPace: WorldStartPace.FounderSetup,
            geographyOptions: new GeographyOptions(seed, WorldSizePreset.Small));
        var map = world.ExportState().Map;
        Assert.NotEmpty(map.Resources);
        IEnumerable<GridPoint>[] layers =
            [map.MapObjects.Select(item => item.Position), map.Resources.Select(item => item.Position)];
        _ = GroundOccupancy.Combine(layers);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var occupied = GroundOccupancy.Combine(layers);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 64L * 1024);
        Assert.Equal(layers.SelectMany(layer => layer).Distinct().ToArray(), occupied.ToArray());
    }

    [Fact]
    public void GroundUnionKeepsOrderAndEnumeratesUnknownLayersOnlyOnce()
    {
        var first = new HashSet<GridPoint> { new(1, 1), new(2, 2), new(3, 3) };
        first.Remove(new(1, 1));
        var calls = 0;
        IEnumerable<GridPoint> SingleUse()
        {
            Assert.Equal(0, calls++);
            yield return new(3, 3);
            yield return new(4, 4);
        }
        var occupied = GroundOccupancy.Combine(first, SingleUse(), [new GridPoint(1, 1), new(2, 2)]);
        GridPoint[] expected = [new(2, 2), new(3, 3), new(4, 4), new(1, 1)];
        Assert.Equal(expected, occupied.ToArray());
        Assert.Equal(1, calls);
    }
}
