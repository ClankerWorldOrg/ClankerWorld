using System.Collections.Frozen;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class TownLayoutSnapshotTests
{
    [Fact]
    public void ImmutableWalkingCostsFitTheContextAllocationBudget()
    {
        var map = Meadow(64);
        var town = new TownRuntimeState("town:layout", "Layout", "founded", 0, [], [], [new GridPoint(2, 2)]);
        var costs = map.Tiles.ToFrozenDictionary(tile => tile.Position, _ => 100);
        _ = new TownLayoutContext(map, town, [], costs, [], []);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var context = new TownLayoutContext(map, town, [], costs, [], []);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 1, 64L * 1024);
        Assert.Equal(costs.Count, context.ReachableFootCosts.Count);
        Assert.All(costs, pair => Assert.Equal(pair.Value, context.ReachableFootCosts[pair.Key]));
    }

    [Fact]
    public void MutableWalkingCostsCannotChangeAnExistingContext()
    {
        var map = Meadow(8);
        var costs = map.Tiles.ToDictionary(tile => tile.Position, _ => 100);
        var context = new TownLayoutContext(map, null, [], costs, [], []);
        costs[new(2, 2)] = 9_000;
        costs.Clear();
        Assert.Equal(64, context.ReachableFootCosts.Count);
        Assert.Equal(100, context.ReachableFootCosts[new(2, 2)]);
        Assert.Contains(new GridPoint(2, 2), context.CandidateAnchors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImmutableWalkingCostsStillRejectInvalidPositionsAndValues(bool outside)
    {
        var map = Meadow(8);
        var costs = new Dictionary<GridPoint, int> { [outside ? new(8, 0) : new(2, 2)] = outside ? 100 : -1 }
            .ToFrozenDictionary();
        var error = Assert.Throws<ArgumentException>(() => new TownLayoutContext(map, null, [], costs, [], []));
        Assert.Equal("reachableFootCosts", error.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WalkingCostSnapshotsKeepOrdinaryCoordinateEquality(bool frozen)
    {
        var map = Meadow(8);
        var stored = new GridPoint(2, 2);
        var unlisted = new GridPoint(2, 3);
        var comparer = EqualityComparer<GridPoint>.Create((left, right) => left.X == right.X, point => point.X);
        var mutable = new Dictionary<GridPoint, int>(comparer) { [stored] = 100 };
        IReadOnlyDictionary<GridPoint, int> input = frozen ? mutable.ToFrozenDictionary(comparer) : mutable;
        Assert.True(input.ContainsKey(unlisted));
        Assert.DoesNotContain(input.Keys, point => point == unlisted);
        var town = new TownRuntimeState("town:layout", "Layout", "founded", 0, [], [], [stored]);
        var context = new TownLayoutContext(map, town, [], input, [], []);
        Assert.Contains(unlisted, context.CandidateAnchors);
        Assert.Single(context.ReachableFootCosts);
        Assert.Equal(100, context.ReachableFootCosts[stored]);
        Assert.False(context.ReachableFootCosts.ContainsKey(unlisted));
        Assert.False(context.ReachableFootCosts.TryGetValue(unlisted, out _));
    }

    private static WorldMap Meadow(int size) => new(size, size, 0,
        (from y in Enumerable.Range(0, size)
         from x in Enumerable.Range(0, size)
         select new TerrainTile(new GridPoint(x, y), TerrainKind.Meadow)).ToArray(),
        [], [], "layout-snapshot-component");
}
