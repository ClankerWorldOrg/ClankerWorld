using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.UI;

namespace ClankerWorld.Tests;

public sealed class GameDisplayPreferencesStoreTests
{
    [Fact]
    public void GameClockPreferencePersistsOutsideWorldSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "clankerworld-display-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new GameDisplayPreferencesStore(Path.Combine(directory, "game-settings.json"));
            Assert.False(store.Load().UseTwelveHourClock);
            store.Save(new GameDisplayPreferences(UseTwelveHourClock: true,
                WindowWidth: 1600, WindowHeight: 900, Fullscreen: false));
            var restored = new GameDisplayPreferencesStore(Path.Combine(directory, "game-settings.json")).Load();
            Assert.True(restored.UseTwelveHourClock);
            Assert.Equal("01-01-0001 · 12:00 AM", GameUiText.FormatWorldClock(0,
                useTwelveHourClock: restored.UseTwelveHourClock));
            Assert.Equal("01-01-0001 · 11:59 PM", GameUiText.FormatWorldClock(1_439,
                useTwelveHourClock: restored.UseTwelveHourClock));
            Assert.Equal((1600, 900), (restored.WindowWidth, restored.WindowHeight));
            Assert.False(restored.UsesFullscreen);
            Assert.Equal(GameUiText.SeasonDates, restored.DateStyle);
            store.Save(restored with { DateStyle = "ymd" });
            Assert.Equal("ymd", store.Load().DateStyle);
            Assert.Equal((1600, 900), (store.Load().WindowWidth, store.Load().WindowHeight));
            Assert.False(store.Load().UsesFullscreen);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OlderSavedInterfaceAndRenderChoicesAreIgnored()
    {
        Assert.True(new GameDisplayPreferences().UsesFullscreen);
        var directory = Directory.CreateTempSubdirectory("clanker-display-ui-scale-");
        try
        {
            var path = Path.Combine(directory.FullName, "game-settings.json");
            // The older DateFormat entry held "dmy" whether or not the player
            // chose it, so season dates replace it rather than guessing.
            File.WriteAllText(path, "{\"UiScalePercent\":400,\"RenderWidth\":1920,\"RenderHeight\":1080,\"Theme\":\"dark\",\"DateFormat\":\"dmy\"}");
            var store = new GameDisplayPreferencesStore(path);
            Assert.Equal("dark", store.Load().Theme);
            Assert.Equal(GameUiText.SeasonDates, store.Load().DateStyle);
            store.Save(store.Load());
            Assert.DoesNotContain("UiScalePercent", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.DoesNotContain("RenderWidth", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.DoesNotContain("DateFormat", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void TheInterfaceDropsASizeRatherThanSqueezeTheMenus()
    {
        Assert.Equal(1, DisplayUiScalePolicy.FittingFactor(1700, 1080));
        Assert.Equal(2, DisplayUiScalePolicy.FittingFactor(2560, 1600));
    }

    [Fact]
    public void ThePictureFollowsTheScreenInFullscreenAndTheWindowOtherwise()
    {
        var monitor = new DisplayDimensions(2560, 1440);
        Assert.Equal(monitor, DisplayResolutionPolicy.AutomaticRenderSize(
            monitor, new DisplayDimensions(1600, 900), fullscreen: true));
        Assert.Equal(new DisplayDimensions(1600, 900), DisplayResolutionPolicy.AutomaticRenderSize(
            monitor, new DisplayDimensions(1600, 900), fullscreen: false));
    }
}
