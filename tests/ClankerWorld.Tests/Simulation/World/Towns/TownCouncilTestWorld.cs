using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

internal static class TownCouncilTestWorld
{
    internal const int Day = 10;

    internal static WorldRuntimeState Prepare(string seed)
    {
        using var generated = NormalPathWorld.CreateGenerated(seed, _ => new ActionCoverageRecorder(chooseIdle: true));
        for (var ordinal = 5; ordinal <= 8; ordinal++)
        {
            var setup = generated.ExportState();
            var site = setup.Towns![0].BorderTiles.First(point => setup.Map.IsBuildable(point) &&
                !setup.Map.Resources.Any(resource => resource.Position == point) &&
                !setup.Map.MapObjects.Any(item => item.Position == point) &&
                !setup.Agents.Any(person => person.Position == point));
            generated.AddAgent($"agent:{ordinal:D32}", site);
        }
        var initial = generated.ExportState();
        var society = initial.Society.Society;
        var oldDay = society.Config.TicksPerWorldDay;
        // Callers supply their own Council ledger before restoring this shorter clock.
        return initial with
        {
            WorldSystems = RegionalWeatherRules.Initialize(initial.WorldSystems! with
            {
                Config = initial.WorldSystems.Config with { TicksPerDay = Day, CalendarOffsetTicks = 0 },
                RegionalWeather = null,
            }, initial.Map),
            Society = initial.Society with
            {
                Society = society with
                {
                    Config = society.Config with { TicksPerWorldDay = Day },
                    Agents = society.Agents.Select(person => person with
                    {
                        BirthTick = person.BirthTick / oldDay * Day,
                        BirthLifeTick = person.BirthLifeTick is { } birth ? birth / oldDay * Day : null,
                    }).ToArray(),
                },
            },
        };
    }
}
