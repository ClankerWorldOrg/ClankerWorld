using System.IO.Compression;
using Godot;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    /// <summary>
    /// Settings offers Report a problem, and the report it writes holds the
    /// summary and the game's log through the last print, without waiting for an error or exit.
    /// </summary>
    private void VerifyProblemReport()
    {
        if (!gameSettingsContent.FindChildren("*", nameof(Button), recursive: true, owned: false).OfType<Button>()
                .Any(button => button.Text == "Report a problem"))
            throw new InvalidOperationException("Game settings must offer Report a problem.");
        var directory = ProjectSettings.GlobalizePath("user://smoke-problem-reports");
        try
        {
            if (!ProjectSettings.GetSetting("application/run/flush_stdout_on_print").AsBool())
                throw new InvalidOperationException("Release builds must flush game logs on every print.");
            var marker = "problem_report_live_log_check " + Guid.NewGuid().ToString("N");
            GD.Print(marker);
            var path = WriteProblemReport(directory);
            using var archive = ZipFile.OpenRead(path);
            using var reader = new StreamReader(archive.GetEntry("summary.txt")!.Open());
            var summary = reader.ReadToEnd();
            var gameLog = archive.GetEntry("logs/game.log");
            if (!summary.Contains("Game version: " + BuildInformation.Version, StringComparison.Ordinal) ||
                !summary.Contains("World server: ", StringComparison.Ordinal) ||
                gameLog is null)
                throw new InvalidOperationException("A problem report must hold its summary and the game's log.");
            using var logReader = new StreamReader(gameLog.Open());
            if (!logReader.ReadToEnd().Contains(marker, StringComparison.Ordinal))
                throw new InvalidOperationException("A problem report must include the last printed line while the game is running.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
