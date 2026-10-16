using System.Net;
using System.Net.Sockets;
using ClankerWorld.GodotClient.Networking;

namespace ClankerWorld.Tests;

public sealed partial class LocalServerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SlowConnectionRefusalLaunchesOnlyOnAFreePort(bool occupied, bool cancelCaller)
    {
        var directory = Directory.CreateTempSubdirectory("local-server-slow-refusal-");
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.LocalEndPoint!).Port;
        if (occupied) listener.Listen();
        else listener.Dispose();
        var layout = new LocalServerLayout(Path.Combine(directory.FullName, "missing-server"), [],
            directory.FullName, directory.FullName);
        var clock = new ManualTimeProvider();
        using var handler = new SlowConnectionRefusalHandler(clock);
        using var http = new HttpClient(handler);
        var game = new LocalServer(layout, http, port, FreePort(), TimeSpan.FromSeconds(2), timeProvider: clock);
        using var cancellation = new CancellationTokenSource();
        var starting = game.StartAsync("1.2.3", "abc", cancellation.Token);
        try
        {
            await ModelWorkHarness.WaitForSignalAsync(handler.Entered.Task);
            Assert.False(File.Exists(layout.LogPath));
            if (cancelCaller) cancellation.Cancel();
            else clock.Advance(TimeSpan.FromSeconds(2));

            if (cancelCaller)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModelWorkHarness.WaitForSignalAsync(starting));
            else
                Assert.Equal(occupied ? LocalServerStartOutcome.TimedOut : LocalServerStartOutcome.CouldNotStart,
                    (await ModelWorkHarness.WaitForSignalAsync(starting)).Outcome);

            // Launch opens the log before attempting the executable. A missing
            // executable makes the launch attempt observable without a child.
            Assert.Equal(!occupied && !cancelCaller, File.Exists(layout.LogPath));
            Assert.False(File.Exists(layout.SecretPath));
            Assert.False(game.StartedServer);
        }
        finally
        {
            cancellation.Cancel();
            try { await ModelWorkHarness.WaitForSignalAsync(starting); }
            catch (OperationCanceledException) { }
            await game.StopAsync(CancellationToken.None);
            directory.Delete(recursive: true);
        }
    }

    private sealed class SlowConnectionRefusalHandler(TimeProvider clock) : HttpMessageHandler
    {
        public InlineCompletionSource Entered { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Windows can take longer to refuse an empty loopback port than
            // the probe's two-second deadline. Advance only the client's clock.
            var refusal = clock.DelayAsync(TimeSpan.FromSeconds(3), cancellationToken);
            Entered.TrySetResult();
            await refusal;
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused.",
                new SocketException((int)SocketError.ConnectionRefused));
        }
    }
}
