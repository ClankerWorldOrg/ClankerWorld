using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.Tests;

public sealed partial class LocalServerTests
{
    private const string OverlapDirectory = "CLANKERWORLD_TEST_OVERLAP_DIRECTORY";

    [Fact]
    public async Task SeparateGameProcessesPreserveTheSecretAndReuseTheServerAfterOwnerCrash()
    {
        if (Environment.GetEnvironmentVariable(OverlapDirectory) is { } childDirectory)
        {
            await RunOwningGameProcess(childDirectory);
            return;
        }
        var directory = Directory.CreateTempSubdirectory("local-server-process-overlap-");
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory.FullName);
        var (port, approval) = FreePorts();
        File.WriteAllLines(Path.Combine(directory.FullName, "ports"),
            [port.ToString(CultureInfo.InvariantCulture), approval.ToString(CultureInfo.InvariantCulture)]);
        var start = new ProcessStartInfo(layout.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RuntimeWriterScan.RepositoryRoot,
        };
        foreach (var argument in new[] { "test", "tests/ClankerWorld.Tests/ClankerWorld.Tests.csproj", "-c", "Release",
            "--no-build", "--no-restore", "--filter",
            "FullyQualifiedName=ClankerWorld.Tests.LocalServerTests.SeparateGameProcessesPreserveTheSecretAndReuseTheServerAfterOwnerCrash" })
            start.ArgumentList.Add(argument);
        start.Environment[OverlapDirectory] = directory.FullName;
        using var ownerRunner = Process.Start(start)!;
        var output = ownerRunner.StandardOutput.ReadToEndAsync();
        var errors = ownerRunner.StandardError.ReadToEndAsync();
        Process? companion = null;
        Process? owner = null;
        using var actualHttp = new HttpClient();
        var fresh = new LocalServer(layout, actualHttp, port, approval);
        try
        {
            await WaitForOverlapFile(directory.FullName, "held");
            var identities = File.ReadAllLines(Path.Combine(directory.FullName, "held"));
            owner = Process.GetProcessById(int.Parse(identities[0], CultureInfo.InvariantCulture));
            companion = Process.GetProcessById(int.Parse(identities[1], CultureInfo.InvariantCulture));
            var digest = SHA256.HashData(File.ReadAllBytes(layout.SecretPath));
            using var emptyHttp = new HttpClient(new EmptyPortHandler());
            var losing = new LocalServer(layout with { FileName = Path.Combine(directory.FullName, "missing-server") },
                emptyHttp, port, approval);
            Assert.False((await losing.StartAsync(version, revision, CancellationToken.None)).IsReady);
            Assert.Equal(digest, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            Assert.False(File.Exists(layout.PreviousLogPath));
            File.WriteAllText(Path.Combine(directory.FullName, "release"), "release");
            await WaitForOverlapFile(directory.FullName, "ready");

            // Kill only the owning game test process, leaving its actual bundled
            // companion alive, exactly as an abnormal game exit would.
            owner.Kill(entireProcessTree: false);
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(companion.HasExited);
            Assert.Equal(LocalServerStartOutcome.Reused,
                (await fresh.StartAsync(version, revision, CancellationToken.None)).Outcome);
            Assert.False(fresh.StartedServer);
            Assert.Equal(digest, SHA256.HashData(File.ReadAllBytes(layout.SecretPath)));
            Assert.True(await fresh.StopAsync(CancellationToken.None));
            await WaitForCompanionExit(companion);
        }
        finally
        {
            File.WriteAllText(Path.Combine(directory.FullName, "release"), "release");
            File.WriteAllText(Path.Combine(directory.FullName, "finish"), "finish");
            if (companion is not null && !CompanionExited(companion)) companion.Kill(entireProcessTree: true);
            if (!ownerRunner.HasExited) ownerRunner.Kill(entireProcessTree: true);
            await ownerRunner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await Task.WhenAll(output, errors);
            if (companion is not null) await WaitForCompanionExit(companion);
            owner?.Dispose();
            companion?.Dispose();
            directory.Delete(recursive: true);
        }
    }

