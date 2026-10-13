namespace ClankerWorld.Tests;

/// <summary>A test clock whose timers fire only when the test advances it.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private long ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(ticks);
    public override long GetTimestamp() => ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount.Ticks);
        var target = checked(ticks + amount.Ticks);
        while (timers.Where(timer => timer.Due <= target).MinBy(timer => timer.Due) is { } next)
        {
            ticks = next.Due;
            next.Fire();
        }
        ticks = target;
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public long Due { get; private set; } = long.MaxValue;
        private long periodTicks;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (disposed) return false;
            if (dueTime < Timeout.InfiniteTimeSpan || period < Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(clock.ticks + dueTime.Ticks);
            periodTicks = period.Ticks;
            return true;
        }
        public void Fire()
        {
            Due = periodTicks > 0 ? checked(Due + periodTicks) : long.MaxValue;
            callback(state);
        }
        public void Dispose() { disposed = true; Due = long.MaxValue; clock.timers.Remove(this); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

public static class TestClockWaits
{
    public static Task DelayAsync(this TimeProvider clock, TimeSpan duration, CancellationToken cancellationToken = default) =>
        Task.Delay(duration, clock, cancellationToken);
}
