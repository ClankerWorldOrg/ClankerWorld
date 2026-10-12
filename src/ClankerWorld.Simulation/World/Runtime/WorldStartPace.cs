using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.World;

/// <summary>New-world rules only; existing saves retain their saved configs.</summary>
public enum WorldStartPace
{
    /// <summary>The original 365-day calendar, with the founders already placed.</summary>
    Legacy,
    /// <summary>The game calendar of 40-day years, with the founders already placed.</summary>
    PlacedFounders,
    /// <summary>The game calendar, with the founders placed by the player at founder setup.</summary>
    FounderSetup,
}

internal static class WorldStartPaceRules
{
    public static WorldSystemsConfig WorldSystems(WorldStartPace pace) => pace switch
    {
        WorldStartPace.Legacy => WorldSystemsConfig.Default,
        WorldStartPace.PlacedFounders or WorldStartPace.FounderSetup => WorldSystemsConfig.Default with
        {
            ContractVersion = 3,
            TicksPerDay = 360,
            CalendarOffsetTicks = 90,
            DaysPerYear = 40,
            SpringDays = 10,
            SummerDays = 10,
            AutumnDays = 10,
            WinterDays = 10,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(pace)),
    };

    public static SocietyConfig Society(WorldStartPace pace) => pace switch
    {
        WorldStartPace.Legacy => new SocietyConfig(),
        WorldStartPace.PlacedFounders or WorldStartPace.FounderSetup => new SocietyConfig(
            TicksPerWorldDay: 360,
            DaysPerWorldYear: 40,
            ContractVersion: 3,
            DayLifecycle: new SocietyDayLifecycle()),
        _ => throw new ArgumentOutOfRangeException(nameof(pace)),
    };
}
