using System.Diagnostics;
using System.Text;
using ClankerWorld.GodotClient.Launcher;

namespace ClankerWorld.Tests;

[CollectionDefinition("Launcher native process handles", DisableParallelization = true)]
public sealed class LauncherNativeProcessFixture;

[Collection("Launcher native process handles")]
public sealed class LauncherHandoffTests
{
    [WindowsSaveFact]
    public async Task AReturnedLauncherDoesNotKeepTheGamesLogOpenAcrossRepeatedPlay()
    {
        var directory = Directory.CreateTempSubdirectory("launcher handle check ");
        try
        {
            // Establish that the Windows fixture exposes the actual inherited
            // handle failure, then use the production handoff configuration.
            await HandoffAsync(Path.Combine(directory.FullName, "control"), forceDirect: true);
            var logs = Path.Combine(directory.FullName, "game logs");
            for (var round = 0; round < 3; round++) await HandoffAsync(logs, forceDirect: false);
        }
        finally { directory.Delete(recursive: true); }
    }

    private static async Task HandoffAsync(string logs, bool forceDirect)
    {
        Directory.CreateDirectory(logs);
        var log = GameStarter.PrepareGameLog(logs);
        var previous = Path.Combine(logs, "game.previous.log");
        const string lastRun = "saved Town; last game log line\n";
        using var stream = new FileStream(log, FileMode.Create, FileAccess.Write,
            FileShare.Read | FileShare.Inheritable);
        stream.Write(Encoding.UTF8.GetBytes(lastRun));
        stream.Flush(flushToDisk: true);
        var name = "Local\\ClankerWorld-log-check-" + Guid.NewGuid().ToString("N");
        using var entered = new EventWaitHandle(false, EventResetMode.ManualReset, name + "-entered");
        using var release = new EventWaitHandle(false, EventResetMode.ManualReset, name + "-release");
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = GameStarter.LauncherStartInfo(shell);
        if (forceDirect) start.UseShellExecute = false;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        // Named events hold the real child open without sleeps or file polling.
        var command = $"$ready=[Threading.EventWaitHandle]::OpenExisting('{name}-entered'); " +
            $"$release=[Threading.EventWaitHandle]::OpenExisting('{name}-release'); " +
            "$null=$ready.Set(); $null=$release.WaitOne(); $ready.Dispose(); $release.Dispose()";
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes(command)) }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new IOException("The native launcher fixture did not start.");
        var ready = Task.Run(() => entered.WaitOne());
        try
        {
            var first = await ModelWorkHarness.WaitForSignalAsync(Task.WhenAny(ready, child.WaitForExitAsync()));
            Assert.Same(ready, first);
            Assert.True(await ready);
            Assert.False(child.HasExited);
            // The game has closed its own copy; only the returned launcher
            // could now keep its inherited copy open.
            stream.Dispose();
            if (forceDirect)
            {
                Assert.Throws<IOException>(() => GameStarter.PrepareGameLog(logs));
                Assert.False(File.Exists(previous));
            }
            else
            {
                GameStarter.PrepareGameLog(logs);
                Assert.Equal(lastRun, File.ReadAllText(previous));
                File.WriteAllText(log, "next game opened its log\n");
                Assert.Equal("next game opened its log\n", File.ReadAllText(log));
            }
        }
        finally
        {
            entered.Set();
            release.Set();
            try { await ModelWorkHarness.WaitForSignalAsync(child.WaitForExitAsync()); }
            finally
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await ModelWorkHarness.WaitForSignalAsync(ready);
            }
        }
        Assert.Equal(0, child.ExitCode);
        if (forceDirect)
        {
            GameStarter.PrepareGameLog(logs);
            Assert.Equal(lastRun, File.ReadAllText(previous));
        }
    }
}
