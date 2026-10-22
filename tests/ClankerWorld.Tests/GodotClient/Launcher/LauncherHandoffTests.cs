using System.Diagnostics;
using System.Text;
using System.IO.Pipes;
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

    [WindowsSaveFact]
    public async Task TemporaryControlReadinessCausalProbe()
    {
        var directory = Directory.CreateTempSubdirectory("launcher readiness proof ");
        try
        {
            for (var round = 0; round < 128; round++)
            {
                await Assert.ThrowsAsync<IOException>(() => HandoffAsync(
                    Path.Combine(directory.FullName, $"old-{round}"), true, waitForReleasedControl: false, holdAfterExit: true));
                await HandoffAsync(Path.Combine(directory.FullName, $"fixed-{round}"), true,
                    waitForReleasedControl: true, holdAfterExit: true);
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private static async Task HandoffAsync(string logs, bool forceDirect,
        bool waitForReleasedControl = true, bool holdAfterExit = false)
    {
        Directory.CreateDirectory(logs);
        var log = GameStarter.PrepareGameLog(logs);
        var previous = Path.Combine(logs, "game.previous.log");
        const string lastRun = "saved Town; last game log line\n";
        using var stream = new FileStream(log, FileMode.Create, FileAccess.Write,
            FileShare.Read | FileShare.Inheritable);
        stream.Write(Encoding.UTF8.GetBytes(lastRun));
        stream.Flush(flushToDisk: true);
        using var controlHold = holdAfterExit
            ? new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) : null;
        var name = "ClankerWorld-log-check-" + Guid.NewGuid().ToString("N");
        await using var pipe = new NamedPipeServerStream(name, PipeDirection.Out, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var connection = new CancellationTokenSource();
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var start = GameStarter.LauncherStartInfo(shell);
        if (forceDirect) start.UseShellExecute = false;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        // Native line input holds the child until the parent sends its release.
        foreach (var argument in new[] { "/D", "/C", $@"set /p held=<\\.\pipe\{name} >NUL" }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new IOException("The native launcher fixture did not start.");
        var ready = pipe.WaitForConnectionAsync(connection.Token);
        try
        {
            var first = await ModelWorkHarness.WaitForSignalAsync(Task.WhenAny(ready, child.WaitForExitAsync()));
            Assert.Same(ready, first);
            Assert.True(pipe.IsConnected);
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
            try
            {
                connection.Cancel();
                if (pipe.IsConnected && !child.HasExited)
                    await ModelWorkHarness.WaitForSignalAsync(pipe.WriteAsync("release\r\n"u8.ToArray()).AsTask());
                await ModelWorkHarness.WaitForSignalAsync(child.WaitForExitAsync());
            }
            finally
            {
                await pipe.DisposeAsync();
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                try { await ModelWorkHarness.WaitForSignalAsync(ready); }
                catch (OperationCanceledException) when (connection.IsCancellationRequested) { }
            }
        }
        Assert.Equal(0, child.ExitCode);
        if (forceDirect)
        {
            // Process exit is not the file-availability signal. Hold a compatible
            // read handle through rotation so another reader cannot race it.
            if (waitForReleasedControl)
            {
                using (await NativeFileWaits.AcquireForRenameAsync(log, () => controlHold?.Dispose()))
                    GameStarter.PrepareGameLog(logs);
            }
            else GameStarter.PrepareGameLog(logs);
            Assert.Equal(lastRun, File.ReadAllText(previous));
        }
    }
}
