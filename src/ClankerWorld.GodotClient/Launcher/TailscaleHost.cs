using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.GodotClient.Launcher;

public sealed record HostCommandResult(int ExitCode, string Output);
public interface IHostCommandRunner
{
    Task<HostCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>Runs only the installed network helper, without logging its private status.</summary>
public sealed class HostCommandRunner(TimeProvider? timeProvider = null) : IHostCommandRunner
{
    public async Task<HostCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        LocalServerLayout.WithholdServerSettings(start.Environment);
        using var process = Process.Start(start) ?? throw new IOException("The private network helper did not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15), timeProvider ?? TimeProvider.System);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var output = ReadAsync(process.StandardOutput.BaseStream, deadline.Token);
            var error = ReadAsync(process.StandardError.BaseStream, deadline.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(deadline.Token));
            return new(process.ExitCode, await output);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<string> ReadAsync(Stream input, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + count > 128 * 1024)
                throw new InvalidDataException("The private network helper returned too much data.");
            output.Write(buffer, 0, count);
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }
}

/// <summary>Creates one private HTTPS route, refusing another service or public Funnel.</summary>
public sealed class TailscaleHost(IHostCommandRunner commands, string executable = "tailscale")
{
    public async Task DisconnectAsync(Uri localOrigin, Uri hostedOrigin, CancellationToken cancellationToken)
    {
        using var current = await JsonAsync(["serve", "status", "--json"], cancellationToken);
        if (IsEmpty(current.RootElement) || current.RootElement.ValueKind == JsonValueKind.Object &&
            current.RootElement.EnumerateObject().All(item => IsEmpty(item.Value))) return;
        RequireOwnRoute(current.RootElement, hostedOrigin.Host, localOrigin);
        var stopped = await commands.RunAsync(executable, ["serve", "--https=443", "off"], cancellationToken);
        if (stopped.ExitCode != 0)
            throw new GameInstallException("The world stopped, but Tailscale could not turn off its address. Check Tailscale's hosting settings.");
    }

    public async Task<Uri> ConnectAsync(Uri localOrigin, CancellationToken cancellationToken)
    {
        if (!localOrigin.IsLoopback || localOrigin.Scheme != "http" || localOrigin.AbsolutePath != "/")
            throw new ArgumentException("The hosted server must use its own loopback listener.", nameof(localOrigin));
        using var status = await JsonAsync(["status", "--json"], cancellationToken);
        if (!status.RootElement.TryGetProperty("BackendState", out var state) || state.GetString() != "Running" ||
            !status.RootElement.TryGetProperty("Self", out var self) ||
            !self.TryGetProperty("DNSName", out var name) || name.GetString()?.TrimEnd('.') is not { } dns ||
            Uri.CheckHostName(dns) != UriHostNameType.Dns || !dns.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase))
            throw new GameInstallException("Set up Tailscale on this computer, sign in to your private network and enable HTTPS before hosting.");
        var origin = new Uri("https://" + dns + "/");
        using var current = await JsonAsync(["serve", "status", "--json"], cancellationToken);
        if (IsEmpty(current.RootElement) || current.RootElement.ValueKind == JsonValueKind.Object &&
            current.RootElement.EnumerateObject().All(item => IsEmpty(item.Value)))
        {
            var configured = await commands.RunAsync(executable, ["serve", "--bg", "--https=443", localOrigin.AbsoluteUri], cancellationToken);
            if (configured.ExitCode != 0)
                throw new GameInstallException("Tailscale could not start private HTTPS hosting. Check its HTTPS setup, then try again.");
            using var confirmed = await JsonAsync(["serve", "status", "--json"], cancellationToken);
            RequireOwnRoute(confirmed.RootElement, origin.Host, localOrigin);
        }
        else RequireOwnRoute(current.RootElement, origin.Host, localOrigin);
        return origin;
    }

    private async Task<JsonDocument> JsonAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await commands.RunAsync(executable, arguments, cancellationToken);
        if (result.ExitCode != 0)
            throw new GameInstallException("Tailscale could not be checked. Open it and check your private network connection, then try again.");
        try { return JsonDocument.Parse(result.Output); }
        catch (JsonException exception)
        { throw new GameInstallException("Tailscale returned an unreadable status. Update it and try again.", exception); }
    }

    private static bool IsEmpty(JsonElement value) => value.ValueKind is JsonValueKind.Null ||
        value.ValueKind == JsonValueKind.Object && !value.EnumerateObject().Any();

    private static void RequireOwnRoute(JsonElement config, string dns, Uri target)
    {
        var valid = config.ValueKind == JsonValueKind.Object &&
            config.EnumerateObject().All(item => item.Name is "TCP" or "Web" or "AllowFunnel" || IsEmpty(item.Value)) &&
            (!config.TryGetProperty("AllowFunnel", out var funnel) || IsEmpty(funnel) ||
                funnel.ValueKind == JsonValueKind.Object && funnel.EnumerateObject().All(item => item.Value.ValueKind == JsonValueKind.False)) &&
            config.TryGetProperty("TCP", out var tcp) && tcp.ValueKind == JsonValueKind.Object && tcp.EnumerateObject().Count() == 1 &&
            tcp.TryGetProperty("443", out var port) && port.ValueKind == JsonValueKind.Object &&
            port.TryGetProperty("HTTPS", out var https) && https.ValueKind == JsonValueKind.True &&
            config.TryGetProperty("Web", out var web) && web.ValueKind == JsonValueKind.Object && web.EnumerateObject().Count() == 1 &&
            web.TryGetProperty(dns + ":443", out var site) && site.ValueKind == JsonValueKind.Object && site.TryGetProperty("Handlers", out var handlers) &&
            handlers.ValueKind == JsonValueKind.Object && handlers.EnumerateObject().Count() == 1 &&
            handlers.TryGetProperty("/", out var root) && root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("Proxy", out var proxy) && proxy.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(proxy.GetString(), UriKind.Absolute, out var actual) && actual == target;
        if (!valid)
            throw new GameInstallException("Tailscale already serves another setup or public Funnel is enabled. Keep that setup; use a separate hosting computer or change it yourself before trying again.");
    }
}
