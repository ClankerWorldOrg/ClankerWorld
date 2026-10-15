using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ClankerWorld.GodotClient.ClientState;
using ClankerWorld.GodotClient.Launcher;
using ClankerWorld.GodotClient.Pairing;

namespace ClankerWorld.GodotClient.Networking;

/// <summary>
/// Where the local server lives next to the game, and where it keeps the
/// player's data. Program files and user data never share a folder, so
/// replacing a game version cannot touch saves, keys or the model-call count.
/// </summary>
public sealed record LocalServerLayout(
    string FileName,
    IReadOnlyList<string> PrefixArguments,
    string ServerDirectory,
    string DataDirectory)
{
    public string SecretPath => Path.Combine(DataDirectory, "server", "launch.secret");
    public string StartupLockPath => Path.Combine(DataDirectory, "server", "startup.lock");
    public string LogPath => Path.Combine(DataDirectory, "logs", "server.log");
    /// <summary>The log of the launch before this one, kept so a crash can still be reported.</summary>
    public string PreviousLogPath => Path.Combine(DataDirectory, "logs", "server.previous.log");
    public string ReportDirectory => Path.Combine(DataDirectory, "reports");
    public string SaveDirectory => Path.Combine(DataDirectory, "saves");
    public string SettingsDirectory => Path.Combine(DataDirectory, "settings");

    /// <summary>
    /// Finds the world server named by the game's package root. A
    /// development build or a server-only setup has none, and keeps
    /// connecting to whatever server the player chose.
    /// </summary>
    public static LocalServerLayout? FindBundled(string gameExecutablePath, string localApplicationData)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData) ||
            PackageEntryPoints.FindInstalledForGame(gameExecutablePath) is not { } package)
            return null;
        var serverExecutable = Path.Combine(package.Directory, package.EntryPoints.Server);
        return File.Exists(serverExecutable)
            ? new LocalServerLayout(serverExecutable, [], Path.GetDirectoryName(serverExecutable)!,
                Path.Combine(localApplicationData, "ClankerWorld"))
            : null;
    }

    /// <summary>
    /// Host settings. Only paths and ports: the secret stays in its file and
    /// keys stay in the host's protected store, never on the command line.
    /// </summary>
    public IReadOnlyList<string> ServerArguments(int port, int approvalPort, bool detailedLogs = false) =>
    [
        .. PrefixArguments,
        $"--contentRoot={ServerDirectory}",
        $"--ClankerWorld:Http:Port={port}",
        $"--ClankerWorld:Pairing:ApprovalPort={approvalPort}",
        $"--ClankerWorld:LocalServer:LaunchSecretPath={SecretPath}",
        $"--ClankerWorld:Pairing:AuthorityPath={Path.Combine(SettingsDirectory, "owner-authority.json")}",
        $"--ClankerWorld:World:SavePath={Path.Combine(SaveDirectory, "world.json")}",
        $"--ClankerWorld:Providers:ConfigurationPath={Path.Combine(SettingsDirectory, "provider-configuration.json")}",
        $"--ClankerWorld:Providers:UsagePath={Path.Combine(SettingsDirectory, "provider-usage.json")}",
        .. detailedLogs ? new[] { "--Logging:LogLevel:ClankerWorld=Debug" } : [],
    ];

    /// <summary>
    /// Provider keys a server started on its own saves from its environment on first
    /// start (the server's <c>Program.cs</c>). The game's own host takes keys
    /// only from the API keys screen, so these are not passed to it.
    /// </summary>
    public static IReadOnlyList<string> ProviderKeyVariables { get; } =
        ["TYPESAFE_API_KEY", "OPENAI_API_KEY", "OLLAMA_API_KEY", "ANTHROPIC_API_KEY"];

    /// <summary>
    /// How the game starts its host: the settings above, with the player's
    /// environment minus provider keys and ClankerWorld setting overrides.
    /// </summary>
    public ProcessStartInfo StartInfo(int port, int approvalPort, bool detailedLogs = false)
    {
        var start = new ProcessStartInfo(FileName)
        {
            WorkingDirectory = ServerDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in ServerArguments(port, approvalPort, detailedLogs)) start.ArgumentList.Add(argument);
        WithholdServerSettings(start.Environment);
        return start;
    }

    /// <summary>Removes provider keys and ClankerWorld setting overrides; keeps every other variable.</summary>
    public static void WithholdServerSettings(IDictionary<string, string?> environment)
    {
        foreach (var name in environment.Keys.Where(IsServerSetting).ToArray()) environment.Remove(name);
    }

    private static bool IsServerSetting(string name)
    {
        if (ProviderKeyVariables.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
        // The host reads ClankerWorld__Section__Key as a setting, with or
        // without the DOTNET_ or ASPNETCORE_ prefix, in any letter case.
        foreach (var settingPrefix in (string[])["DOTNET_", "ASPNETCORE_"])
        {
            if (!name.StartsWith(settingPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            name = name[settingPrefix.Length..];
            break;
        }
        return name.StartsWith("ClankerWorld__", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("ClankerWorld:", StringComparison.OrdinalIgnoreCase);
    }
}

public enum LocalServerStartOutcome
{
    /// <summary>This game started the host and it answered.</summary>
    Started,
    /// <summary>A host of this exact build that an earlier launch started is still running.</summary>
    Reused,
    /// <summary>Something that is not this build's host holds the port.</summary>
    PortInUse,
    /// <summary>The host exited before it was ready.</summary>
    Exited,
    /// <summary>The host did not answer in time.</summary>
    TimedOut,
    /// <summary>The host program could not be started at all.</summary>
    CouldNotStart,
}

public sealed record LocalServerStart(LocalServerStartOutcome Outcome, string? Detail = null)
{
    public bool IsReady => Outcome is LocalServerStartOutcome.Started or LocalServerStartOutcome.Reused;

    /// <summary>A sentence for the player: what went wrong and what to do next.</summary>
    public string PlayerMessage => Outcome switch
    {
        LocalServerStartOutcome.Started or LocalServerStartOutcome.Reused => "Your world server is running on this PC.",
        LocalServerStartOutcome.PortInUse =>
            $"Another program is using the port the world server needs ({Detail}). Close it, or another copy of ClankerWorld, then try again. Your saves are safe.",
        LocalServerStartOutcome.Exited =>
            "The world server closed while starting. Your saves are safe. Try again, and if it keeps happening, choose Report a problem and send the report.",
        LocalServerStartOutcome.TimedOut =>
            "The world server is taking too long to start. Your saves are safe. Try again.",
        _ => "The world server could not be started. Check that the game folder is complete, then try again.",
    };
}

/// <summary>
/// Starts, pairs with and stops the host bundled with the game, on this PC
/// only. It never stops a process it did not start, and never looks for or
/// falls back to a remote server.
/// </summary>
public sealed class LocalServer : IAsyncDisposable
{
    public const int DefaultPort = 5188;
    public const int DefaultApprovalPort = 5189;
    public const string SecretHeader = "X-ClankerWorld-Launch-Secret";

    private readonly LocalServerLayout layout;
    private readonly HttpClient http;
    private readonly int port;
    private readonly int approvalPort;
    private readonly TimeSpan readyTimeout;
    private readonly TimeProvider timeProvider;
    private readonly bool detailedLogs;
    private Process? process;
    private StreamWriter? log;
    private string? secret;
    // True once the listener answered the launch check for this secret. Only
    // then does the game send the secret on approval requests.
    private bool proven;
    private readonly object logGate = new();

    public LocalServer(LocalServerLayout layout, HttpClient http, int port = DefaultPort,
        int approvalPort = DefaultApprovalPort, TimeSpan? readyTimeout = null, bool detailedLogs = false, TimeProvider? timeProvider = null)
    {
        this.layout = layout;
        this.http = http;
        this.port = port;
        this.approvalPort = approvalPort;
        this.readyTimeout = readyTimeout ?? TimeSpan.FromSeconds(90);
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.detailedLogs = detailedLogs;
    }

    /// <summary>
    /// A fixed literal-loopback origin: the client pins the origin it paired
    /// with, so it must not change between launches.
    /// </summary>
    public Uri Origin => new($"http://127.0.0.1:{port}/");

    private Uri ApprovalOrigin => new($"http://127.0.0.1:{approvalPort}/");

    public bool StartedServer => process is not null;
    internal int? StartedServerProcessId => process?.Id;

    public LocalServerLayout Layout => layout;

    /// <summary>Exact values a problem report must blank, should a log ever carry one.</summary>
    public IReadOnlyCollection<string> SecretsToHide() => secret is null ? [] : [secret];

    public async Task<LocalServerStart> StartAsync(string expectedVersion, string expectedRevision,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileStream startup;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(layout.StartupLockPath)!);
            var options = new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            startup = new FileStream(layout.StartupLockPath, options);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Another game may still be starting its companion. Refuse before
            // probing or changing its secret, log or process. Keep the lock file:
            // deleting it would let another opener lock a different file.
            return new LocalServerStart(LocalServerStartOutcome.CouldNotStart, exception.GetType().Name);
        }
        using (startup)
        {
            try { return await StartExclusiveAsync(expectedVersion, expectedRevision, cancellationToken); }
            catch (OperationCanceledException)
            {
                // Finish stopping our child before releasing startup ownership.
                // It never answered the launch check, so it is stopped directly;
                // a reused server belongs to another launch.
                if (process is not null) await StopAsync(CancellationToken.None);
                throw;
            }
        }
    }

    private async Task<LocalServerStart> StartExclusiveAsync(string expectedVersion, string expectedRevision,
        CancellationToken cancellationToken)
    {
        if (process is { HasExited: false }) return new LocalServerStart(LocalServerStartOutcome.Started);
        var deadline = timeProvider.GetUtcNow() + readyTimeout;
        while (true)
        {
            var existing = await ProbeAsync(expectedVersion, expectedRevision, cancellationToken);
            if (existing == Probe.Nothing) break;
            if (existing == Probe.SameBuild && TryReadSecret(out var running))
            {
                var proof = await CheckLaunchSecretAsync(running, cancellationToken);
                if (proof == LaunchCheck.Confirmed)
                {
                    (secret, proven) = (running, true);
                    return new LocalServerStart(LocalServerStartOutcome.Reused);
                }
                if (proof == LaunchCheck.Refused)
                    return new LocalServerStart(LocalServerStartOutcome.PortInUse, port.ToString(CultureInfo.InvariantCulture));
            }
            else if (existing != Probe.Unresponsive)
                return new LocalServerStart(LocalServerStartOutcome.PortInUse, port.ToString(CultureInfo.InvariantCulture));

            // A request deadline supplies no evidence of another build or an
            // invalid secret. Retry within the existing startup window before
            // reporting an unresponsive server, without starting a second host.
            if (timeProvider.GetUtcNow() >= deadline) return new LocalServerStart(LocalServerStartOutcome.TimedOut);
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, cancellationToken);
        }

        byte[]? previousSecret = null;
        var secretWritten = false;
        try
        {
            if (File.Exists(layout.SecretPath)) previousSecret = File.ReadAllBytes(layout.SecretPath);
            (secret, proven) = (WriteFreshSecret(), false);
            secretWritten = true;
            process = Launch();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (process is { HasExited: false } running)
            {
                running.Kill(entireProcessTree: true);
                await running.WaitForExitAsync(CancellationToken.None);
            }
            await ForgetProcessAsync();
            if (secretWritten)
            {
                if (previousSecret is null) File.Delete(layout.SecretPath);
                else WriteSecret(previousSecret);
            }
            secret = null;
            return new LocalServerStart(LocalServerStartOutcome.CouldNotStart, exception.GetType().Name);
        }

        while (timeProvider.GetUtcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                var code = process.ExitCode;
                await ForgetProcessAsync();
                return new LocalServerStart(LocalServerStartOutcome.Exited, code.ToString(CultureInfo.InvariantCulture));
            }
            switch (await ProbeAsync(expectedVersion, expectedRevision, cancellationToken))
            {
                case Probe.SameBuild:
                    if (await CheckLaunchSecretAsync(secret!, cancellationToken) == LaunchCheck.Confirmed)
                    {
                        proven = true;
                        return new LocalServerStart(LocalServerStartOutcome.Started);
                    }
                    break;
                case Probe.OtherBuild or Probe.NotClankerWorld:
                    // Something else claimed the port first; leave it, stop ours.
                    await StopAsync(cancellationToken);
                    return new LocalServerStart(LocalServerStartOutcome.PortInUse, port.ToString(CultureInfo.InvariantCulture));
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, cancellationToken);
        }
        await StopAsync(cancellationToken);
        return new LocalServerStart(LocalServerStartOutcome.TimedOut);
    }

    /// <summary>True when <paramref name="origin"/> is this game's own host, not another server.</summary>
    public bool Serves(Uri origin) => ServerOrigin.Same(origin, Origin);

    /// <summary>
    /// Starts this game's pairing on its own local server's approval listener, with
    /// this launch's secret, so it does not depend on the public route's
    /// shared request budget or pending slots. Status and activation still
    /// use <see cref="Origin"/>, the origin the registration is pinned to.
    /// </summary>
    public Task<OwnerPairingStart> StartPairingAsync(IOwnerDeviceSigner deviceKey, CancellationToken cancellationToken)
    {
        if (secret is null || !proven) throw new InvalidOperationException("The world server on this PC has not started yet.");
        return new OwnerPairingClient(http).StartPairingAsync(ApprovalOrigin, OwnerPairingEndpoints.ApprovalPairings,
            deviceKey, new KeyValuePair<string, string>(SecretHeader, secret), cancellationToken);
    }

    /// <summary>
    /// Approves this game's own pending pairing on the host's approval
    /// listener, using the comparison code the host returned to it.
    /// </summary>
    public async Task<bool> ApprovePairingAsync(string pairingId, string pairingCode, CancellationToken cancellationToken)
    {
        if (secret is null || !proven) return false;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(ApprovalOrigin, $"api/v1/approval/pairings/{Uri.EscapeDataString(pairingId)}/approve"))
        {
            Content = JsonContent.Create(new { pairingCode }),
        };
        request.Headers.Add(SecretHeader, secret);
        using var response = await http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Asks the host to save and exit, and waits for it. Only a host this
    /// launch started is ever forced to stop, and only if it ignores the request.
    /// </summary>
    public async Task<bool> StopAsync(CancellationToken cancellationToken)
    {
        var acknowledged = false;
        if (secret is not null && proven)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ApprovalOrigin, "api/v1/approval/shutdown"));
                request.Headers.Add(SecretHeader, secret);
                using var deadlineTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5), timeProvider);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineTimeout.Token);
                using var response = await http.SendAsync(request, deadline.Token);
                // The host explicitly refused to exit: preserve its paused world
                // and our process handle so the player can repair and retry.
                if (!response.IsSuccessStatusCode) return false;
                acknowledged = true;
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Already gone, or not answering: handled below for a host we started.
            }
        }
        if (process is { HasExited: false } running)
        {
            // Our child never answered the launch check, so whatever holds the
            // approval port was not asked over HTTP: stop the child directly.
            if (!proven) running.Kill(entireProcessTree: true);
            using var waitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, waitTimeout.Token);
            try
            {
                await running.WaitForExitAsync(wait.Token);
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The host saves every tick, so forcing our own child is the last resort.
                running.Kill(entireProcessTree: true);
            }
        }
        // A host that acknowledged the request, or our own child that has now
        // exited, no longer needs this launch's secret. A host that may still be
        // running keeps it, so a later launch can still prove and reuse it.
        if (acknowledged || process is not null) ForgetSecret();
        await ForgetProcessAsync();
        return true;
    }

    private void ForgetSecret()
    {
        if (secret is null) return;
        try
        {
            if (TryReadSecret(out var saved) && saved == secret) File.Delete(layout.SecretPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The next launch writes a fresh secret before starting a host.
        }
        (secret, proven) = (null, false);
    }

    public async ValueTask DisposeAsync() { await StopAsync(CancellationToken.None); }

    private enum LaunchCheck { Confirmed, Refused, Unresponsive }

    /// <summary>
    /// Asks the listener to prove it holds <paramref name="value"/> by answering
    /// a fresh challenge. The secret itself is not sent, so a listener that
    /// cannot answer learns nothing from the check.
    /// </summary>
    private async Task<LaunchCheck> CheckLaunchSecretAsync(string value, CancellationToken cancellationToken)
    {
        var challenge = LocalServerLaunchCheck.NewChallenge();
        try
        {
            using var deadlineTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2), timeProvider);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineTimeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApprovalOrigin, "api/v1/approval/launch-check"));
            request.Headers.Add(LocalServerLaunchCheck.ChallengeHeader, challenge);
            using var response = await http.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode) return LaunchCheck.Refused;
            var answer = await response.Content.ReadFromJsonAsync<LocalServerLaunchCheckAnswer>(deadline.Token);
            return LocalServerLaunchCheck.Matches(value, challenge, answer?.Answer) ? LaunchCheck.Confirmed : LaunchCheck.Refused;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is HttpRequestException or OperationCanceledException)
        {
            return LaunchCheck.Unresponsive;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            return LaunchCheck.Refused;
        }
    }

    private enum Probe { Nothing, SameBuild, OtherBuild, NotClankerWorld, Unresponsive }

    private async Task<Probe> ProbeAsync(string expectedVersion, string expectedRevision, CancellationToken cancellationToken)
    {
        try
        {
            using var deadlineTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2), timeProvider);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadlineTimeout.Token);
            using var handshake = await http.GetAsync(new Uri(Origin, "api/v1/handshake"), deadline.Token);
            if (!handshake.IsSuccessStatusCode) return Probe.NotClankerWorld;
            var status = await http.GetFromJsonAsync<ServerStatus>(new Uri(Origin, "api/v1/status"), deadline.Token);
            return status is not null &&
                string.Equals(status.Version, expectedVersion, StringComparison.Ordinal) &&
                string.Equals(status.SourceRevision, expectedRevision, StringComparison.Ordinal)
                    ? Probe.SameBuild
                    : Probe.OtherBuild;
        }
        catch (HttpRequestException exception) when (exception.HttpRequestError == HttpRequestError.ConnectionError)
        {
            return Probe.Nothing;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Windows can take longer than the request deadline to refuse a
            // connection to an empty loopback port. A timeout alone cannot say
            // whether a listener exists; an exclusive bind can prove it is free.
            return CanBindPort() ? Probe.Nothing : Probe.Unresponsive;
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException or
            NotSupportedException)
        {
            return Probe.NotClankerWorld;
        }
    }

    private bool CanBindPort()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ExclusiveAddressUse = true,
            };
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            // A listener, reserved port or denied bind is not proof of a free
            // port. Leave its process, secret and logs alone and keep waiting.
            return false;
        }
    }

    private bool TryReadSecret(out string value)
    {
        try
        {
            value = File.ReadAllText(layout.SecretPath).Trim();
            return value.Length > 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            value = string.Empty;
            return false;
        }
    }

    private string WriteFreshSecret()
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        WriteSecret(System.Text.Encoding.UTF8.GetBytes(value));
        return value;
    }

    private void WriteSecret(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(layout.SecretPath)!);
        // The player's own data folder is private to their Windows account.
        // Elsewhere, restrict the file to this user before writing the value.
        var temporary = layout.SecretPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var file = new FileStream(temporary, options))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, layout.SecretPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private Process Launch()
    {
        Directory.CreateDirectory(layout.SaveDirectory);
        Directory.CreateDirectory(layout.SettingsDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(layout.LogPath)!);
        try
        {
            File.Move(layout.LogPath, layout.PreviousLogPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // No earlier log, or it is still open somewhere: start a fresh one anyway.
        }
        log = new StreamWriter(new FileStream(layout.LogPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
        if (detailedLogs) WriteLog($"server_launch logging=detailed public_port={port} approval_port={approvalPort}");
        var started = new Process { StartInfo = layout.StartInfo(port, approvalPort, detailedLogs), EnableRaisingEvents = true };
        started.OutputDataReceived += (_, line) => WriteLog(line.Data);
        started.ErrorDataReceived += (_, line) => WriteLog(line.Data);
        if (!started.Start()) throw new InvalidOperationException("The host process did not start.");
        process = started;
        started.BeginOutputReadLine();
        started.BeginErrorReadLine();
        return started;
    }

    private void WriteLog(string? line)
    {
        if (line is null) return;
        try
        {
            lock (logGate) log?.WriteLine(line);
        }
        catch (ObjectDisposedException)
        {
            // The game is closing the log after the host exited.
        }
    }

    private async Task ForgetProcessAsync()
    {
        process?.Dispose();
        process = null;
        await StopLoggingAsync();
    }

    private async Task StopLoggingAsync()
    {
        StreamWriter? closing;
        lock (logGate)
        {
            closing = log;
            log = null;
        }
        if (closing is not null) await closing.DisposeAsync();
    }

    private sealed record ServerStatus(
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("sourceRevision")] string? SourceRevision);
}
