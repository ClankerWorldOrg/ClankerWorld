using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.Tests;

public sealed class HostingInstallationTests
{
    [Fact]
    public void HostingUsesVerifiedFilesAndTheSameInstallationMeterAndKeepsTestDataSeparate()
    {
        var directory = Directory.CreateTempSubdirectory("hosting-installation-");
        try
        {
            var package = Path.Combine(directory.FullName, "version");
            Directory.CreateDirectory(Path.Combine(package, "server"));
            File.WriteAllText(Path.Combine(package, "ClankerWorld.exe"), "synthetic game");
            File.WriteAllText(Path.Combine(package, "server", "ClankerWorld.Server.exe"), "synthetic server");
            File.WriteAllText(Path.Combine(package, "package.json"), """{"game":"ClankerWorld.exe","server":"server/ClankerWorld.Server.exe"}""");
            var manifest = string.Join('\n', Directory.GetFiles(package, "*", SearchOption.AllDirectories).Select(file =>
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) + " *" + Path.GetRelativePath(package, file)));
            var manifestPath = Path.Combine(package, LauncherLayout.PackageManifest);
            File.WriteAllText(manifestPath, manifest);
            var version = new InstalledVersion(GameVersionName.Parse("0.1.0-alpha.2")!, package, 0, new string('a', 40));
            var launcher = new LauncherLayout(Path.Combine(directory.FullName, "data"));
            Directory.CreateDirectory(Path.Combine(launcher.Root, "settings"));
            var meter = Path.Combine(launcher.Root, "settings", "provider-usage.json");
            File.WriteAllText(meter, "retained installation accounting");
            var retained = File.ReadAllBytes(meter);
            Assert.Throws<GameInstallException>(() => LocalServerLayout.ForHosting(version, launcher, false));
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var verifier = new TestReleaseSignatureVerifier(key);
            ReleasePackageSignature SignManifest()
            {
                var unsigned = new ReleasePackageSignature(GameReleaseFeed.PackageName(version.Version) + ".zip", new string('b', 64),
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath))), "");
                return unsigned with
                {
                    Signature = Convert.ToBase64String(key.SignData(unsigned.Payload(), HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
                };
            }
            var approvalPath = Path.Combine(package, ReleasePackageSignature.InstalledFile);
            File.WriteAllText(approvalPath, JsonSerializer.Serialize(SignManifest()));
            var unsupported = Assert.Throws<GameInstallException>(() => LocalServerLayout.ForHosting(version, launcher, false, verifier));
            Assert.Contains("doesn't support hosting", unsupported.Message, StringComparison.Ordinal);
            File.AppendAllText(manifestPath, "\n" + LocalServerLayout.HostingMarker + "\n");
            File.Delete(approvalPath);
            Assert.Throws<GameInstallException>(() => LocalServerLayout.ForHosting(version, launcher, false));
            var approval = SignManifest();
            File.WriteAllText(approvalPath, JsonSerializer.Serialize(approval with { Signature = "invalid" }));
            Assert.Throws<GameInstallException>(() => LocalServerLayout.ForHosting(version, launcher, false, verifier));
            File.WriteAllText(approvalPath, JsonSerializer.Serialize(approval));
            Assert.Throws<GameInstallException>(() => LocalServerLayout.ForHosting(version, launcher, false));
            var host = LocalServerLayout.ForHosting(version, launcher, true, verifier);
            Assert.Equal(launcher.Root, host.DataDirectory);
            Assert.Equal(Path.Combine(launcher.Root, "settings"), host.SettingsDirectory);
            Assert.Equal("launcher", host.HostKind);
            Assert.True(host.StopWhenGameQuits);
            Assert.Equal(retained, File.ReadAllBytes(meter));
            var settings = new LauncherSettings(DeveloperMode: true, UseTestData: true, HostedUseTestData: true);
            Assert.Equal(launcher.TestDataDirectory, launcher.HostingDataDirectory(settings, restoring: false));
            settings = settings with { DeveloperMode = false, UseTestData = false };
            Assert.Equal(launcher.Root, launcher.HostingDataDirectory(settings, restoring: false));
            Assert.Equal(launcher.TestDataDirectory, launcher.HostingDataDirectory(settings, restoring: true));
            File.AppendAllText(Path.Combine(package, "server", "ClankerWorld.Server.exe"), "changed after installation");
            Assert.Throws<GameInstallException>(() => LocalServerLayout.ForHosting(version, launcher, false, verifier));
            Assert.Equal(retained, File.ReadAllBytes(meter));
        }
        finally { directory.Delete(recursive: true); }
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HostedPlayKeepsTheActualHostDataAndIgnoresAnUnrelatedRemoteChoice(bool developer, bool hostTestData)
    {
        var localData = Path.Combine(Path.GetTempPath(), "hosting-play-data");
        var layout = LauncherLayout.ForUser(localData);
        var saved = new LauncherSettings(DeveloperMode: developer, UseTestData: !hostTestData,
            JoinRemoteWorld: true, HostedUseTestData: hostTestData);
        var game = saved.ForHostedGame();
        Assert.False(game.JoinRemoteWorld);
        Assert.Equal(layout.HostingDataDirectory(saved, restoring: true), layout.GameDataDirectory(game));
        var arguments = GameStarter.Arguments("launcher.exe", game.DeveloperMode, useTestData: game.UseTestData, hostedWorld: true);
        Assert.Contains(GameStarter.HostedWorldOption, arguments);
        Assert.DoesNotContain(arguments, value => value.StartsWith("--server-url=", StringComparison.Ordinal));
        Assert.Equal(layout.HostingDataDirectory(saved, restoring: true), GameStarter.DataDirectory(localData, arguments));
        Assert.True(saved.JoinRemoteWorld);
        Assert.Equal(!hostTestData, saved.UseTestData);
        Assert.Throws<GameInstallException>(() => GameStarter.Arguments("launcher.exe", false,
            serverUrl: "https://other.example.test/", hostedWorld: true));
    }

    [Fact]
    public void HostedPlayRefusesAnOlderInstalledGameBeforeTryingToLaunchIt()
    {
        var directory = Directory.CreateTempSubdirectory("hosting-old-game-");
        try
        {
            var executable = Path.Combine(directory.FullName, "ClankerWorld.exe");
            File.WriteAllText(executable, "synthetic game that must not start");
            File.WriteAllText(Path.Combine(directory.FullName, "package.json"), """{"game":"ClankerWorld.exe","server":"server/ClankerWorld.Server.exe"}""");
            File.WriteAllText(Path.Combine(directory.FullName, LauncherLayout.PackageManifest), LocalServerLayout.HostingMarker);
            var error = Assert.Throws<GameInstallException>(() => GameStarter.Start(executable, "launcher.exe",
                new LauncherSettings(), new LauncherLayout(Path.Combine(directory.FullName, "data")), hostedWorld: true));
            Assert.Contains("newer build", error.Message, StringComparison.Ordinal);
        }
        finally { directory.Delete(recursive: true); }
    }
}
