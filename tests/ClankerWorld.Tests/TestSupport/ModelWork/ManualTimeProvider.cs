namespace ClankerWorld.Tests;

/// <summary>A test clock whose timers fire only when the test advances it.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object gate = new();
    private readonly object advanceGate = new();
    private readonly List<ManualTimer> timers = [];
    private long ticks;
    public override DateTimeOffset GetUtcNow()
    {
        lock (gate) return DateTimeOffset.UnixEpoch.AddTicks(ticks);
    }
    public override long GetTimestamp()
    {
        lock (gate) return ticks;
    }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            var timer = new ManualTimer(this, callback, state);
            timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
    }

    public void Advance(TimeSpan amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount.Ticks);
        lock (advanceGate)
        {
            long target;
            lock (gate) target = checked(ticks + amount.Ticks);
            while (true)
            {
                ManualTimer? next;
                lock (gate)
                {
                    next = timers.Where(timer => timer.Due <= target).MinBy(timer => timer.Due);
                    if (next is null)
                    {
                        ticks = target;
                        return;
                    }
                    ticks = next.Due;
                    next.ScheduleNext();
                }
                // HTTP completion may dispose a timer on another thread. Keep
                // callbacks outside the timer-state lock so it can finish.
                next.Fire();
            }
        }
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public long Due { get; private set; } = long.MaxValue;
        private long periodTicks;
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.gate)
            {
                if (disposed) return false;
                if (dueTime < Timeout.InfiniteTimeSpan || period < Timeout.InfiniteTimeSpan)
                    throw new ArgumentOutOfRangeException(nameof(dueTime));
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(clock.ticks + dueTime.Ticks);
                periodTicks = period.Ticks;
                return true;
            }
        }
        public void ScheduleNext() => Due = periodTicks > 0 ? checked(Due + periodTicks) : long.MaxValue;
        public void Fire() => callback(state);
        public void Dispose()
        {
            lock (clock.gate)
            {
                disposed = true;
                Due = long.MaxValue;
                clock.timers.Remove(this);
            }
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

public static class TestClockWaits
{
    public static Task DelayAsync(this TimeProvider clock, TimeSpan duration, CancellationToken cancellationToken = default) =>
        Task.Delay(duration, clock, cancellationToken);
}
