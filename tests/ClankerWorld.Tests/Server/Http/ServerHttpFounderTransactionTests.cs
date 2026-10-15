using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Fact]
    public async Task FailedFounderCheckpointRestoresWorldAndKeysThenAllowsRetry()
    {
        var directory = Directory.CreateTempSubdirectory("founder-transaction-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: false);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var world = host.Services.GetRequiredService<WorldRuntime>();
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
            file.Save(world);
            var original = File.ReadAllBytes(file.Path);
            var routing = JsonSerializer.Serialize(providers.CaptureRuntimeConfiguration());
            var id = "founder:" + Guid.NewGuid().ToString("N");
            var action = new OwnerFounderPlacementAction(id, 0, 0,
                new("personal", "openai", "test-model", "test-secret", false, id, Guid.NewGuid().ToString("N"), "Test"));
            File.Move(file.Path, file.Path + ".prior");
            Directory.CreateDirectory(file.Path);
            try
            {
                using var failed = await SendSignedAsync(host, client, key, device.DeviceId,
                    "/api/v1/owner/founders/place", action, OwnerHttpBinding.FounderPlacementPayload(action));
                Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            Assert.Empty(world.Agents);
            Assert.Equal(routing, JsonSerializer.Serialize(providers.CaptureRuntimeConfiguration()));
            var reopened = new ProviderConfigurationStore(providers.Path,
                new("deterministic", null, null, null, null, null, null));
            Assert.Equal(routing, JsonSerializer.Serialize(reopened.CaptureRuntimeConfiguration()));
            Directory.Delete(file.Path);
            File.Move(file.Path + ".prior", file.Path);
            Assert.Equal(original, File.ReadAllBytes(file.Path));
            using var retry = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/founders/place", action, OwnerHttpBinding.FounderPlacementPayload(action));
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            using var restored = file.LoadOrCreate(world.ExportState().WorldSeed);
            Assert.Contains(restored.Agents, person => person.AgentId == id);
            Assert.Equal(2, providers.CaptureRuntimeConfiguration().Assignments!.Count(item => item.AgentId == id));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectionAndFounderPlacementCannotInterleaveRoutingCommits(bool cancelWait)
    {
        var directory = Directory.CreateTempSubdirectory("founder-selection-gate-");
        try
        {
            var placementEntry = new FounderPlacementEntry();
            using var baseHost = new ServerWebApplicationFactory(directory.FullName, gameWorld: true, legacyPace: false);
            using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(placementEntry)));
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var world = host.Services.GetRequiredService<WorldRuntime>();
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
            using var target = new WorldRuntime("selection-gate-target", startPace: WorldStartPace.FounderSetup);
            var entry = host.Services.GetRequiredService<WorldCatalogStore>().Add("Target", target.ExportState());
            var transitionLog = new RecordingLogger<ServerHttpTests>();
            bool? commitHeldGate = null;
            var founderCommitted = transitionLog.WaitForAsync(message =>
            {
                if (!message.Contains("town_transition ", StringComparison.Ordinal) ||
                    !message.Contains("transition=ResidentJoined", StringComparison.Ordinal)) return false;
                // Observe the native commit on its own thread, before the endpoint leaves its lock.
                commitHeldGate = Monitor.IsEntered(providers.WorldMutationGate);
                return true;
            });
            host.Services.GetRequiredService<ILoggerFactory>().AddProvider(
                new RecordingLoggerProvider<ServerHttpTests>(transitionLog));
            var id = "founder:" + Guid.NewGuid().ToString("N");
            var action = new OwnerFounderPlacementAction(id, 0, 0,
                new("personal", "openai", "test-model", "test-secret", false, id, Guid.NewGuid().ToString("N"), "Test"));
            var envelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/founders/place", action, OwnerHttpBinding.FounderPlacementPayload(action));
            // Test-only checkpoint-latency injection reproduces the reported interleaving.
            var fileGate = typeof(WorldStateFile).GetField("gate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(file)!;
            var entered = new InlineCompletionSource();
            using var release = new ManualResetEventSlim();
            var held = Task.Run(() => { lock (fileGate) { entered.TrySetResult(); release.Wait(); } });
            Task? selection = null;
            Task? observation = null;
            Task<HttpResponseMessage>? placement = null;
            var waitFailure = await Record.ExceptionAsync(async () =>
            {
                try
                {
                    await ModelWorkHarness.WaitForSignalAsync(entered.Task);
                    selection = Task.Run(() => host.Services.GetRequiredService<WorldSelectionCoordinator>().Select(entry.Id));
                    observation = WaitForSelectedWorldAsync();
                    await ModelWorkHarness.WaitForSignalAsync(observation);
                    placement = Task.Run(() => client.PostAsJsonAsync("/api/v1/owner/founders/place", envelope));
                    new CancellationToken(canceled: cancelWait).ThrowIfCancellationRequested();
                    var blocked = await ModelWorkHarness.WaitForSignalAsync(placementEntry.Blocked.Task);
                    Assert.True(blocked, "Selection must hold the world-mutation gate until its checkpoint commits.");
                    Assert.False(selection.IsCompleted);
                    Assert.False(placement.IsCompleted);
                    Assert.Empty(world.Agents);
                }
                finally
                {
                    release.Set();
                    // Even a failed observation must drain every writer before host/directory teardown.
                    try
                    {
                        await Task.WhenAll(held, observation ?? Task.CompletedTask, selection ?? Task.CompletedTask,
                            placement ?? (Task)Task.CompletedTask);
                    }
                    finally
                    {
                        if (placement?.IsCompletedSuccessfully == true) (await placement).Dispose();
                    }
                }
            });
            if (cancelWait) Assert.IsAssignableFrom<OperationCanceledException>(waitFailure);
            else Assert.Null(waitFailure);
            Assert.True(held.IsCompleted && selection?.IsCompleted == true && placement?.IsCompleted == true);
            var response = await placement!;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(world.Agents, person => person.AgentId == id);
            Assert.Equal(2, providers.CaptureRuntimeConfiguration().Assignments!.Count(item => item.AgentId == id));
            await ModelWorkHarness.WaitForSignalAsync(founderCommitted);
            Assert.True(commitHeldGate, "Founder placement must hold the selection gate through its native commit.");
            using var restored = file.LoadOrCreate(target.ExportState().WorldSeed);
            Assert.Contains(restored.Agents, person => person.AgentId == id);

            async Task WaitForSelectedWorldAsync()
            {
                while (world.Society.WorldId != target.Society.WorldId)
                {
                    if (release.IsSet) return;
                    if (selection!.IsCompleted)
                    {
                        await selection;
                        Assert.Equal(target.Society.WorldId, world.Society.WorldId);
                    }
                    await Task.Yield();
                }
            }
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class FounderPlacementEntry : IStartupFilter
    {
        public InlineCompletionSource<bool> Blocked { get; } = new();

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                if (context.Request.Path == "/api/v1/owner/founders/place")
                {
                    var gate = context.RequestServices.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate;
                    var entered = Monitor.TryEnter(gate);
                    if (entered) Monitor.Exit(gate);
                    Blocked.TrySetResult(!entered);
                }
                return continuation(context);
            });
            next(app);
        };
    }
}
