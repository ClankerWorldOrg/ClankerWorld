using System.Diagnostics;
using System.IO.Compression;
using ClankerWorld.GodotClient.Diagnostics;

namespace ClankerWorld.Tests;

public sealed class ProblemReportTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 30, 0, TimeSpan.Zero);
    private readonly string root = Directory.CreateTempSubdirectory("problem-report-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void ReportHoldsSummaryAndLogsWithSecretsBlanked()
    {
        const string launchSecret = "q2Vb7nX0pL9sT4wY1zA8cD3eF6gH5jK2mN0rU7vW9xQ";
        const string requestId = "3f2a9c1be04d4e7f";
        var hostLog = Write("server.log",
            $"info: decision_accepted tick=5040 request={requestId} outcome=accepted latency_ms=812",
            "warn: provider_failed model=openai/gpt-5 key=sk-proj-AbCdEf0123456789XyZ outcome=fallback",
            $"debug: launch secret header {launchSecret}",
            "debug: launch_secret=Lw3Xc9Vb1Nm7Qa5Sd2Fg8Hj4Kl6Zx0Pq companion-secret: Rt5Yu7Io9Pa1Sd3Fg5Hj7Kl9Zx1Cv3Bn",
            "debug: Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig",
            "debug: {\"apiKey\":\"mistral-key-without-prefix\",\"pairing_code\":\"482913\"}",
            "debug: provider url https://player:hunter22@example.test/v1",
            "debug: token Zx81Qw7Er6Ty5Ui4Op3As2Df1Gh0JkLmNb");
        var gameLog = Write("godot.log", "Godot Engine v4.7.2", "owner_action_failed type=HttpRequestException");

        var path = ProblemReport.Write(Path.Combine(root, "reports"),
            [new("Game version", "0.1.0-dev"), new("World", "Riverfold\n(day 12)")],
            [new("server.log", hostLog), new("godot.log", gameLog), new("server.previous.log", Path.Combine(root, "missing.log"))],
            [launchSecret], Now);

        Assert.Equal("ClankerWorld-report-2026-10-10-093000.zip", Path.GetFileName(path));
        using var archive = ZipFile.OpenRead(path);
        Assert.Equal(["logs/server.log", "logs/godot.log", "summary.txt"], archive.Entries.Select(entry => entry.FullName));
        var host = Read(archive, "logs/server.log");
        foreach (var secret in new[]
                 {
                     launchSecret, "sk-proj-AbCdEf0123456789XyZ", "eyJhbGciOiJIUzI1NiJ9", "mistral-key-without-prefix",
                     "482913", "hunter22", "Zx81Qw7Er6Ty5Ui4Op3As2Df1Gh0JkLmNb", "Lw3Xc9Vb1Nm7Qa5Sd2Fg8Hj4Kl6Zx0Pq",
                     "Rt5Yu7Io9Pa1Sd3Fg5Hj7Kl9Zx1Cv3Bn",
                 })
            Assert.DoesNotContain(secret, host);
        // What a bug report needs to follow a request survives.
        Assert.Contains($"tick=5040 request={requestId} outcome=accepted latency_ms=812", host);
        Assert.Contains("model=openai/gpt-5", host);
        Assert.Contains("https://[removed]@example.test/v1", host);
        Assert.Contains("owner_action_failed type=HttpRequestException", Read(archive, "logs/godot.log"));

        var summary = Read(archive, "summary.txt");
        Assert.Contains("Created: 2026-10-10 09:30:00 UTC", summary);
        Assert.Contains("World: Riverfold (day 12)", summary);
        Assert.Contains("- server.previous.log: not found", summary);
    }

    [Fact]
    public void LongLogKeepsOnlyWholeLinesFromItsEnd()
    {
        var line = new string('z', 99);
        var lines = Enumerable.Range(0, ProblemReport.MaxBytesPerLog / 100 + 500).Select(_ => line).ToList();
        lines.Add("last line before the crash");
        var log = Write("server.log", [.. lines]);

        var path = ProblemReport.Write(root, [], [new("server.log", log)], [], Now);

        using var archive = ZipFile.OpenRead(path);
        var kept = Read(archive, "logs/server.log").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("[earlier lines left out]", kept[0]);
        Assert.All(kept[1..^1], item => Assert.Equal(line, item));
        Assert.Equal("last line before the crash", kept[^1]);
        Assert.True(kept.Length < lines.Count);
    }

    [Fact]
    public void LongDottedDiagnosticLineDoesNotStallRedaction()
    {
        var line = string.Concat(Enumerable.Repeat("Some.Type.", 26_214));
        var started = Stopwatch.GetTimestamp();

        Assert.Equal(line, ProblemReport.Redact(line));

        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.True(elapsed < TimeSpan.FromSeconds(5), $"Redacting a bounded diagnostic line took {elapsed}.");
    }

    [Theory]
    [InlineData(@"Found hostfxr: C:\Users\Player\AppData\Local\ClankerWorld\versions\0.1.0-nightly.20261015.5\data_ClankerWorld.windows.x86_64\hostfxr.dll")]
    [InlineData("Found hostfxr: C:/Users/Player/AppData/Local/ClankerWorld/versions/0.1.0-nightly.20261015.5/data_ClankerWorld.windows.x86_64/hostfxr.dll")]
    [InlineData("Loading /home/Player/ClankerWorld/versions/0.1.0-nightly.20261015.5/data_ClankerWorld.windows.x86_64/ClankerWorld.GodotClient.dll")]
    [InlineData("Loading res://UI/ClankerWorld1234567890LongResourceName/Menu.tscn")]
    [InlineData("Loading ../ClankerWorld1234567890LongResourceName/Menu.tscn")]
    [InlineData("Loading ClankerWorld1234567890LongResourceName.dll")]
    [InlineData("Loading ClankerWorld1234567890LongResourceName.GodotClient.dll")]
    [InlineData("Loading ClankerWorld1234567890LongResourceName.dll.")]
    [InlineData("Loading ClankerWorld1234567890LongResourceName.DLL")]
    public void OrdinaryPathsRemainReadableBesideSecrets(string diagnostic)
    {
        const string randomToken = "Zx81Qw7Er6Ty5Ui4Op3As2Df1Gh0JkLmNb";
        var line = diagnostic + " api_key=sk-proj-AbCdEf0123456789XyZ pairing_code=482913 launch_secret=local-secret " + randomToken;
        Assert.Equal(diagnostic + " api_key=[removed] pairing_code=[removed] launch_secret=[removed] [removed]",
            ProblemReport.Redact(line));
    }

    [Theory]
    [InlineData(".")]
    [InlineData(").")]
    [InlineData(", next diagnostic")]
    public void SentencePunctuationDoesNotProtectStandaloneRandomValues(string suffix)
    {
        const string value = "Zx81Qw7Er6Ty5Ui4Op3As2Df1Gh0JkLmNb";
        Assert.Equal("debug: [removed]" + suffix, ProblemReport.Redact("debug: " + value + suffix));
    }

    [Theory]
    [InlineData("C:/logs/sk-proj-AbCdEf0123456789XyZ/game.log")]
    [InlineData("/logs/sk-proj-AbCdEf0123456789XyZ/game.log")]
    public void PathContextNeverProtectsKnownOrProviderSecrets(string path)
    {
        const string launchSecret = "knownLaunchSecret12345678901234567890";
        var redacted = ProblemReport.Redact(path + "/" + launchSecret, [launchSecret]);
        Assert.DoesNotContain("sk-proj-AbCdEf0123456789XyZ", redacted);
        Assert.DoesNotContain(launchSecret, redacted);
        Assert.Contains("[removed]/game.log/[removed]", redacted);
    }

    [Theory]
    [InlineData("abcdef0123456789abcdef0123456789")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("482913")]
    public void UnlabelledHexTokensAndComparisonCodesAreRemoved(string token)
    {
        Assert.Equal("debug: [removed]", ProblemReport.Redact("debug: " + token));
    }

    [Fact]
    public void OnlyTheNewestReportsAreKept()
    {
        var reports = Path.Combine(root, "reports");
        var written = Enumerable.Range(0, ProblemReport.KeptReports + 2)
            .Select(minute =>
            {
                var path = ProblemReport.Write(reports, [], [], [], Now.AddMinutes(minute));
                File.SetLastWriteTimeUtc(path, Now.AddMinutes(minute).UtcDateTime);
                return path;
            })
            .ToList();
        File.WriteAllText(Path.Combine(reports, "notes.txt"), "the player's own file");

        Assert.Equal(written.TakeLast(ProblemReport.KeptReports).Order(),
            Directory.GetFiles(reports, "*.zip").Order());
        Assert.True(File.Exists(Path.Combine(reports, "notes.txt")));
    }

    private string Write(string name, params string[] lines)
    {
        var path = Path.Combine(root, name);
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string Read(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}
