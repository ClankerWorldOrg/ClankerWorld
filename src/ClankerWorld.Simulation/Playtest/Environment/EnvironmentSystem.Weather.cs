using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Playtest;

internal sealed partial class EnvironmentSystem
{
    internal WeatherKind WeatherAt(SeededMap map, GridPoint position) => WeatherRules.At(State, position, map.Height,
        WeatherRules.RegionClimate(map, position));

    internal int OutdoorExposure(SeededMap map, GridPoint position, int nightChillAtFullDarkness) => WeatherExposure(map, position) +
        nightChillAtFullDarkness * DaylightRules.DarknessBasisPoints(State) / DaylightRules.FullDarkness;

    internal int WeatherExposure(SeededMap map, GridPoint position) => WeatherAt(map, position) switch
    {
        WeatherKind.Snow => 60,
        WeatherKind.Storm => 55,
        WeatherKind.Rain => 25,
        _ => map.ClimateAt(position) switch
        {
            ClimateZone.Tropical => 0,
            ClimateZone.Polar => 55,
            ClimateZone.Cold => State.Climate.Season == SeasonKind.Winter ? 45 : 15,
            _ => State.Climate.Season == SeasonKind.Winter ? 40 : 0,
        },
    };

    internal void SyncEcologyResourceStates(IEnvironmentWorld world)
    {
        foreach (var resource in State.Ecology.Resources)
            world.SetResourceAvailability(resource.Id, resource.State == EcologyResourceState.Available && resource.Quantity > 0);
    }

    internal void AdvanceWeather(IEnvironmentWorld world)
    {
        var previousClimate = State.Climate;
        var campPosition = world.WeatherAnchor;
        var previousCampWeather = WeatherAt(world.Map, campPosition);
        // Old saves retain their exact active daily weather until the first
        // resumed tick imports it into an episode. Loading alone changes nothing.
        State = RegionalWeatherRules.Initialize(State, world.Map);
        State = WorldSystemsRules.AdvanceOneTick(State);
        SyncEcologyResourceStates(world);
        var campWeather = WeatherAt(world.Map, campPosition);
        if (previousClimate.Season != State.Climate.Season || previousCampWeather != campWeather)
            world.AppendEvent("weather_changed", $"{State.Climate.Season.ToString().ToLowerInvariant()}:{campWeather.ToString().ToLowerInvariant()}");
    }
}
