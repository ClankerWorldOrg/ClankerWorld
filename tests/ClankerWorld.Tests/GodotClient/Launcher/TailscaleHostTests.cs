using ClankerWorld.GodotClient.Launcher;

namespace ClankerWorld.Tests;

public sealed class TailscaleHostTests
{
    private static readonly string[] SetupArguments = ["serve", "--bg", "--https=443", "http://127.0.0.1:5188/"];
    private static readonly string[] StatusArguments = ["serve", "status", "--json"];
    private const string Status = """{"BackendState":"Running","Self":{"DNSName":"world.example.ts.net."}}""";
    private const string Own = """{"TCP":{"443":{"HTTPS":true}},"Web":{"world.example.ts.net:443":{"Handlers":{"/":{"Proxy":"http://127.0.0.1:5188"}}}},"AllowFunnel":{"world.example.ts.net:443":false}}""";

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"TCP\":null,\"Web\":null,\"AllowFunnel\":null}")]
    public async Task ANewPrivateRouteIsConfirmedBeforeItsAddressIsShown(string empty)
    {
        var commands = new Commands([new(0, Status), new(0, empty), new(0, "private setup output"), new(0, Own)]);
        var address = await new TailscaleHost(commands).ConnectAsync(new("http://127.0.0.1:5188/"), CancellationToken.None);
        Assert.Equal(new Uri("https://world.example.ts.net/"), address);
        Assert.Equal(SetupArguments, commands.Calls[2]);
        Assert.Equal(StatusArguments, commands.Calls[3]);
        Assert.DoesNotContain(commands.Calls.SelectMany(call => call), value => value.Contains("funnel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnExistingExactPrivateRouteIsReusedWithoutChangingConfiguration()
    {
        var commands = new Commands([new(0, Status), new(0, Own)]);
        await new TailscaleHost(commands).ConnectAsync(new("http://127.0.0.1:5188/"), CancellationToken.None);
        Assert.Equal(2, commands.Calls.Count);
    }

    [Theory]
    [InlineData("funnel")]
    [InlineData("target")]
    [InlineData("path")]
    [InlineData("port")]
    public async Task AnotherSetupOrPublicFunnelIsNeverOverwritten(string difference)
    {
        var config = difference switch
        {
            "funnel" => Own.Replace(":false", ":true", StringComparison.Ordinal),
            "target" => Own.Replace(":5188", ":9999", StringComparison.Ordinal),
            "path" => Own.Replace("\"/\"", "\"/other\"", StringComparison.Ordinal),
            _ => Own.Replace("\"443\":", "\"80\":", StringComparison.Ordinal),
        };
        var commands = new Commands([new(0, Status), new(0, config)]);
        await Assert.ThrowsAsync<GameInstallException>(() => new TailscaleHost(commands)
            .ConnectAsync(new("http://127.0.0.1:5188/"), CancellationToken.None));
        Assert.Equal(2, commands.Calls.Count);
    }

    [Fact]
    public async Task FailedCommandsDoNotRepeatTheirPrivateOutputToThePlayer()
    {
        const string privateOutput = "synthetic private status that must not be shown";
        var commands = new Commands([new(1, privateOutput)]);
        var error = await Assert.ThrowsAsync<GameInstallException>(() => new TailscaleHost(commands)
            .ConnectAsync(new("http://127.0.0.1:5188/"), CancellationToken.None));
        Assert.DoesNotContain(privateOutput, error.Message, StringComparison.Ordinal);
        Assert.Single(commands.Calls);
    }

    [Fact]
    public async Task StopRemovesOnlyTheExactCheckedPrivateRoute()
    {
        var commands = new Commands([new(0, Own), new(0, "")]);
        await new TailscaleHost(commands).DisconnectAsync(new("http://127.0.0.1:5188/"),
            new("https://world.example.ts.net/"), CancellationToken.None);
        Assert.Equal("serve --https=443 off", string.Join(' ', commands.Calls[1]));
        var changed = new Commands([new(0, Own.Replace(":5188", ":9999", StringComparison.Ordinal))]);
        await Assert.ThrowsAsync<GameInstallException>(() => new TailscaleHost(changed).DisconnectAsync(
            new("http://127.0.0.1:5188/"), new("https://world.example.ts.net/"), CancellationToken.None));
        Assert.Single(changed.Calls);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("json")]
    [InlineData("cancel")]
    [InlineData("foreign")]
    [InlineData("cleanup")]
    public async Task FailedConfirmationRemovesOnlyTheRouteItCreated(string failure)
    {
        const string privateOutput = "synthetic private helper output";
        var foreign = Own.Replace(":5188", ":9999", StringComparison.Ordinal);
        var confirmation = failure switch
        {
            "json" => new HostCommandResult(0, "{"),
            "foreign" => new HostCommandResult(0, foreign),
            "cancel" => new HostCommandResult(0, Own),
            _ => new HostCommandResult(1, privateOutput),
        };
        using var cancellation = new CancellationTokenSource();
        var commands = new Commands([new(0, Status), new(0, "{}"), new(0, ""), confirmation,
            new(0, failure == "foreign" ? foreign : Own), new(failure == "cleanup" ? 1 : 0, privateOutput)])
        {
            AfterResult = call => { if (failure == "cancel" && call == 4) cancellation.Cancel(); },
        };
        var host = new TailscaleHost(commands);
        if (failure == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.ConnectAsync(new("http://127.0.0.1:5188/"), cancellation.Token));
        else
        {
            var error = await Assert.ThrowsAsync<GameInstallException>(() => host.ConnectAsync(new("http://127.0.0.1:5188/"), cancellation.Token));
            Assert.DoesNotContain(privateOutput, error.Message, StringComparison.Ordinal);
            if (failure is "foreign" or "cleanup") Assert.Contains("Check Tailscale", error.Message, StringComparison.Ordinal);
        }
        Assert.Equal(failure == "foreign" ? 5 : 6, commands.Calls.Count);
        Assert.Equal(StatusArguments, commands.Calls[4]);
        if (failure == "foreign") Assert.DoesNotContain(commands.Calls, call => call.Contains("off"));
        else Assert.Equal("serve --https=443 off", string.Join(' ', commands.Calls[5]));
    }

    private sealed class Commands(IEnumerable<HostCommandResult> results) : IHostCommandRunner
    {
        private readonly Queue<HostCommandResult> results = new(results);
        public List<IReadOnlyList<string>> Calls { get; } = [];
        public Action<int>? AfterResult { get; init; }
        public Task<HostCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Assert.Equal("tailscale", executable);
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(arguments);
            var result = results.Dequeue();
            AfterResult?.Invoke(Calls.Count);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }
}
