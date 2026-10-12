using System.Text;
using ClankerWorld.Simulation.World;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Tests;

public sealed class MorningWorldStartTests
{
    [Fact]
    public async Task GeneratedMorningSetupKeepsRawTickZeroThroughLayoutRedoFounderUndoAndStart()
    {
        const string seed = "morning-start-audit";
        using var world = new WorldRuntime(seed, _ => new ActionCoverageRecorder(chooseIdle: true),
            startPace: WorldStartPace.FounderSetup,
            geographyOptions: new GeographyOptions(seed, WorldSizePreset.Small));
        AssertMorning(world);
        Assert.True(world.Society.IsPaused);
        Assert.False(world.FounderSetup!.Started);
        Assert.Empty(world.Agents);
        Assert.False((await world.AdvanceOneTickAsync()).Advanced);

        world.InitializeFirstTownContent();
        var map = world.ExportState().Map;
        var site = NormalPathWorld.FindStartingTownSite(map);
        world.AcceptFirstTownLayout(site);
        world.AcceptFirstTownLayout(site);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "first_town_layout_redone");
        AssertMorning(world);

        var positions = world.RoadTiles.Take(WorldRuntime.RequiredFounders).ToArray();
        Assert.Equal(WorldRuntime.RequiredFounders, positions.Length);
        for (var index = 0; index < positions.Length; index++)
            world.PlaceFounder($"founder:{index + 1:D32}", positions[index]);
        var lastId = world.FounderSetup.FounderIds[^1];
        var lastBirth = world.Society.GetAgent(lastId).BirthTick;
        Assert.Equal(3, world.UndoLastFounder(lastId));
        world.PlaceFounder(lastId, positions[^1]);
        Assert.Equal(lastBirth, world.Society.GetAgent(lastId).BirthTick);
        AssertMorning(world);
        for (var index = 0; index < world.FounderSetup.FounderIds.Count; index++)
        {
            var person = world.Society.GetAgent(world.FounderSetup.FounderIds[index]);
            Assert.Equal(SocietyRules.FounderArrivalAge(world.Society.Config, seed, index),
                world.Society.AgeAt(person, 0));
        }
        Assert.All(world.ExportState().Events, item => Assert.Equal(0, item.WorldTick));
        var zeroOffsetState = world.ExportState();
        zeroOffsetState = zeroOffsetState with
        {
            WorldSystems = zeroOffsetState.WorldSystems! with
            {
                SchemaVersion = 2,
                Config = zeroOffsetState.WorldSystems.Config with { ContractVersion = 2, CalendarOffsetTicks = 0 },
            },
        };
        var zeroOffsetBytes = WorldRuntimeCodec.Encode(zeroOffsetState);
        using var existingClock = WorldRuntime.Restore(WorldRuntimeCodec.Decode(zeroOffsetBytes));
        Assert.Equal(zeroOffsetBytes, WorldRuntimeCodec.Encode(existingClock.ExportState()));
        Assert.Equal(0, WorldCalendarRules.FromTick(existingClock.WorldTick, existingClock.WorldSystems.Config).TickOfDay);
        Assert.Equal(world.Society.Agents.Select(person => (person.BirthTick, person.BirthLifeTick, world.Society.AgeAt(person, 0))),
            existingClock.Society.Agents.Select(person => (person.BirthTick, person.BirthLifeTick, existingClock.Society.AgeAt(person, 0))));
        var encoded = WorldRuntimeCodec.Encode(world.ExportState());
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded),
            _ => new ActionCoverageRecorder(chooseIdle: true));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        AssertMorning(restored);
        Assert.Equal(world.Society.Agents.Select(person => person.BirthTick),
            restored.Society.Agents.Select(person => person.BirthTick));

        world.StartWorld();
        restored.StartWorld();
        AssertMorning(world);
        Assert.True(world.FounderSetup.Started);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.True((await restored.AdvanceOneTickAsync()).Advanced);
        Assert.Equal(1, world.WorldTick);
        Assert.Equal(91, WorldCalendarRules.FromTick(world.WorldTick, world.WorldSystems.Config).TickOfDay);
        Assert.Equal(WorldRuntimeCodec.Encode(world.ExportState()),
            WorldRuntimeCodec.Encode(restored.ExportState()));
    }

    [Fact]
    public void ModernPresetStartsAtSixWhileExistingZeroOffsetSaveKeepsItsClockAndBytes()
    {
        using var modern = new WorldRuntime("morning-modern-preset", startPace: WorldStartPace.PlacedFounders);
        AssertMorning(modern);
        Assert.NotEmpty(modern.Agents);
        using var legacy = new WorldRuntime("morning-legacy-save");
        var before = legacy.ExportState();
        Assert.Equal(0, before.WorldSystems!.Config.CalendarOffsetTicks);
        Assert.Equal(2, before.WorldSystems.SchemaVersion);
        var encoded = WorldRuntimeCodec.Encode(before);
        Assert.DoesNotContain("calendarOffsetTicks", Encoding.UTF8.GetString(encoded), StringComparison.OrdinalIgnoreCase);
        using var restored = WorldRuntime.Restore(WorldRuntimeCodec.Decode(encoded));
        Assert.Equal(encoded, WorldRuntimeCodec.Encode(restored.ExportState()));
        Assert.Equal(0, WorldCalendarRules.FromTick(restored.WorldTick, restored.WorldSystems.Config).TickOfDay);
        Assert.Equal(DaylightRules.FullDarkness, DaylightRules.DarknessBasisPoints(restored.WorldSystems));
        Assert.Equal(before.Society.Society.Agents.Select(person => (person.BirthTick, person.BirthLifeTick, person.AgeBand)),
            restored.Society.Agents.Select(person => (person.BirthTick, person.BirthLifeTick, person.AgeBand)));
    }

    [Fact]
    public void CivicDeadlineLabelsCountCalendarDaysFromTheMorningStart()
    {
        // Ballot, mayoral-round and land-grant deadlines are all told to agents through this day number.
        using var morning = new WorldRuntime("morning-civic-days", startPace: WorldStartPace.PlacedFounders);
        Assert.Equal(90, morning.WorldSystems.Config.CalendarOffsetTicks);
        Assert.Equal(1, morning.CivicDayNumber(0));
        Assert.Equal(1, morning.CivicDayNumber(269));
        Assert.Equal(2, morning.CivicDayNumber(270));
        Assert.Equal(2, morning.CivicDayNumber(360));
        Assert.Equal(3, morning.CivicDayNumber(630));

        using var legacy = new WorldRuntime("morning-civic-days-legacy");
        var day = legacy.WorldSystems.Config.TicksPerDay;
        Assert.Equal(0, legacy.WorldSystems.Config.CalendarOffsetTicks);
        Assert.Equal(1, legacy.CivicDayNumber(day - 1));
        Assert.Equal(2, legacy.CivicDayNumber(day));
    }

    private static void AssertMorning(WorldRuntime world)
    {
        Assert.Equal(0, world.WorldTick);
        Assert.Equal(0, world.WorldSystems.WorldTick);
        Assert.Equal(360, world.WorldSystems.Config.TicksPerDay);
        Assert.Equal(90, world.WorldSystems.Config.CalendarOffsetTicks);
        Assert.Equal(3, world.WorldSystems.SchemaVersion);
        var calendar = WorldCalendarRules.FromTick(world.WorldTick, world.WorldSystems.Config);
        Assert.Equal(0, calendar.WorldTick);
        Assert.Equal(0, calendar.DayIndex);
        Assert.Equal(0, calendar.DayOfYear);
        Assert.Equal(SeasonKind.Spring, calendar.Season);
        Assert.Equal(90, calendar.TickOfDay);
        Assert.Equal(0, DaylightRules.DarknessBasisPoints(world.WorldSystems));
    }
}
