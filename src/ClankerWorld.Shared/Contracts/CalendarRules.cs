namespace ClankerWorld.Contracts;

/// <summary>One calendar calculation for the server and its display formatter.</summary>
public static class CalendarRules
{
    public static (long DayIndex, int TickOfDay) FromTick(long tick, int ticksPerDay, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerDay);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, ticksPerDay);
        var shifted = tick % ticksPerDay + (long)offset;
        return (tick / ticksPerDay + shifted / ticksPerDay, (int)(shifted % ticksPerDay));
    }

    public static (int Season, int FirstDay) SeasonAtDay(int day, int spring, int summer, int autumn)
    {
        if (day < spring) return (0, 0);
        if (day < spring + summer) return (1, spring);
        if (day < spring + summer + autumn) return (2, spring + summer);
        return (3, spring + summer + autumn);
    }
}
