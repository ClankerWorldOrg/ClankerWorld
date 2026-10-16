using System.Numerics;
using System.Text.Json;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.GodotClient.Protocol;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Observation;

namespace ClankerWorld.Tests;

public sealed class GameUiTextTests
{
    private static readonly JsonSerializerOptions HostJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions GameJson = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData("future_internal_diagnostic", false)]
    [InlineData("saved_road_footprints_repaired", false)]
    [InlineData("tick_advanced", false)]
    [InlineData("tree_planting_refused", false)]
    [InlineData("instruction_applied", false)]
    [InlineData("housing_blocked", true)]
    [InlineData("relocation_notice", true)]
    [InlineData("relocation_cancelled", true)]
    [InlineData("household_delivery_recovered", false)]
    [InlineData("market_built", true)]
    [InlineData("market_stall_built", true)]
    [InlineData("market_stall_borrowed", true)]
    [InlineData("market_stock_loaded", false)]
    [InlineData("market_stock_delivered", false)]
    [InlineData("market_stock_collected", false)]
    [InlineData("market_stall_left", true)]
    [InlineData("market_trade_offered", true)]
    [InlineData("market_trade_completed", true)]
    [InlineData("market_trade_cancelled", true)]
    [InlineData("town_project_approved", true)]
    [InlineData("town_project_blocked", true)]
    [InlineData("town_project_resumed", true)]
    [InlineData("town_project_cancelled", true)]
    [InlineData("town_project_donated", true)]
    [InlineData("town_project_material_picked_up", false)]
    [InlineData("town_project_material_delivered", false)]
    [InlineData("town_project_material_returned", false)]
    [InlineData("town_project_worked", false)]
    [InlineData("town_project_completed", true)]
    [InlineData("housing_answer_recorded", false)]
    [InlineData("housing_request_cancelled", false)]
    [InlineData("model_call_warning", true)]
    public void EventLogSelectsKnownPlayerEventsInsteadOfPublishingUnknownDiagnostics(string kind, bool visible)
    {
        Assert.Equal(visible, GameUiText.IsPlayerFacingEvent(kind));
    }

    [Theory]
    [InlineData("  Alexandria   Smith  ", "Alexandria")]
    [InlineData("Álexandriannnnnnnn", "Á.")]
    [InlineData("李 小龙", "李")]
    [InlineData("  ", "?")]
    public void MapNamesUseWholeGivenNamesOrWholeTextElementInitials(string fullName, string expected) =>
        Assert.Equal(expected, GameUiText.ActorMapLabel(fullName));

    [Fact]
    public void PlayerFailuresDescribeRecoveryWithoutExposingRawExceptionText()
    {
        const string secret = "sk-private /home/private/save provider-response";
        Exception[] failures = [new InvalidOperationException(secret), new IOException(secret),
            new UnauthorizedAccessException(secret), new ArgumentException(secret),
            new System.Text.Json.JsonException(secret), new System.Security.Cryptography.CryptographicException(secret),
            new HttpRequestException(secret, null, System.Net.HttpStatusCode.Forbidden),
            new TaskCanceledException(secret)];
        foreach (var failure in failures)
        {
            var message = ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(failure);
            Assert.DoesNotContain(secret, message, StringComparison.Ordinal);
            Assert.DoesNotContain(failure.GetType().Name, message, StringComparison.Ordinal);
            Assert.NotEmpty(message);
        }
        Assert.Contains("connecting", ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(failures[6]), StringComparison.Ordinal);
        Assert.Contains("storage", ClankerWorld.GodotClient.UI.GameUiFailures.FriendlyFailure(failures[1]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WorldStartPace.Legacy)]
    [InlineData(WorldStartPace.PlacedFounders)]
    [InlineData(WorldStartPace.FounderSetup)]
    public void OwnerSnapshotReportsTheSavedWorldCalendarPace(WorldStartPace startPace)
    {
        using var world = new WorldRuntime("calendar-projection", startPace: startPace);
        var config = world.ExportState().WorldSystems!.Config;
        var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
        var pace = snapshot.CalendarPace;
        Assert.Equal(new CalendarPaceView(config.TicksPerDay, config.DaysPerYear,
            config.SpringDays, config.SummerDays, config.AutumnDays, config.WinterDays, config.CalendarOffsetTicks), pace);

        // The game reads the season lengths the host sends rather than keeping
        // its own copy, so every date names the season the world is in.
        var client = JsonSerializer.Deserialize<OwnerWorldSnapshot>(JsonSerializer.Serialize(snapshot, HostJson), GameJson)!;
        var received = client.CalendarPace;
        Assert.Equal(0, client.WorldTick);
        Assert.Equal(startPace == WorldStartPace.Legacy ? DaylightRules.FullDarkness : 0, client.DarknessBasisPoints);
        Assert.Equal(startPace == WorldStartPace.Legacy ? "Spring 1, Year 1 · 00:00" : "Spring 1, Year 1 · 06:00",
            GameUiText.FormatWorldClock(client.WorldTick, calendarPace: received));
        Assert.True(GameUiText.ShowsSeasonDates(received, GameUiText.SeasonDates));
        for (var day = 0; day < config.DaysPerYear * 2; day++)
        {
            var tick = (long)day * config.TicksPerDay + config.TicksPerDay / 2 - config.CalendarOffsetTicks;
            var calendar = WorldCalendarRules.FromTick(tick, config);
            Assert.Equal($"{calendar.Season} {calendar.DayOfSeason + 1}, Year {day / config.DaysPerYear + 1} · 12:00",
                GameUiText.FormatWorldClock(tick, calendarPace: received));
        }
    }

    [Theory]
    [InlineData(0, "Spring 1, Year 1 · 06:00")]
    [InlineData(1, "Spring 1, Year 1 · 06:04")]
    [InlineData(269, "Spring 1, Year 1 · 23:56")]
    [InlineData(270, "Spring 2, Year 1 · 00:00")]
    [InlineData(3509, "Spring 10, Year 1 · 23:56")]
    [InlineData(3510, "Summer 1, Year 1 · 00:00")]
    [InlineData(14309, "Winter 10, Year 1 · 23:56")]
    [InlineData(14310, "Spring 1, Year 2 · 00:00")]
    public void MorningCalendarFormatsRawHistoryTicksAcrossDateBoundaries(long tick, string expected)
    {
        var calendar = new OwnerWorldCalendarPace(360, 40, 10, 10, 10, 10, CalendarOffsetTicks: 90);
        Assert.Equal(expected, GameUiText.FormatWorldClock(tick, calendarPace: calendar));
    }

    [Fact]
    public void MorningOffsetAppliesToEveryDateStyleAndOldHostsKeepMidnight()
    {
        var calendar = new OwnerWorldCalendarPace(360, 40, 10, 10, 10, 10, CalendarOffsetTicks: 90);
        Assert.Equal("Spring 1, Year 1 · 6:00 AM", GameUiText.FormatWorldClock(0, true, calendar));
        Assert.Equal("02-01-0001 · 00:00", GameUiText.FormatWorldClock(270, calendarPace: calendar, dateFormat: "dmy"));
        Assert.Equal("01-02-0001 · 12:00 AM", GameUiText.FormatWorldClock(270, true, calendar, "mdy"));
        Assert.Equal("0001-01-02 · 00:00", GameUiText.FormatWorldClock(270, calendarPace: calendar, dateFormat: "ymd"));
        var oldHost = JsonSerializer.Deserialize<OwnerWorldCalendarPace>(
            """{"ticksPerDay":360,"daysPerYear":40,"springDays":10,"summerDays":10,"autumnDays":10,"winterDays":10}""", GameJson)!;
        Assert.Equal(0, oldHost.CalendarOffsetTicks);
        Assert.Equal("Spring 1, Year 1 · 00:00", GameUiText.FormatWorldClock(0, calendarPace: oldHost));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameUiText.FormatWorldClock(0, calendarPace: calendar with { CalendarOffsetTicks = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameUiText.FormatWorldClock(0, calendarPace: calendar with { CalendarOffsetTicks = 360 }));
    }

    [Theory]
    [InlineData(360, 90)]
    [InlineData(int.MaxValue, int.MaxValue - 1)]
    public void CalendarOffsetFormatsTheLargestRepresentableTickWithoutOverflow(int ticksPerDay, int offset)
    {
        var calendar = new OwnerWorldCalendarPace(ticksPerDay, 40, 10, 10, 10, 10, offset);
        var shifted = (BigInteger)long.MaxValue + offset;
        var day = shifted / ticksPerDay;
        var dayOfYear = (int)(day % 40);
        var minutes = (int)((shifted % ticksPerDay) * 1440 / ticksPerDay);
        var season = new[] { "Spring", "Summer", "Autumn", "Winter" }[dayOfYear / 10];
        var expected = $"{season} {dayOfYear % 10 + 1}, Year {day / 40 + 1} · {minutes / 60:00}:{minutes % 60:00}";
        Assert.Equal(expected, GameUiText.FormatWorldClock(long.MaxValue, calendarPace: calendar));
    }

    [Fact]
    public void SeasonDatesAreTheDefaultAndWorkWithBothClocks()
    {
        var calendar = new OwnerWorldCalendarPace(360, 40, 10, 10, 10, 10);
        // Day 22 of the year at 14:20 is the second day of Autumn.
        const long autumnAfternoon = 21 * 360 + 215;
        Assert.Equal("Autumn 2, Year 1 · 14:20", GameUiText.FormatWorldClock(autumnAfternoon, calendarPace: calendar));
        Assert.Equal("Autumn 2, Year 1 · 2:20 PM", GameUiText.FormatWorldClock(autumnAfternoon,
            useTwelveHourClock: true, calendarPace: calendar));
        Assert.Equal("Autumn 2, Year 1 · 14:20", GameUiText.FormatWorldClock(autumnAfternoon,
            calendarPace: calendar, dateFormat: new GameDisplayPreferences().DateStyle));
        Assert.Equal("Spring 1, Year 1 · 12:00 AM", GameUiText.FormatWorldClock(0,
            useTwelveHourClock: true, calendarPace: calendar));
        Assert.Equal("Winter 10, Year 1 · 23:56", GameUiText.FormatWorldClock(14_399, calendarPace: calendar));
        Assert.Equal("Spring 1, Year 2 · 00:00", GameUiText.FormatWorldClock(14_400, calendarPace: calendar));
        Assert.Equal("Summer 10, Year 12 · 12:00 PM", GameUiText.FormatWorldClock(11 * 14_400 + 19 * 360 + 180,
            useTwelveHourClock: true, calendarPace: calendar));

        // Numeric orders keep both clocks, and the season date is not shown.
        Assert.Equal("02-03-0001 · 14:20", GameUiText.FormatWorldClock(autumnAfternoon,
            calendarPace: calendar, dateFormat: "dmy"));
        Assert.Equal("03-02-0001 · 2:20 PM", GameUiText.FormatWorldClock(autumnAfternoon,
            useTwelveHourClock: true, calendarPace: calendar, dateFormat: "mdy"));
        Assert.Equal("0001-03-02 · 14:20", GameUiText.FormatWorldClock(autumnAfternoon,
            calendarPace: calendar, dateFormat: "ymd"));
        Assert.False(GameUiText.ShowsSeasonDates(calendar, "dmy"));
        Assert.True(GameUiText.ShowsSeasonDates(calendar, GameUiText.SeasonDates));
        Assert.True(GameUiText.ShowsSeasonDates(calendar, "an-unknown-style"));

        // Without the world's season lengths (an older host, or lengths that
        // do not fill the year), dates fall back to numbers instead of guessing.
        foreach (var unknown in new[] { new OwnerWorldCalendarPace(360, 40), calendar with { WinterDays = 9 }, calendar with { SpringDays = 0, SummerDays = 20 } })
        {
            Assert.False(GameUiText.ShowsSeasonDates(unknown, GameUiText.SeasonDates));
            Assert.Equal("02-03-0001 · 14:20", GameUiText.FormatWorldClock(autumnAfternoon, calendarPace: unknown));
        }
        Assert.False(GameUiText.ShowsSeasonDates(null, GameUiText.SeasonDates));
    }

    [Fact]
    public void ResourceHelpDistinguishesExhaustionRegrowthAndLegacyUnknownQuantities()
    {
        var wood = new OwnerWorldResource("wood", "construction", new(0, 0), false, "depleted", 0, 12, 0, 0, "spring");
        Assert.Equal("Wild timber · 0/12\nDepleted\nDoes not grow back.", GameUiText.ResourceTooltip(wood));
        var berries = wood with { Id = "food", Kind = "food", IsRenewable = true, RegenerationAmount = 4, RegenerationIntervalDays = 1 };
        Assert.Contains("+4 every 1 day in Spring", GameUiText.ResourceTooltip(berries), StringComparison.Ordinal);
        Assert.Equal("Soil 3/3", GameUiText.ResourceQuantity("fertile_land", 3, 3));
        var legacy = new OwnerWorldResource("old", "food", new(0, 0), true, "available");
        Assert.Contains("Grows back.", GameUiText.ResourceTooltip(legacy), StringComparison.Ordinal);
        Assert.Equal(string.Empty, GameUiText.ResourceMapCaption(legacy, 16));
        Assert.Equal("Food", GameUiText.ResourceMapCaption(legacy, 48));
        Assert.Equal("Wood 0/12", GameUiText.ResourceMapCaption(wood, 48));
        Assert.Equal("Plot Soil 3/3", GameUiText.ResourceMapCaption(wood with { Kind = "fertile_land", Quantity = 3, Capacity = 3 }, 48));
        Assert.DoesNotContain("0/", GameUiText.ResourceTooltip(legacy), StringComparison.Ordinal);
    }

    [Fact]
    public void WorldClockUsesDefaultAndCustomCalendarBoundariesAndScalesWorldTicks()
    {
        Assert.Equal("01-02-0001 · 00:00", GameUiText.FormatWorldClock(44_640));
        var calendar = new OwnerWorldCalendarPace(360, 40);
        Assert.Equal("01-01-0001 · 00:04", GameUiText.FormatWorldClock(1, calendarPace: calendar));
        Assert.Equal("01-02-0001 · 00:00", GameUiText.FormatWorldClock(3_600, calendarPace: calendar));
        Assert.Equal("01-01-0002 · 00:00", GameUiText.FormatWorldClock(14_400, calendarPace: calendar));
        Assert.Equal("02-01-0002 · 12:00 PM", GameUiText.FormatWorldClock(14_940,
            useTwelveHourClock: true, calendarPace: calendar));
        Assert.Equal("01-02-0002 · 12:00", GameUiText.FormatWorldClock(14_940,
            calendarPace: calendar, dateFormat: "mdy"));
        Assert.Equal("0002-01-02 · 12:00", GameUiText.FormatWorldClock(14_940,
            calendarPace: calendar, dateFormat: "ymd"));
    }

    [Theory]
    [InlineData("seek_food", "looking for food", "looking for food")]
    [InlineData("build:building:sha256:abcdef/building/stone-hearth@1.0.0", "build:building:sha256:abcdef/building/stone-hearth@1.0.0", "build Stone hearth")]
    [InlineData(null, null, "taking in the surroundings")]
    public void RosterActivitiesReadAsShortPhrases(string? candidate, string? summary, string expected)
    {
        Assert.Equal(expected, GameUiText.ActivityPhrase(candidate, summary));
    }

    [Fact]
    public void PartyNamesUseAgentHouseholdAndTownNamesInsteadOfIds()
    {
        var position = new OwnerWorldPosition(0, 0);
        var agent = new OwnerWorldAgent("founder:1", "Rowan", "active", position, 8_000, [], [],
            new OwnerWorldRoute("idle", null, null, [], string.Empty),
            new OwnerWorldSpatialKnowledge(position, [position], [position]), false);
        var snapshot = new OwnerWorldSnapshot("names", 0, "names-map", [], [], [], null, 0)
        {
            Agents = [agent],
            Stockpiles = [new OwnerWorldStockpile("household:first", "First household", [])],
            Towns = [new OwnerWorldTown("town:first", "First Town", "founding", 0, [], [], [])],
        };
        Assert.Equal("Rowan", GameUiText.PartyName(snapshot, "founder:1"));
        Assert.Equal("First household", GameUiText.PartyName(snapshot, "household:first"));
        Assert.Equal("First Town", GameUiText.PartyName(snapshot, "town:first"));
        Assert.Equal("a household", GameUiText.PartyName(snapshot, "household:agent:123"));
        Assert.DoesNotContain("household:", GameUiText.PartyName(null, "household:second"), StringComparison.Ordinal);
    }
}
