using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Saves;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("valid")]
    public async Task LaunchCheckAnswersAChallengeWithoutTheSecretAndDoesNotChangeTheWorld(string? challengeKind)
    {
        var secret = new string('s', 32);
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new ApprovalListenerOptions(5189, secret))));
        using var client = host.CreateClient();
        var runtime = host.Services.GetRequiredService<WorldRuntime>();
        var file = host.Services.GetRequiredService<WorldStateFile>();
        var before = File.ReadAllBytes(file.Path);
        var challenge = challengeKind switch
        {
            "valid" => LocalServerLaunchCheck.NewChallenge(),
            "short" => "too-short",
            _ => null,
        };
        var response = await host.Server.SendAsync(context =>
        {
            LocalServerShutdownRequest(context);
            context.Request.Method = "GET";
            context.Request.Path = "/api/v1/approval/launch-check";
            // The check never needs the secret itself, and the secret alone is not a challenge.
            context.Request.Headers.Remove(ApprovalListenerOptions.LaunchSecretHeader);
            if (challenge is not null) context.Request.Headers[LocalServerLaunchCheck.ChallengeHeader] = challenge;
        });
        if (challengeKind == "valid")
        {
            Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
            if (response.Response.Body.CanSeek) response.Response.Body.Position = 0;
            var answer = await JsonSerializer.DeserializeAsync<LocalServerLaunchCheckAnswer>(response.Response.Body,
                JsonSerializerOptions.Web);
            Assert.True(LocalServerLaunchCheck.Matches(secret, challenge!, answer?.Answer));
            Assert.False(LocalServerLaunchCheck.Matches(secret, LocalServerLaunchCheck.NewChallenge(), answer?.Answer));
            Assert.DoesNotContain(secret, answer!.Answer, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(StatusCodes.Status404NotFound, response.Response.StatusCode);
        }
        Assert.False(runtime.Society.IsPaused);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalServerShutdownSavesPausedWorldBeforeRequestingExitAndCanRetryFailedWrite(bool failWrite)
    {
        var directory = Directory.CreateTempSubdirectory("companion-exit-");
        try
        {
            using var baseHost = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton(new ApprovalListenerOptions(5189, new string('s', 32)))));
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
                var failed = await host.Server.SendAsync(LocalServerShutdownRequest);
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
                LocalServerShutdownRequest(context);
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
    public async Task LocalServerShutdownWithoutItsSecretDoesNotPauseSaveOrExit(string? secret)
    {
        using var baseHost = new ServerWebApplicationFactory(null, gameWorld: true);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new ApprovalListenerOptions(5189, new string('s', 32)))));
        using var client = host.CreateClient();
        var runtime = host.Services.GetRequiredService<WorldRuntime>();
        var file = host.Services.GetRequiredService<WorldStateFile>();
        var before = File.ReadAllBytes(file.Path);
        var denied = await host.Server.SendAsync(context =>
        {
            LocalServerShutdownRequest(context);
            context.Request.Headers.Remove(ApprovalListenerOptions.LaunchSecretHeader);
            if (secret is not null) context.Request.Headers[ApprovalListenerOptions.LaunchSecretHeader] = secret;
        });
        Assert.Equal(StatusCodes.Status404NotFound, denied.Response.StatusCode);
        Assert.False(runtime.Society.IsPaused);
        Assert.Equal(before, File.ReadAllBytes(file.Path));
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
    }

    [Theory]
    [InlineData("schema", null)]
    [InlineData("schema", "wrong")]
    [InlineData("schema", "valid")]
    [InlineData("truncated", "valid")]
    public async Task LocalServerCanQuitStartupRecoveryWithoutChangingTheRefusedSave(string damage, string? secret)
    {
        var directory = Directory.CreateTempSubdirectory("recovery-exit-");
        try
        {
            var path = Path.Combine(directory.FullName, "runtime.json");
            using (var original = new WorldStateFile(path).LoadOrCreate("recovery-exit")) { }
            if (damage == "schema")
            {
                var checkpoint = JsonNode.Parse(File.ReadAllBytes(path))!;
                checkpoint["state"]!["schemaVersion"] = 1;
                File.WriteAllText(path, checkpoint.ToJsonString());
            }
            else File.WriteAllText(path, "{interrupted checkpoint");
            var refused = File.ReadAllBytes(path);
            var build = File.ReadAllBytes(SavedBuild.PathFor(path));

            using (var baseHost = new ServerWebApplicationFactory(directory.FullName, runClock: true))
            using (var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton(new ApprovalListenerOptions(5189, new string('s', 32))))))
            using (host.CreateClient())
            {
                var recovery = host.Services.GetRequiredService<WorldStartupRecovery>();
                Assert.True(recovery.Pending);
                Assert.Null(recovery.Capture().Autosave);
                Assert.Equal(damage == "schema" ? "different_save_format" : "checkpoint_refused", recovery.Capture().Reason);
                var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
                var exited = new TaskCompletionSource();
                using var onExit = lifetime.ApplicationStopping.Register(() => exited.TrySetResult());
                var response = await host.Server.SendAsync(context =>
                {
                    LocalServerShutdownRequest(context);
                    if (secret != "valid")
                    {
                        context.Request.Headers.Remove(ApprovalListenerOptions.LaunchSecretHeader);
                        if (secret is not null) context.Request.Headers[ApprovalListenerOptions.LaunchSecretHeader] = secret;
                    }
                });
                Assert.Equal(secret == "valid" ? StatusCodes.Status202Accepted : StatusCodes.Status404NotFound,
                    response.Response.StatusCode);
                if (secret == "valid") await ModelWorkHarness.WaitForSignalAsync(exited.Task);
                Assert.Equal(secret == "valid", lifetime.ApplicationStopping.IsCancellationRequested);
                Assert.True(recovery.Pending);
            }

            // Check after host teardown too: no shutdown service may save a replacement world.
            Assert.Equal(refused, File.ReadAllBytes(path));
            Assert.Equal(build, File.ReadAllBytes(SavedBuild.PathFor(path)));
            Assert.Empty(Directory.GetFiles(directory.FullName, "runtime.json.damaged.*.json"));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static void LocalServerShutdownRequest(HttpContext context)
    {
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/approval/shutdown";
        context.Connection.LocalPort = 5189;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers[ApprovalListenerOptions.LaunchSecretHeader] = new string('s', 32);
    }
}
