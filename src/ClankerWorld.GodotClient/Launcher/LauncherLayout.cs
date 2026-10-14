using System.Text.Json;

namespace ClankerWorld.GodotClient.Launcher;

/// <summary>
/// Where the launcher keeps game versions and its own settings. Everything sits
/// under the same per-user folder the game's local server uses for saves, keys and
/// logs (<c>%LOCALAPPDATA%\ClankerWorld</c>), but in separate subfolders, so
/// installing, repairing or removing a version never touches player data.
/// </summary>
public sealed record LauncherLayout(string Root)
{
    public const string GameExecutable = "ClankerWorld.exe";
    public const string PackageManifest = "manifest.sha256";

    public static LauncherLayout ForUser(string localApplicationData) =>
        new(Path.Combine(localApplicationData, "ClankerWorld"));

    public string VersionsDirectory => Path.Combine(Root, "versions");
    public string LauncherDirectory => Path.Combine(Root, "launcher");
    public string DownloadsDirectory => Path.Combine(LauncherDirectory, "downloads");
    public string SettingsPath => Path.Combine(LauncherDirectory, "settings.json");
    public string TestDataDirectory => Path.Combine(LauncherDirectory, "test-data");
    public string GameDataDirectory(LauncherSettings settings) =>
        settings.DeveloperMode && settings.UseTestData ? TestDataDirectory : Root;

    /// <summary>The game's own data folders, written by its host (#1564).</summary>
    public string SavesDirectory => Path.Combine(Root, "saves");
    public string LogsDirectory => Path.Combine(Root, "logs");
    public string WorldCatalogPath => Path.Combine(SavesDirectory, "world.json.worlds", "catalog.json");

    public string VersionDirectory(GameVersionName version) => Path.Combine(VersionsDirectory, version.ToString());
}

/// <summary>
/// The programs in one game version's folder, as its own <c>package.json</c>
/// names them: <c>{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}</c>.
/// The launcher reads them from the version it checks or starts, so a newer
/// package can move or rename its programs without making installed versions
/// look damaged. A version packaged before <c>package.json</c> has its server
/// in <c>host/</c>, under the name it had then.
/// </summary>
public sealed record PackageEntryPoints(string Game, string Server)
{
    public const string FileName = "package.json";

    private static readonly string[] OlderServers =
        [Path.Combine("host", "ClankerWorld.Viewer.exe"), Path.Combine("host", "ClankerWorld.Server.exe")];

    /// <summary>
    /// A version folder's entry points as paths relative to it, or null when
    /// its <c>package.json</c> cannot be read or names an unsafe path. Without
    /// one, the server is the first older path <paramref name="isPresent"/>
    /// accepts, or the oldest if none is.
    /// </summary>
    public static PackageEntryPoints? Read(string directory, Func<string, bool> isPresent)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return new(LauncherLayout.GameExecutable, OlderServers.FirstOrDefault(isPresent) ?? OlderServers[0]);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                RelativePath(document.RootElement, "game") is { } game &&
                RelativePath(document.RootElement, "server") is { } server
                    ? new(game, server) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>The entry points of a version on disk, as when starting it.</summary>
    public static PackageEntryPoints? ReadInstalled(string directory) =>
        Read(directory, relative => File.Exists(Path.Combine(directory, relative)));

    /// <summary>Find the installed package that names this game, including a game in a subfolder.</summary>
    public static (string Directory, PackageEntryPoints EntryPoints)? FindInstalledForGame(string executable)
    {
        var game = Path.GetFullPath(executable);
        if (Path.GetDirectoryName(game) is not { } parent) return null;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var directory = new DirectoryInfo(parent); directory is not null; directory = directory.Parent)
        {
            var hasManifest = File.Exists(Path.Combine(directory.FullName, FileName));
            if (ReadInstalled(directory.FullName) is not { } entryPoints)
            {
                if (hasManifest) return null;
                continue;
            }
            if (!string.Equals(Path.GetFullPath(Path.Combine(directory.FullName, entryPoints.Game)), game, comparison) ||
                !hasManifest && !File.Exists(Path.Combine(directory.FullName, entryPoints.Server))) continue;
            return (directory.FullName, entryPoints);
        }
        return null;
    }

    private static string? RelativePath(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        value.GetString() is { } relative && GameVersionStore.IsSafeRelativePath(relative)
            ? relative.Replace('/', Path.DirectorySeparatorChar) : null;
}
