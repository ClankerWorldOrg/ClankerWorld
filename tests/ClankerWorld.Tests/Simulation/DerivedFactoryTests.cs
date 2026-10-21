using ClankerWorld.Simulation;

namespace ClankerWorld.Tests;

public sealed class DerivedFactoryTests
{
    [Fact]
    public void FreshSnapshotLookupsUseBoundedTemporaryMemory()
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var keys = Enumerable.Range(0, 1024).Select(number => new Snapshot(number)).ToArray();
        var cache = new Derived<Snapshot, Value>(static key => new(key.Number), static (left, right) => left == right);
        Value? last = null;
        var sum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var key in keys)
        {
            last = cache.Get(key);
            sum += last.Number;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(1024 * 1023 / 2, sum);
        Assert.Same(last, cache.Get(keys[^1]));
        Assert.InRange(allocated, 1, 96L * 1024);
        GC.KeepAlive(keys);
        GC.KeepAlive(cache);
    }

    [Fact]
    public void FactoryFailuresKeepCachedIdentityAndEveryHitStillVerifies()
    {
        Assert.True(DerivedVerification.RecomputeOnHit);
        var calls = 0;
        var refuse = true;
        var failure = new InvalidOperationException("Factory refused this snapshot.");
        var cache = new Derived<Snapshot, Value>(key =>
        {
            calls++;
            if (refuse) throw failure;
            return new(key.Number);
        }, static (left, right) => left == right);
        var key = new Snapshot(7);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => cache.Get(key)));
        Assert.Equal(1, calls);
        refuse = false;
        var stored = cache.Get(key);
        Assert.Equal(2, calls);
        Assert.Same(stored, cache.Get(key));
        Assert.Equal(3, calls);
        Assert.NotSame(stored, cache.Get(new(7)));
        Assert.Equal(4, calls);
        refuse = true;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => cache.Get(key)));
        Assert.Equal(5, calls);
        refuse = false;
        Assert.Same(stored, cache.Get(key));
        Assert.Equal(6, calls);
    }

    private sealed record Snapshot(int Number);
    private sealed record Value(int Number);
}
