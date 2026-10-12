using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Control;
using ClankerWorld.Server.Observation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("wrong", false)]
    [InlineData("ssssssssssssssssssssssssssssssss", true)]
    public async Task CompanionDiscoveryRequiresTheSecretAndDoesNotChangeTheWorld(string? secret, bool accepted)
    {
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new OwnerPairingHostOptions(5189, new string('s', 32)))));
        using var client = host.CreateClient();
        var runtime = host.Services.GetRequiredService<WorldRuntime>();
        var file = host.Services.GetRequiredService<WorldStateFile>();
        var before = File.ReadAllBytes(file.Path);
        var response = await host.Server.SendAsync(context =>
        {
            CompanionShutdownRequest(context);
            context.Request.Method = "GET";
            context.Request.Path = "/api/v1/local/companion";
            context.Request.Headers.Remove(OwnerPairingHostOptions.CompanionSecretHeader);
            if (secret is not null) context.Request.Headers[OwnerPairingHostOptions.CompanionSecretHeader] = secret;
        });
        Assert.Equal(accepted ? StatusCodes.Status200OK : StatusCodes.Status404NotFound, response.Response.StatusCode);
        Assert.False(runtime.Society.IsPaused);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompanionShutdownSavesPausedWorldBeforeRequestingExitAndCanRetryFailedWrite(bool failWrite)
    {
        var directory = Directory.CreateTempSubdirectory("companion-exit-");
        try
        {
            using var baseHost = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton(new OwnerPairingHostOptions(5189, new string('s', 32)))));
            using var client = host.CreateClient();
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
            var tick = runtime.WorldTick;
            var exitObservedSavedPause = false;
            var exitObservedAcceptedResponse = false;
            var exitObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var shutdownScope = host.Services.CreateScope();
            HttpContext? shutdownContext = null;
            using var onExit = lifetime.ApplicationStopping.Register(() =>
            {
                try
                {
                    using var saved = WorldRuntime.Restore(WorldRuntimeCodec.Decode(File.ReadAllBytes(file.Path)));
                    exitObservedSavedPause = saved.Society.IsPaused && saved.WorldTick == tick;
                    exitObservedAcceptedResponse = shutdownContext?.Response.HasStarted == true &&
                        shutdownContext.Response.StatusCode == StatusCodes.Status202Accepted;
                    // Force request-service teardown at exit rather than relying on
                    // the host's nondeterministic disposal to expose a late result.
                    shutdownScope.Dispose();
                    exitObserved.TrySetResult();
                }
                catch (Exception exception) { exitObserved.TrySetException(exception); }
            });
            using var locked = failWrite && OperatingSystem.IsWindows()
                ? new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
            var previous = File.ReadAllBytes(file.Path);
            if (failWrite && locked is null)
            {
                File.Delete(file.Path);
                Directory.CreateDirectory(file.Path);
            }

            if (failWrite)
            {
                var failed = await host.Server.SendAsync(CompanionShutdownRequest);
                Assert.Equal(StatusCodes.Status503ServiceUnavailable, failed.Response.StatusCode);
                Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
                Assert.True(runtime.Society.IsPaused);
                Assert.Equal(tick, runtime.WorldTick);
                locked?.Dispose();
                if (!OperatingSystem.IsWindows())
                {
                    Directory.Delete(file.Path);
                    File.WriteAllBytes(file.Path, previous);
                }
            }

            var stopped = await host.Server.SendAsync(context =>
            {
                CompanionShutdownRequest(context);
                context.RequestServices = shutdownScope.ServiceProvider;
                shutdownContext = context;
            });
            if (stopped.Response.StatusCode != StatusCodes.Status202Accepted)
            {
                if (stopped.Response.Body.CanSeek) stopped.Response.Body.Position = 0;
                using var response = new StreamReader(stopped.Response.Body, leaveOpen: true);
                output.WriteLine($"shutdown retry status={stopped.Response.StatusCode} tick={runtime.WorldTick} " +
                    $"paused={runtime.Society.IsPaused} stopping={lifetime.ApplicationStopping.IsCancellationRequested}");
                output.WriteLine(await response.ReadToEndAsync());
            }
            Assert.Equal(StatusCodes.Status202Accepted, stopped.Response.StatusCode);
            await exitObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
            Assert.True(exitObservedSavedPause);
            Assert.True(exitObservedAcceptedResponse);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task CompanionShutdownWithoutItsSecretDoesNotPauseSaveOrExit(string? secret)
    {
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new OwnerPairingHostOptions(5189, new string('s', 32)))));
        using var client = host.CreateClient();
        var runtime = host.Services.GetRequiredService<WorldRuntime>();
        var file = host.Services.GetRequiredService<WorldStateFile>();
        var before = File.ReadAllBytes(file.Path);
        var denied = await host.Server.SendAsync(context =>
        {
            CompanionShutdownRequest(context);
            context.Request.Headers.Remove(OwnerPairingHostOptions.CompanionSecretHeader);
            if (secret is not null) context.Request.Headers[OwnerPairingHostOptions.CompanionSecretHeader] = secret;
        });
        Assert.Equal(StatusCodes.Status404NotFound, denied.Response.StatusCode);
        Assert.False(runtime.Society.IsPaused);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
    }

    private static void CompanionShutdownRequest(HttpContext context)
    {
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/local/shutdown";
        context.Connection.LocalPort = 5189;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers[OwnerPairingHostOptions.CompanionSecretHeader] = new string('s', 32);
    }
}
