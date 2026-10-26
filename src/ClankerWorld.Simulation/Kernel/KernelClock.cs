namespace ClankerWorld.Simulation.Kernel;

/// <summary>
/// Saved integer clock arithmetic. No host wall-clock data participates.
/// </summary>
public sealed record KernelClock(long WorldTick, long DayIndex, int DayOfYear, int MinuteOfDay)
{
    public const int TicksPerDay = 1_440;
    public const int DaysPerYear = 365;
}
