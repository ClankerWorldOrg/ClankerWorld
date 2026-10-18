using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ClankerWorld.GodotClient.Diagnostics;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.Tests;

public sealed class LauncherTests
{
    private static readonly ECDsa SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private const string Download = "https://github.com/ClankerWorldOrg/ClankerWorld/releases/download/";
    private const string Page = "https://github.com/ClankerWorldOrg/ClankerWorld/releases/tag/";
    private static readonly (string Path, string Text) EntryPoints =
        ("package.json", """{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}""");

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RestartRetainsTheWholeLastRunAndReportsBothLogs(bool developerMode, bool useTestData)
    {
        var root = Directory.CreateTempSubdirectory("launcher log retention ");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            var settings = new LauncherSettings(DeveloperMode: developerMode, UseTestData: useTestData);
            var directory = Path.Combine(layout.GameDataDirectory(settings), "logs");
            var current = GameStarter.PrepareGameLog(directory);
            var previous = Path.Combine(directory, "game.previous.log");
            Assert.False(File.Exists(current));
            Assert.False(File.Exists(previous));
            var firstRun = string.Concat(Enumerable.Repeat("resource loaded\n", 1000)) + "last line before forced close\n";
            File.WriteAllText(current, firstRun);
            Assert.Equal(current, GameStarter.PrepareGameLog(directory));
            Assert.Equal(firstRun, File.ReadAllText(previous));
            // A failed start creates no current log; trying again must keep the crash evidence.
            GameStarter.PrepareGameLog(directory);
            Assert.Equal(firstRun, File.ReadAllText(previous));
            File.WriteAllText(current, "next run reached the world\n");
            var report = ProblemReport.Write(Path.Combine(root.FullName, "reports"), [],
                [new("game.log", current), new("game.previous.log", previous)], [], DateTimeOffset.UtcNow);
            using (var archive = ZipFile.OpenRead(report))
            {
                using var last = new StreamReader(archive.GetEntry("logs/game.previous.log")!.Open());
                using var live = new StreamReader(archive.GetEntry("logs/game.log")!.Open());
                Assert.Equal(firstRun, last.ReadToEnd());
                Assert.Equal("next run reached the world\n", live.ReadToEnd());
            }
            GameStarter.PrepareGameLog(directory);
            Assert.Equal("next run reached the world\n", File.ReadAllText(previous));
            Assert.Equal([previous], Directory.GetFiles(directory));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task LocalBuildRunsWithSeparateArgumentBoundariesAndUnsupportedOptionsAreRefused(bool detailed, bool nested)
    {
        var root = Directory.CreateTempSubdirectory("launcher local build ");
        try
        {
            var localApplicationData = Path.Combine(root.FullName, "player data");
            var layout = LauncherLayout.ForUser(localApplicationData);
            var settings = new LauncherSettings(DeveloperMode: true, DetailedLogs: detailed);
            settings.Save(layout);
            Assert.Equal(settings, LauncherSettings.Load(layout));
            var packageRoot = Path.Combine(root.FullName, "local game");
            var gamePath = nested ? "bin/ClankerWorld.exe" : "ClankerWorld.exe";
            var executable = Path.Combine(packageRoot, gamePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            Directory.CreateDirectory(Path.Combine(packageRoot, "server"));
            File.WriteAllText(executable, "local executable");
            Assert.Throws<GameInstallException>(() => GameStarter.StartLocal(executable, "launcher.exe", settings, layout));
            File.WriteAllText(Path.Combine(packageRoot, EntryPoints.Path), System.Text.Json.JsonSerializer.Serialize(
                new { game = gamePath, server = "server/ClankerWorld.Server.exe" }));
            File.WriteAllText(Path.Combine(packageRoot, "server", "ClankerWorld.Server.exe"), "server");
            Assert.Throws<GameInstallException>(() => GameStarter.StartLocal(executable, "launcher.exe", settings, layout));
            Assert.False(Directory.Exists(layout.TestDataDirectory));
            Assert.Throws<GameInstallException>(() => GameStarter.StartLocal(executable, "launcher.exe",
                settings with { DeveloperMode = false }, layout));
            File.WriteAllText(Path.Combine(packageRoot, LauncherLayout.PackageManifest),
                GameStarter.DeveloperOptionsMarker + "\n");
            var log = Path.Combine(layout.TestDataDirectory, "logs", "game.log");
            var arguments = GameStarter.Arguments("launcher with spaces.exe", true, useTestData: true, detailedLogs: detailed, logPath: log);
            Assert.Equal(layout.TestDataDirectory, GameStarter.DataDirectory(localApplicationData, arguments));
            Assert.Equal(detailed, arguments.Contains("--verbose"));
            Assert.Equal(["--", "--launcher=launcher.exe"], GameStarter.Arguments("launcher.exe", false,
                useTestData: true, detailedLogs: true));
            if (OperatingSystem.IsLinux())
            {
                File.WriteAllText(executable, "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$(dirname \"$0\")/arguments.txt\"\n");
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.WriteAllText(log, "last line from the earlier run\n");
                using var process = GameStarter.StartLocal(executable, "launcher with spaces.exe", settings, layout);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(arguments, File.ReadAllLines(Path.Combine(Path.GetDirectoryName(executable)!, "arguments.txt")));
                Assert.Equal("last line from the earlier run\n", File.ReadAllText(Path.ChangeExtension(log, "previous.log")));
                Assert.False(File.Exists(log));
            }
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("ClankerWorld.exe", "bin/World Server.exe")]
    [InlineData("bin/ClankerWorld.exe", "server/ClankerWorld.Server.exe")]
    [InlineData("programs/Game files/New Game.exe", "runtime/World Server.exe")]
    public void EachVersionNamesItsOwnEntryPointsInPackageJson(string game, string server)
    {
        var root = Directory.CreateTempSubdirectory("launcher-entry-points-");
        try
        {
            using var http = new HttpClient();
            var store = new GameVersionStore(new LauncherLayout(root.FullName), http);
            var version = GameVersionName.Parse("0.1.0-alpha.3")!;
            var zip = Path.Combine(root.FullName, "package.zip");
            File.WriteAllBytes(zip, Package(version, (game, "game"),
                ("package.json", System.Text.Json.JsonSerializer.Serialize(new { game, server })),
                (server, "server")));
            var installed = store.InstallPackage(zip, version);
            Assert.Empty(GameVersionStore.Verify(installed));
            var gamePath = game.Replace('/', Path.DirectorySeparatorChar);
            var serverPath = server.Replace('/', Path.DirectorySeparatorChar);
            Assert.Equal(new PackageEntryPoints(gamePath, serverPath),
                PackageEntryPoints.ReadInstalled(installed.Directory));
            Assert.Equal(Path.Combine(installed.Directory, gamePath), installed.Executable);
            var localData = Path.Combine(root.FullName, "player data");
            var bundled = LocalServerLayout.FindBundled(installed.Executable, localData);
            Assert.NotNull(bundled);
            Assert.Equal(Path.Combine(installed.Directory, serverPath), bundled.FileName);
            Assert.Equal(Path.GetDirectoryName(bundled.FileName), bundled.ServerDirectory);
            Assert.Equal(Path.Combine(localData, "ClankerWorld"), bundled.DataDirectory);

            // A server the manifest does not list, or an unsafe path, is damage, never a different program.
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"),
                ("package.json", """{"game": "ClankerWorld.exe", "server": "server/ClankerWorld.Server.exe"}""")));
            Assert.Throws<GameInstallException>(() => store.InstallPackage(zip, version));
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"),
                ("package.json", """{"game": "ClankerWorld.exe", "server": "../outside.exe"}""")));
            Assert.Throws<GameInstallException>(() => store.InstallPackage(zip, version));
            Assert.Empty(GameVersionStore.Verify(Assert.Single(store.Installed())));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("host/ClankerWorld.Viewer.exe")]
    [InlineData("host/ClankerWorld.Server.exe")]
    public void AVersionWithoutPackageJsonKeepsTheServerPathItShippedWith(string server)
    {
        var root = Directory.CreateTempSubdirectory("launcher-older-version-");
        try
        {
            using var http = new HttpClient();
            var store = new GameVersionStore(new LauncherLayout(root.FullName), http);
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var zip = Path.Combine(root.FullName, "package.zip");
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"), (server, "server")));
            var installed = store.InstallPackage(zip, version);
            var serverPath = server.Replace('/', Path.DirectorySeparatorChar);
            Assert.Empty(GameVersionStore.Verify(installed));
            Assert.Equal(new PackageEntryPoints("ClankerWorld.exe", serverPath), PackageEntryPoints.ReadInstalled(installed.Directory));
            Assert.Equal(Path.Combine(installed.Directory, "ClankerWorld.exe"), installed.Executable);
            var bundled = LocalServerLayout.FindBundled(installed.Executable, root.FullName);
            Assert.NotNull(bundled);
            Assert.Equal(Path.Combine(installed.Directory, serverPath), bundled.FileName);

            File.WriteAllText(Path.Combine(installed.Directory, serverPath), "changed");
            Assert.Equal([serverPath], GameVersionStore.Verify(installed));
            File.Delete(Path.Combine(installed.Directory, serverPath));
            Assert.Equal([serverPath], GameVersionStore.Verify(installed));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void VersionsSortNewestReleaseFirstAndOnlySafeNamesParse()
    {
        string[] ordered = ["0.1.0-alpha.1", "0.1.0-alpha.2", "0.1.0-alpha.10", "0.1.0-beta", "0.1.0", "0.2.0"];
        var parsed = ordered.Select(text => GameVersionName.Parse(text)!).ToArray();
        Assert.Equal(ordered, parsed.Reverse().OrderByDescending(version => version).Reverse().Select(version => version.ToString()));
        foreach (var unsafeName in new[] { "../0.1.0", "0.1", "v0.1.0", "0.1.0-", "0.1.0-a/b", "01.0.0", "0.1.0 " })
            Assert.Null(GameVersionName.Parse(unsafeName));
    }

    [Fact]
    public void TheFeedKeepsCompleteGameReleasesAndTheNewestLauncher()
    {
        var feed = GameReleaseFeed.Read(
        [
            Release("v0.1.0-alpha.1"),
            Release("v0.1.0-alpha.2"),
            Release("v0.1.0-alpha.3") with { Draft = true },
            Release("v0.1.0-alpha.4") with { Assets = Release("v0.1.0-alpha.4").Assets!.Take(1).ToArray() },
            Release("v0.1.0-alpha.5") with
            {
                Assets = Release("v0.1.0-alpha.5").Assets!.Select(asset => asset with
                {
                    BrowserDownloadUrl = asset.BrowserDownloadUrl!.Replace("github.com", "example.com", StringComparison.Ordinal),
                }).ToArray(),
            },
            new GitHubRelease("launcher-v1.0.0", Page + "launcher-v1.0.0", false, false, []),
            new GitHubRelease("launcher-v1.2.0", Page + "launcher-v1.2.0", false, false, []),
        ]);

        Assert.Equal(["0.1.0-alpha.2", "0.1.0-alpha.1"], feed.Games.Select(release => release.Version.ToString()));
        Assert.Equal("1.2.0", feed.NewestLauncher?.Version.ToString());
    }

    [Fact]
    public void NightlyBuildsAreOfferedOnlyInDeveloperMode()
    {
        var feed = GameReleaseFeed.Read([Release("v0.1.0-nightly.20261011.4") with { PreRelease = true }, Release("v0.1.0-alpha.1")]);

        Assert.True(feed.Games[0].Version.IsNightly);
        Assert.False(feed.Games[1].Version.IsNightly);
        Assert.Equal(["0.1.0-alpha.1"], GameReleaseFeed.Offered(feed.Games, developerMode: false).Select(release => release.Version.ToString()));
        Assert.Equal(2, GameReleaseFeed.Offered(feed.Games, developerMode: true).Count);
    }

    [Fact]
    public async Task InstallChecksTheDownloadAndRepairAndRemoveNeverTouchSaves()
    {
        var root = Directory.CreateTempSubdirectory("launcher-install-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            Directory.CreateDirectory(layout.SavesDirectory);
            var save = Path.Combine(layout.SavesDirectory, "world.json");
            File.WriteAllText(save, "world");
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var package = Package(version, ("ClankerWorld.exe", "game"), EntryPoints, ("server/ClankerWorld.Server.exe", "host"));
            var server = new FakeReleases();
            var release = server.Publish(version, package);
            using var http = new HttpClient(server);
            var store = new GameVersionStore(layout, http, new TestReleaseSignatureVerifier(SigningKey));

            // A download that doesn't match its published hash is never unpacked.
            server.Tamper = true;
            var refused = await Assert.ThrowsAsync<GameInstallException>(() => store.InstallAsync(release, null, CancellationToken.None));
            Assert.Contains("didn't match the published file", refused.Message, StringComparison.Ordinal);
            Assert.Empty(store.Installed());
            server.Tamper = false;

            var installed = await store.InstallAsync(release, null, CancellationToken.None);
            Assert.Equal(version, installed.Version);
            Assert.Equal(layout.VersionDirectory(version), installed.Directory);
            Assert.Equal("0123456789abcdef0123456789abcdef01234567", installed.SourceRevision);
            Assert.True(installed.SizeBytes > 0);
            Assert.Empty(GameVersionStore.Verify(installed));

            File.WriteAllText(Path.Combine(installed.Directory, "server", "ClankerWorld.Server.exe"), "changed");
            Assert.Equal([Path.Combine("server", "ClankerWorld.Server.exe")], GameVersionStore.Verify(installed));
            var repaired = await store.InstallAsync(release, null, CancellationToken.None);
            Assert.Empty(GameVersionStore.Verify(repaired));
            Assert.Single(Directory.EnumerateDirectories(layout.VersionsDirectory));

            store.Remove(repaired);
            Assert.Empty(store.Installed());
            Assert.Equal("world", File.ReadAllText(save));
            Assert.Empty(Directory.EnumerateFileSystemEntries(layout.DownloadsDirectory));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("ClankerWorld-0.1.0-alpha.1-windows-x64/../escaped.txt")]
    [InlineData("elsewhere/ClankerWorld.exe")]
    public void APackageThatWritesOutsideItsFolderIsRefused(string entryName)
    {
        var root = Directory.CreateTempSubdirectory("launcher-unsafe-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var zip = Path.Combine(root.FullName, "package.zip");
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "game"), EntryPoints, ("server/ClankerWorld.Server.exe", "host")));
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
            using (var writer = new StreamWriter(archive.CreateEntry(entryName).Open()))
                writer.Write("escaped");

            using var http = new HttpClient();
            Assert.Throws<GameInstallException>(() => new GameVersionStore(layout, http).InstallPackage(zip, version));
            Assert.False(File.Exists(Path.Combine(layout.VersionsDirectory, "escaped.txt")));
            Assert.Empty(Directory.EnumerateDirectories(layout.VersionsDirectory));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void SavedWorldsNameTheVersionThatLastSavedThemAndStartOptionsCarryDeveloperMode()
    {
        var root = Directory.CreateTempSubdirectory("launcher-worlds-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            Assert.Empty(SavedWorlds.Read(layout));
            Directory.CreateDirectory(Path.GetDirectoryName(layout.WorldCatalogPath)!);
            File.WriteAllText(layout.WorldCatalogPath,
                """{"ActiveId":"a","Worlds":[{"Name":"Old","GameVersion":"0.1.0-alpha.1"},{"Name":"Unknown"}]}""");
            var before = File.GetLastWriteTimeUtc(layout.WorldCatalogPath);

            var worlds = SavedWorlds.Read(layout);

            Assert.Equal([("Old", "0.1.0-alpha.1"), ("Unknown", (string?)null)],
                worlds.Select(world => (world.Name, world.SavedBy?.ToString())));
            Assert.Equal(before, File.GetLastWriteTimeUtc(layout.WorldCatalogPath));
            Assert.Equal(["--", "--launcher=L.exe"], GameStarter.Arguments("L.exe", developerMode: false));
            Assert.Equal(["--", "--launcher=L.exe", "--developer-mode"], GameStarter.Arguments("L.exe", developerMode: true));
            foreach (var invalidRoot in new[] { "[]", "null", "42", "{\"Worlds\":null}" })
            {
                File.WriteAllText(layout.WorldCatalogPath, invalidRoot);
                Assert.Empty(SavedWorlds.Read(layout));
                Assert.Equal(invalidRoot, File.ReadAllText(layout.WorldCatalogPath));
            }
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void SelectedWorldStartOptionsPreserveTheCatalogIdAndRejectPathsOrOtherValues()
    {
        const string id = "0123456789abcdef0123456789abcdef";
        var arguments = GameStarter.Arguments("launcher with spaces.exe", developerMode: true, id);
        Assert.Equal(["--", "--launcher=launcher with spaces.exe", "--developer-mode", "--choose-world=" + id], arguments);
        Assert.Equal(id, GameStarter.RequestedWorld(arguments));
        Assert.Equal(id.ToUpperInvariant(), GameStarter.RequestedWorld(["--choose-world=" + id.ToUpperInvariant()]));
        foreach (var invalid in new[] { "", "../world", id + "0", id[..31], new string('g', 32), id + "\n--developer-mode" })
        {
            Assert.Null(GameStarter.RequestedWorld(["--choose-world=" + invalid]));
            Assert.Equal(["--", "--launcher=L.exe"], GameStarter.Arguments("L.exe", false, invalid));
        }
        Assert.Null(GameStarter.RequestedWorld(["--choose-version=0.1.0", "--choose-world"]));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("headers")]
    [InlineData("partial")]
    [InlineData("short")]
    [InlineData("unsafe")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("missing-executable")]
    [InlineData("missing-both")]
    [InlineData("unlisted")]
    public async Task DamagedFileListsAndUnlistedFilesNeedRepairAndReinstallPreservesSaves(string damage)
    {
        var root = Directory.CreateTempSubdirectory("launcher-manifest-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            Directory.CreateDirectory(layout.SavesDirectory);
            var save = Path.Combine(layout.SavesDirectory, "world.json");
            File.WriteAllText(save, "saved world");
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var server = new FakeReleases();
            var release = server.Publish(version, Package(version, ("ClankerWorld.exe", "game"), EntryPoints, ("server/ClankerWorld.Server.exe", "host")));
            using var http = new HttpClient(server);
            var store = new GameVersionStore(layout, http, new TestReleaseSignatureVerifier(SigningKey));
            var installed = await store.InstallAsync(release, null, CancellationToken.None);
            var manifestPath = Path.Combine(installed.Directory, LauncherLayout.PackageManifest);
            var manifest = File.ReadAllText(manifestPath);
            var entries = manifest.Split('\n').Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray();
            if (damage is "missing" or "missing-both")
            {
                File.Delete(manifestPath);
                if (damage == "missing-both") File.Delete(installed.Executable);
            }
            else if (damage == "missing-executable") File.Delete(installed.Executable);
            else if (damage == "unlisted") File.WriteAllText(Path.Combine(installed.Directory, "server", "unlisted.dll"), "unexpected program");
            else
            {
                File.WriteAllText(Path.Combine(installed.Directory, "server", "ClankerWorld.Server.exe"), "changed host");
                File.WriteAllText(manifestPath, damage switch
                {
                    "empty" => "",
                    "headers" => "# package\n# Source commit: retained\n",
                    "partial" => entries.Single(line => line.EndsWith("./ClankerWorld.exe", StringComparison.Ordinal)) + "\n",
                    "short" => "incomplete checksum\n",
                    "unsafe" => manifest.Replace("*./", "*../", StringComparison.Ordinal),
                    "duplicate" => manifest + entries[0] + "\n",
                    _ => throw new InvalidOperationException(damage),
                });
            }

            var damaged = Assert.Single(store.Installed());
            Assert.Equal(installed.Version, damaged.Version);
            Assert.NotEmpty(GameVersionStore.Verify(damaged));
            var repaired = await store.InstallAsync(release, null, CancellationToken.None);
            Assert.Empty(GameVersionStore.Verify(repaired));
            Assert.Equal("host", File.ReadAllText(Path.Combine(repaired.Directory, "server", "ClankerWorld.Server.exe")));
            Assert.False(File.Exists(Path.Combine(repaired.Directory, "server", "unlisted.dll")));
            Assert.Equal("saved world", File.ReadAllText(save));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InterruptedReplacementRecoversItsOldCopyButRemovalStaysRemoved(bool replacementPresent, bool removal)
    {
        var root = Directory.CreateTempSubdirectory("launcher-recovery-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            Directory.CreateDirectory(layout.SavesDirectory);
            var save = Path.Combine(layout.SavesDirectory, "world.json");
            File.WriteAllText(save, "saved world");
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var zip = Path.Combine(root.FullName, "package.zip");
            File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "old game"), EntryPoints, ("server/ClankerWorld.Server.exe", "old host")));
            using var http = new HttpClient();
            var store = new GameVersionStore(layout, http, new TestReleaseSignatureVerifier(SigningKey));
            var old = store.InstallPackage(zip, version);
            // The actual directory-move boundary after retiring the old copy.
            var retired = Path.Combine(layout.VersionsDirectory, $".{(removal ? "removed" : "old")}-{version}-{Guid.NewGuid():N}");
            Directory.Move(old.Directory, retired);
            if (replacementPresent)
            {
                File.WriteAllBytes(zip, Package(version, ("ClankerWorld.exe", "new game"), EntryPoints, ("server/ClankerWorld.Server.exe", "new host")));
                _ = store.InstallPackage(zip, version);
            }
            Directory.CreateDirectory(Path.Combine(layout.VersionsDirectory, ".staging-" + Guid.NewGuid().ToString("N")));

            store.CleanUp();
            store.CleanUp();
            if (removal) Assert.Empty(store.Installed());
            else
            {
                var recovered = Assert.Single(store.Installed());
                Assert.Equal(version, recovered.Version);
                Assert.Equal(replacementPresent ? "new game" : "old game", File.ReadAllText(recovered.Executable));
                Assert.Empty(GameVersionStore.Verify(recovered));
            }
            Assert.False(Directory.Exists(retired));
            Assert.DoesNotContain(Directory.EnumerateDirectories(layout.VersionsDirectory),
                path => Path.GetFileName(path).StartsWith('.'));
            Assert.Equal("saved world", File.ReadAllText(save));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public async Task ACheckedDownloadWithAnIncompleteFileListCannotReplaceTheInstalledCopy()
    {
        var root = Directory.CreateTempSubdirectory("launcher-invalid-package-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            var version = GameVersionName.Parse("0.1.0-alpha.1")!;
            var package = Package(version, ("ClankerWorld.exe", "game"), EntryPoints, ("server/ClankerWorld.Server.exe", "host"));
            var server = new FakeReleases();
            using var http = new HttpClient(server);
            var store = new GameVersionStore(layout, http, new TestReleaseSignatureVerifier(SigningKey));
            var installed = await store.InstallAsync(server.Publish(version, package), null, CancellationToken.None);
            using var damaged = new MemoryStream();
            damaged.Write(package);
            using (var archive = new ZipArchive(damaged, ZipArchiveMode.Update, leaveOpen: true))
            {
                var entry = archive.GetEntry(GameReleaseFeed.PackageName(version) + "/manifest.sha256")!;
                string manifest;
                using (var reader = new StreamReader(entry.Open())) manifest = reader.ReadToEnd();
                entry.Delete();
                using var writer = new StreamWriter(archive.CreateEntry(GameReleaseFeed.PackageName(version) + "/manifest.sha256").Open());
                writer.WriteLine(manifest.Split('\n').Single(line => line.EndsWith("./ClankerWorld.exe", StringComparison.Ordinal)));
            }
            // The published ZIP hash is correct; its internal file list is incomplete.
            var release = server.Publish(version, damaged.ToArray());
            await Assert.ThrowsAsync<GameInstallException>(() => store.InstallAsync(release, null, CancellationToken.None));
            Assert.Equal("game", File.ReadAllText(installed.Executable));
            Assert.Empty(GameVersionStore.Verify(installed));
            Assert.Single(store.Installed());
            Assert.Single(Directory.EnumerateDirectories(layout.VersionsDirectory));
        }
        finally { root.Delete(recursive: true); }
    }

    private static GitHubRelease Release(string tag)
    {
        var name = GameReleaseFeed.PackageName(GameVersionName.Parse(tag[1..])!) + ".zip";
        return new GitHubRelease(tag, Page + tag, false, true,
        [
            new GitHubReleaseAsset(name, 1000, Download + tag + "/" + name),
            new GitHubReleaseAsset(name + ".sha256", 100, Download + tag + "/" + name + ".sha256"),
        ]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("bad-signature")]
    [InlineData("wrong-key")]
    [InlineData("wrong-package")]
    [InlineData("wrong-hash")]
    [InlineData("wrong-manifest")]
    public async Task UnsignedOrUnapprovedDownloadsCannotReplaceAnInstalledGame(string failure)
    {
        var root = Directory.CreateTempSubdirectory("launcher-signature-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            var version = GameVersionName.Parse("0.1.0-alpha.2")!;
            using var server = new FakeReleases();
            using var http = new HttpClient(server);
            var store = new GameVersionStore(layout, http, new TestReleaseSignatureVerifier(SigningKey));
            var old = await store.InstallAsync(server.Publish(version, Package(version,
                ("ClankerWorld.exe", "old game"), EntryPoints, ("server/ClankerWorld.Server.exe", "server"))), null, CancellationToken.None);
            GameVersionStore.RequireSigned(old, new TestReleaseSignatureVerifier(SigningKey));
            var before = File.ReadAllBytes(Path.Combine(old.Directory, ReleasePackageSignature.InstalledFile));
            server.SignatureFailure = failure;
            var release = server.Publish(version, Package(version,
                ("ClankerWorld.exe", "replacement"), EntryPoints, ("server/ClankerWorld.Server.exe", "server")));
            await Assert.ThrowsAsync<GameInstallException>(() => store.InstallAsync(release, null, CancellationToken.None));
            Assert.Equal("old game", File.ReadAllText(old.Executable));
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(old.Directory, ReleasePackageSignature.InstalledFile)));
            GameVersionStore.RequireSigned(old, new TestReleaseSignatureVerifier(SigningKey));
            Assert.Single(Directory.GetDirectories(layout.VersionsDirectory));
            Assert.Empty(Directory.GetFiles(layout.DownloadsDirectory));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("missing-proof")]
    [InlineData("bad-proof")]
    [InlineData("manifest")]
    [InlineData("program")]
    public async Task PlayAndRepairRequireTheSignedManifestAndItsOriginalFiles(string damage)
    {
        var root = Directory.CreateTempSubdirectory("launcher-signed-play-");
        try
        {
            var layout = new LauncherLayout(root.FullName);
            Directory.CreateDirectory(layout.SavesDirectory);
            var save = Path.Combine(layout.SavesDirectory, "world.json");
            File.WriteAllText(save, "world");
            var version = GameVersionName.Parse("0.1.0-alpha.2")!;
            using var server = new FakeReleases();
            using var http = new HttpClient(server);
            var store = new GameVersionStore(layout, http, new TestReleaseSignatureVerifier(SigningKey));
            var release = server.Publish(version, Package(version,
                ("ClankerWorld.exe", "game"), EntryPoints, ("server/ClankerWorld.Server.exe", "server")));
            var installed = await store.InstallAsync(release, null, CancellationToken.None);
            var verifier = new TestReleaseSignatureVerifier(SigningKey);
            GameVersionStore.RequireSigned(installed, verifier);
            Assert.False(store.NeedsRepair(installed));
            // The production workflow verifier must never trust this ECDSA-only fixture.
            Assert.Throws<GameInstallException>(() => GameVersionStore.RequireSigned(installed));
            if (damage == "missing-proof") File.Delete(Path.Combine(installed.Directory, ReleasePackageSignature.InstalledFile));
            else if (damage == "bad-proof") File.WriteAllText(Path.Combine(installed.Directory, ReleasePackageSignature.InstalledFile), "not json");
            else File.AppendAllText(damage == "manifest" ? Path.Combine(installed.Directory, LauncherLayout.PackageManifest) : installed.Executable, "changed");
            Assert.Throws<GameInstallException>(() => GameVersionStore.RequireSigned(installed, verifier));
            Assert.True(store.NeedsRepair(installed));
            var repaired = await store.InstallAsync(release, null, CancellationToken.None);
            GameVersionStore.RequireSigned(repaired, verifier);
            Assert.False(store.NeedsRepair(repaired));
            Assert.Equal("world", File.ReadAllText(save));
        }
        finally { root.Delete(recursive: true); }
    }

    /// <summary>A package laid out like scripts/package-windows.sh makes it, manifest included.</summary>
    private static byte[] Package(GameVersionName version, params (string Path, string Text)[] files)
    {
        var folder = GameReleaseFeed.PackageName(version) + "/";
        var manifest = new StringBuilder()
            .Append("# ").Append(GameReleaseFeed.PackageName(version)).Append('\n')
            .Append("# Source commit: 0123456789abcdef0123456789abcdef01234567\n");
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in files)
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                using (var stream = archive.CreateEntry(folder + path).Open()) stream.Write(bytes);
                manifest.Append(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()).Append(" *./").Append(path).Append('\n');
            }
            using var writer = new StreamWriter(archive.CreateEntry(folder + "manifest.sha256").Open());
            writer.Write(manifest.ToString());
        }
        return memory.ToArray();
    }

    private sealed class FakeReleases : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        public bool Tamper { get; set; }
        public string? SignatureFailure { get; set; }

        public GameRelease Publish(GameVersionName version, byte[] package)
        {
            var tag = "v" + version;
            var name = GameReleaseFeed.PackageName(version) + ".zip";
            files[Download + tag + "/" + name] = package;
            using var archive = new ZipArchive(new MemoryStream(package));
            using var manifest = archive.GetEntry(GameReleaseFeed.PackageName(version) + "/manifest.sha256")!.Open();
            var proof = new ReleasePackageSignature(name, Convert.ToHexString(SHA256.HashData(package)),
                Convert.ToHexString(SHA256.HashData(manifest)), "");
            proof = SignatureFailure switch
            {
                "wrong-package" => proof with { Package = "another-release.zip" },
                "wrong-hash" => proof with { PackageSha256 = new string('0', 64) },
                "wrong-manifest" => proof with { ManifestSha256 = new string('0', 64) },
                _ => proof,
            };
            proof = proof with
            {
                Signature = Convert.ToBase64String(SigningKey.SignData(proof.Payload(), HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence))
            };
            if (SignatureFailure == "bad-signature") proof = proof with { Signature = Convert.ToBase64String(new byte[64]) };
            if (SignatureFailure == "wrong-key")
            {
                using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                proof = proof with
                {
                    Signature = Convert.ToBase64String(otherKey.SignData(proof.Payload(), HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
                };
            }
            files[Download + tag + "/" + name + ".sig"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(proof);
            if (SignatureFailure == "missing") files.Remove(Download + tag + "/" + name + ".sig");
            files[Download + tag + "/" + name + ".sha256"] = Encoding.ASCII.GetBytes(
                Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant() + " *" + name + "\n");
            return GameReleaseFeed.Read([Release(tag) with
            {
                Assets = [new GitHubReleaseAsset(name, package.Length, Download + tag + "/" + name),
                    new GitHubReleaseAsset(name + ".sha256", 100, Download + tag + "/" + name + ".sha256"),
                    new GitHubReleaseAsset(name + ".sig", 500, Download + tag + "/" + name + ".sig")],
            }]).Games.Single();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!files.TryGetValue(request.RequestUri!.ToString(), out var bytes))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if (Tamper && request.RequestUri.ToString().EndsWith(".zip", StringComparison.Ordinal))
            {
                bytes = (byte[])bytes.Clone();
                bytes[^1] ^= 1;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
