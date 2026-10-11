using System.Diagnostics;
using System.Text.Json;

namespace ClankerWorld.GodotClient.Launcher;

/// <summary>The launcher's own choices. Developer mode is off until the player turns it on.</summary>
public sealed record LauncherSettings(bool DeveloperMode = false, string? LastPlayedVersion = null,
    bool UseTestData = true, bool DetailedLogs = false)
{
    public static LauncherSettings Load(LauncherLayout layout)
    {
        try
        {
            return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllBytes(layout.SettingsPath)) ?? new();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(LauncherLayout layout)
    {
        Directory.CreateDirectory(layout.LauncherDirectory);
        var temporary = layout.SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this));
        File.Move(temporary, layout.SettingsPath, overwrite: true);
    }
}

/// <summary>A world in the player's saves and the version that last saved it, if known.</summary>
public sealed record SavedWorld(string Name, GameVersionName? SavedBy);

/// <summary>
/// Reads the game's world list without changing it, so the Versions page can
/// say which worlds still need a version. It never writes to the saves folder.
/// </summary>
public static class SavedWorlds
{
    public static IReadOnlyList<SavedWorld> Read(LauncherLayout layout)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(layout.WorldCatalogPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("Worlds", out var worlds) || worlds.ValueKind != JsonValueKind.Array)
                return [];
            return worlds.EnumerateArray()
                .Where(world => world.ValueKind == JsonValueKind.Object)
                .Select(world => new SavedWorld(
                    world.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString()! : "World",
                    world.TryGetProperty("GameVersion", out var version) && version.ValueKind == JsonValueKind.String
                        ? GameVersionName.Parse(version.GetString()) : null))
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}

/// <summary>Starts an installed game version.</summary>
public static class GameStarter
{
    public const string WorldOption = "--choose-world=";
    public const string DeveloperOptionsMarker = "# Launcher options: developer-data-v1";

    public static bool UsesTestData(IEnumerable<string> arguments) =>
        arguments.Contains("--developer-mode", StringComparer.Ordinal) && arguments.Contains("--test-data", StringComparer.Ordinal);

    public static bool UsesDetailedLogs(IEnumerable<string> arguments) =>
        arguments.Contains("--developer-mode", StringComparer.Ordinal) && arguments.Contains("--detailed-logs", StringComparer.Ordinal);

    public static string DataDirectory(string localApplicationData, IEnumerable<string> arguments)
    {
        var layout = LauncherLayout.ForUser(localApplicationData);
        return UsesTestData(arguments) ? layout.TestDataDirectory : layout.Root;
    }

    public static string ClientDataPath(string localApplicationData, IEnumerable<string> arguments, string fileName) =>
        Path.Combine(DataDirectory(localApplicationData, arguments), "client", fileName);

    /// <summary>A catalog ID is a selection hint, never a path or a request to load a world.</summary>
    public static string? WorldId(string? value) =>
        value is { Length: 32 } && value.All(char.IsAsciiHexDigit) ? value : null;

    public static string? RequestedWorld(IEnumerable<string> arguments) =>
        WorldId(arguments.FirstOrDefault(value => value.StartsWith(WorldOption, StringComparison.Ordinal))?[WorldOption.Length..]);

    /// <summary>
    /// Start options the game reads: which launcher started it, so it can
    /// offer Quit to Launcher, and whether Developer mode is on.
    /// </summary>
    public static IReadOnlyList<string> Arguments(string launcherExecutable, bool developerMode, string? worldId = null,
        bool useTestData = false, bool detailedLogs = false, string? logPath = null)
    {
        List<string> arguments = [];
        if (developerMode && detailedLogs) arguments.Add("--verbose");
        if (logPath is not null) arguments.AddRange(["--log-file", logPath]);
        arguments.AddRange(["--", "--launcher=" + launcherExecutable]);
        if (developerMode) arguments.Add("--developer-mode");
        if (developerMode && useTestData) arguments.Add("--test-data");
        if (developerMode && detailedLogs) arguments.Add("--detailed-logs");
        if (WorldId(worldId) is { } id) arguments.Add(WorldOption + id);
        return arguments;
    }

    public static Process Start(InstalledVersion version, string launcherExecutable, bool developerMode, string? worldId = null)
        => StartProcess(version.Executable, launcherExecutable, developerMode, worldId);

    public static Process Start(string executable, string launcherExecutable, LauncherSettings settings,
        LauncherLayout layout, string? worldId = null)
    {
        if (settings.DeveloperMode && (settings.UseTestData || settings.DetailedLogs))
        {
            var manifest = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executable))!, LauncherLayout.PackageManifest);
            if (!File.Exists(manifest) || !File.ReadLines(manifest).Contains(DeveloperOptionsMarker, StringComparer.Ordinal))
                throw new GameInstallException("This build doesn't support the test data folder or detailed logs. Choose a newer build, or turn these options off.");
        }
        var logs = Path.Combine(layout.GameDataDirectory(settings), "logs");
        Directory.CreateDirectory(logs);
        return StartProcess(executable, launcherExecutable, settings.DeveloperMode, worldId,
            settings.UseTestData, settings.DetailedLogs, Path.Combine(logs, "game.log"));
    }

    public static Process StartLocal(string executable, string launcherExecutable, LauncherSettings settings, LauncherLayout layout)
    {
        if (!settings.DeveloperMode) throw new GameInstallException("Turn on Developer mode to run a local build.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        if (!File.Exists(executable) || !File.Exists(Path.Combine(directory, "host", "ClankerWorld.Viewer.exe")))
            throw new GameInstallException("Choose a locally built portable game with its host folder. Your saves are safe.");
        return Start(executable, launcherExecutable, settings, layout);
    }

    private static Process StartProcess(string executable, string launcherExecutable, bool developerMode,
        string? worldId, bool useTestData = false, bool detailedLogs = false, string? logPath = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable)),
            UseShellExecute = false,
        };
        foreach (var argument in Arguments(launcherExecutable, developerMode, worldId, useTestData, detailedLogs, logPath))
            start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new GameInstallException("ClankerWorld didn't start.");
    }
}