    private static async Task RunOwningGameProcess(string directory)
    {
        var assembly = typeof(Program).Assembly;
        var (version, revision) = BuildOf(assembly);
        var ports = File.ReadAllLines(Path.Combine(directory, "ports"));
        var layout = new LocalServerLayout(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [assembly.Location], Path.GetDirectoryName(assembly.Location)!, directory);
        using var held = new HeldLaunchProofHandler();
        using var http = new HttpClient(held);
        var game = new LocalServer(layout, http, int.Parse(ports[0], CultureInfo.InvariantCulture),
            int.Parse(ports[1], CultureInfo.InvariantCulture));
        var starting = game.StartAsync(version, revision, CancellationToken.None);
        try
        {
            await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var heldPath = Path.Combine(directory, "held");
            File.WriteAllLines(heldPath + ".tmp",
                [Environment.ProcessId.ToString(CultureInfo.InvariantCulture), game.StartedServerProcessId!.Value.ToString(CultureInfo.InvariantCulture)]);
            File.Move(heldPath + ".tmp", heldPath);
            await WaitForOverlapFile(directory, "release");
            held.Release.TrySetResult();
            Assert.Equal(LocalServerStartOutcome.Started, (await starting.WaitAsync(TimeSpan.FromSeconds(30))).Outcome);
            File.WriteAllText(Path.Combine(directory, "ready"), "ready");
            await WaitForOverlapFile(directory, "finish");
        }
        finally
        {
            held.Release.TrySetResult();
            await starting.WaitAsync(TimeSpan.FromSeconds(30));
            await game.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForOverlapFile(string directory, string name)
    {
        // Poll explicit cross-process IPC, never an assumed startup duration.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!File.Exists(Path.Combine(directory, name))) await Task.Delay(20, timeout.Token);
    }

    private static async Task WaitForCompanionExit(Process companion)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!CompanionExited(companion)) await Task.Delay(20, timeout.Token);
    }

    [Fact]
    public async Task CompanionExitDuringAnOpenProcessStateReadIsObservedWithoutHidingOtherIoErrors()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var process = Process.Start(new ProcessStartInfo("/bin/sh")
        {
            ArgumentList = { "-c", "read line" },
            RedirectStandardInput = true,
            UseShellExecute = false,
        })!;
        try
        {
            Assert.False(CompanionExited(process));
            var failure = new IOException("An unrelated process-state read failure.");
            Assert.Same(failure, Assert.Throws<IOException>(() => CompanionExited(process, _ => throw failure)));
            var readFailedAfterExit = false;
            Assert.True(await ModelWorkHarness.WaitForSignalAsync(Task.Run(() => CompanionExited(process, path =>
            {
                // Open procfs while the process is alive, then reap it before the read.
                using var reader = File.OpenText(path);
                process.Kill();
                process.WaitForExit();
                try { return reader.ReadToEnd(); }
                catch (IOException) { readFailedAfterExit = true; throw; }
            }))));
            Assert.True(readFailedAfterExit);
        }
        finally
        {
            if (!process.HasExited) process.Kill();
            await ModelWorkHarness.WaitForSignalAsync(process.WaitForExitAsync());
        }
    }

    private static bool CompanionExited(Process companion, Func<string, string>? readState = null)
    {
        if (companion.HasExited) return true;
        if (!OperatingSystem.IsLinux()) return false;
        // Killing the owner orphans its server. Some Linux container init
        // processes leave an exited orphan as a zombie rather than reaping it.
        try
        {
            var stat = (readState ?? File.ReadAllText)($"/proc/{companion.Id}/stat");
            return stat[stat.LastIndexOf(')') + 2] == 'Z';
        }
        // An open procfs file can fail with ESRCH after the process is reaped.
        // Confirm exit again rather than treating every read failure as an exit.
        catch (IOException) when (companion.HasExited) { return true; }
    }
}
