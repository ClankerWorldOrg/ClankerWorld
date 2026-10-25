using System.Diagnostics;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Pairing;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private void VerifyDeveloperRunOptions()
    {
        try
        {
            var arguments = OS.GetCmdlineUserArgs();
            FailUiCheckIf(!GameStarter.UsesTestData(arguments) || !GameStarter.UsesDetailedLogs(arguments), "The native check must receive both Developer run options.");
            var layout = LauncherLayout.ForUser(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData));
            var realPreferences = ProjectSettings.GlobalizePath("user://game-display-preferences.json");
            var previousPreferences = File.Exists(realPreferences) ? File.ReadAllBytes(realPreferences) : null;
            var previousRegistration = ProjectSettings.GlobalizePath("user://owner-device-registration.json");
            var registrationBytes = File.Exists(previousRegistration) ? File.ReadAllBytes(previousRegistration) : null;
            displayPreferencesStore.Save(new(Theme: "dark"));
            registrationStore.Save(new(new("smoke-authority", "smoke-world"), "smoke-device", "smoke-fingerprint"));
            FailUiCheckIf(!File.Exists(Path.Combine(layout.TestDataDirectory, "client", "game-display-preferences.json")) ||
                !File.Exists(Path.Combine(layout.TestDataDirectory, "client", "owner-device-registration.json")) ||
                GameUserPath("owner-pending-submission.json") != Path.Combine(layout.TestDataDirectory, "client", "owner-pending-submission.json"), "The actual game stores must use only test client data.");
            FailUiCheckIf(previousPreferences is null ? File.Exists(realPreferences) : !previousPreferences.SequenceEqual(File.ReadAllBytes(realPreferences)), "A test run changed real display preferences.");
            FailUiCheckIf(registrationBytes is null ? File.Exists(previousRegistration) : !registrationBytes.SequenceEqual(File.ReadAllBytes(previousRegistration)), "A test run changed real pairing registration.");
            GD.Print("Developer run option checks passed: actual command line, test preferences and pairing, pending-submission path, real client data preserved.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PrintErr("Developer run option checks failed: " + exception);
            GetTree().Quit(1);
        }
    }

    private async Task VerifyLaunchOptionsAsync()
    {
        FailUiCheckIf(!NoMenuOverWorld() || developerPanel.Visible || localServer is not null, "Launch option checks need the unbundled world with no menu open.");
        var previousMode = developerMode;
        var previousLauncher = launcherPath;
        var previousPathRead = launcherPathRead;
        var previousFailedVersion = failedLauncherVersion;
        var previousFailedWorld = failedLauncherWorld;
        var previousRetryVersion = retryLauncherVersion;
        var previousRetryWorld = retryLauncherWorld;
        var previousRetryChoice = retryChooseCurrentWorld;
        var directory = Directory.CreateTempSubdirectory("launcher handoff ");
        try
        {
            developerMode = false;
            renderedControlsTheme = null;
            FillControlsGroups();
            _Input(new InputEventKey { Keycode = Key.F12, Pressed = true });
            FailUiCheckIf(developerPanel.Visible, "F12 must do nothing outside Developer mode.");
            _UnhandledKeyInput(new InputEventKey { Keycode = Key.F1, Pressed = true });
            var keys = controlsColumns.FindChildren("*", nameof(Label), recursive: true, owned: false)
                .OfType<Label>().Select(CaptionForChecks).ToArray();
            FailUiCheckIf(!controlsPanel.Visible || keys.Contains("F12"), "The real F1 list must omit F12 outside Developer mode.");
            _UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });

            isQuittingGame = true;
            quitToLauncherConfirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            FailUiCheckIf(!isQuittingGame || launcherStartFailure.Visible, "A pending Quit to Launcher confirmation cannot replace an active quit.");
            isQuittingGame = false;
            launcherPathRead = true;
            launcherPath = Path.Combine(directory.FullName, "removed launcher");
            await QuitGameAsync(openLauncher: true);
            FailUiCheckIf(!launcherStartFailure.Visible || isQuittingGame, "A missing launcher must report failure and keep the game open for retry.");
            launcherStartFailure.Hide();

            const string worldId = "0123456789abcdef0123456789abcdef";
            ShowLocalServerStopFailure("0.0.1-older", worldId);
            localServerStopFailure.Hide();
            localServerStopFailure.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
            FailUiCheckIf(!launcherStartFailure.Visible || failedLauncherVersion != "0.0.1-older" ||
                failedLauncherWorld != worldId || isQuittingGame, "A refused-stop retry must preserve the complete launcher target.");
            launcherStartFailure.Hide();

            if (OperatingSystem.IsLinux())
            {
                launcherPath = Path.Combine(directory.FullName, "launcher with spaces.sh");
                var arguments = Path.Combine(directory.FullName, "arguments.txt");
                File.WriteAllText(launcherPath, "#!/bin/sh\nprintf '%s\\n' \"$@\" > \"$(dirname \"$0\")/arguments.txt\"\n");
                File.SetUnixFileMode(launcherPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                if (!TryOpenLauncher()) throw new InvalidOperationException("The launcher hand-off process must start.");
                var deadline = Stopwatch.StartNew();
                while ((!File.Exists(arguments) || File.ReadAllLines(arguments).Length < 3) && deadline.Elapsed < TimeSpan.FromSeconds(5))
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!File.Exists(arguments) || !File.ReadAllLines(arguments).SequenceEqual(new[] { "--", "--choose-version=" + BuildInformation.Version, "--player-data" }))
                    throw new InvalidOperationException("Launcher hand-off must retain argument boundaries and the actual game version.");
                File.Delete(arguments);
                if (!TryOpenLauncher("0.0.1-older", worldId)) throw new InvalidOperationException("The requested-version hand-off must start.");
                deadline.Restart();
                while ((!File.Exists(arguments) || File.ReadAllLines(arguments).Length < 4) && deadline.Elapsed < TimeSpan.FromSeconds(5))
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!File.Exists(arguments) || !File.ReadAllLines(arguments).SequenceEqual(new[] { "--", "--choose-version=0.0.1-older", "--player-data", "--choose-world=" + worldId }))
                    throw new InvalidOperationException("The real launcher process must receive the selected world's version and complete catalog ID.");
            }
            GD.Print("Game launch option checks passed: Developer mode off, F1 list, busy quit, missing launcher and version hand-off.");
        }
        finally
        {
            isQuittingGame = false;
            launcherPath = previousLauncher;
            launcherPathRead = previousPathRead;
            launcherStartFailure.Hide();
            localServerStopFailure.Hide();
            failedLauncherVersion = previousFailedVersion;
            failedLauncherWorld = previousFailedWorld;
            retryLauncherVersion = previousRetryVersion;
            retryLauncherWorld = previousRetryWorld;
            retryChooseCurrentWorld = previousRetryChoice;
            controlsPanel.Hide();
            developerMode = previousMode;
            renderedControlsTheme = null;
            FillControlsGroups();
            directory.Delete(recursive: true);
        }
    }
}
