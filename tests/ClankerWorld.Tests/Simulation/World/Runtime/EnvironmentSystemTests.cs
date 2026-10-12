using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class EnvironmentSystemTests
{
    [Fact]
    public void ProposedSeasonChangeRegeneratesResourcesAndLeavesCommittedSnapshotAlone()
    {
        var config = new WorldSystemsConfig(TicksPerDay: 16, DaysPerYear: 40,
            SpringDays: 10, SummerDays: 10, AutumnDays: 10, WinterDays: 10,
            WeatherProfiles: Enum.GetValues<SeasonKind>()
                .Select(season => new WeatherProfile(season, 100, 0, 0, 0, 0)).ToArray());
        var state = WorldSystemsRules.CreateGenesis("environment-season-boundary", config,
        [
            new("stone", "construction", new(1, 0), false, 0, 3, 0, 0,
                SeasonKind.Spring, 0, EcologyResourceState.Depleted),
            new("berry", "food", new(0, 0), true, 0, 3, 2, 1,
                SeasonKind.Summer, 10, EcologyResourceState.Regenerating),
        ]) with
        { WorldTick = 159, Climate = new(159, SeasonKind.Spring, WeatherKind.Clear) };
        var committed = new EnvironmentSystem(state);
        var proposed = new EnvironmentSystem(committed.State);
        var before = WorldSystemsCodec.Encode(committed.State);
        var world = new FakeWorld();

        proposed.AdvanceWeather(world);

        Assert.Equal(160, proposed.State.WorldTick);
        Assert.Equal(SeasonKind.Summer, proposed.State.Climate.Season);
        Assert.Equal(2, proposed.State.Ecology.GetResource("berry").Quantity);
        Assert.Equal(new[] { ("berry", true), ("stone", false) }, world.Availability);
        Assert.Equal(("weather_changed", "summer:clear"), Assert.Single(world.Events));
        Assert.NotNull(proposed.State.RegionalWeather);
        Assert.Same(state, committed.State);
        Assert.Equal(before, WorldSystemsCodec.Encode(committed.State));

        var harvested = proposed.State.Ecology.GetResource("berry") with
        {
            Quantity = 0,
            State = EcologyResourceState.Regenerating,
        };
        proposed.ReplaceEcologyResource("berry", harvested);
        world.Availability.Clear();
        proposed.SyncEcologyResourceStates(world);
        Assert.Equal(new[] { ("berry", false), ("stone", false) }, world.Availability);
        Assert.Equal(2, committed.State.Ecology.Resources.Count);
        Assert.Equal(before, WorldSystemsCodec.Encode(committed.State));
    }

    private sealed class FakeWorld : IEnvironmentWorld
    {
        public WorldMap Map { get; } = new(64, 128, 0, [], [], [], "environment-fake-world")
        {
            ClimateZones = Enumerable.Repeat((byte)ClimateZone.Temperate, 64 * 128).ToArray(),
        };
        public GridPoint WeatherAnchor => new(0, 0);
        public List<(string, bool)> Availability { get; } = [];
        public List<(string, string)> Events { get; } = [];
        public void SetResourceAvailability(string id, bool available) => Availability.Add((id, available));
        public void AppendEvent(string kind, string detail) => Events.Add((kind, detail));
    }
}
