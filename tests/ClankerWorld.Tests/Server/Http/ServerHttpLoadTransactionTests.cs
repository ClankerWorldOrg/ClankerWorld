using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentLoadAndSelectionArchiveOneCompleteCheckpointAndSettings(bool cancelWait)
    {
        var directory = Directory.CreateTempSubdirectory("load-selection-");
        try
        {
            using var host = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var client = host.CreateClient();
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(host, client, key);
            var runtime = host.Services.GetRequiredService<WorldRuntime>();
            var file = host.Services.GetRequiredService<WorldStateFile>();
            var providers = host.Services.GetRequiredService<ProviderConfigurationStore>();
            var autosave = host.Services.GetRequiredService<WorldAutosaveStore>();
            var catalog = host.Services.GetRequiredService<WorldCatalogStore>();
            var saves = host.Services.GetRequiredService<ManualWorldSaveStore>();
            runtime.Pause();
            var worldA = runtime.Society.WorldId;
            var entryA = catalog.Active();
            providers.Configure(new("planning", "openai", "old-model-A", "test-only-load-key", false, "founder-scout"));
            autosave.Configure(true, 1, 3);
            var earlier = saves.Create("Earlier A", runtime, providers.CaptureRuntimeConfiguration().Assignments!, autosave.Capture());
            runtime.SetJevEnabled(false);
            providers.Configure(new("planning", "openai", "new-model-A", null, false, "founder-scout"));
            autosave.Configure(false, 5, 5);
            file.Save(runtime);
            using var other = new WorldRuntime("load-selection-other");
            other.Pause();
            var entryB = catalog.Add("Other", other.ExportState());
            var loadAction = await ApprovedModelLoadAsync(host, client, key, device.DeviceId, "load", earlier.Id);
            var selectAction = await ApprovedModelLoadAsync(host, client, key, device.DeviceId, "select-world", entryB.Id);
            var loadEnvelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/saves/load", loadAction, OwnerHttpBinding.ManualSavePayload(loadAction));
            var selectEnvelope = await CreateSignedRequestAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/worlds/select", selectAction, OwnerHttpBinding.ManualSavePayload(selectAction));
            var fileGate = typeof(WorldStateFile).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(file)!;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var held = Task.Run(() => { lock (fileGate) { entered.SetResult(); release.Wait(TimeSpan.FromSeconds(20)); } });
            await entered.Task;
            var load = Task.Run(() => client.PostAsJsonAsync("/api/v1/owner/saves/load", loadEnvelope));
            Task<HttpResponseMessage>? selection = null;
            string? worldDuringLoad = null;
            var waitFailure = await Record.ExceptionAsync(async () =>
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    while (!runtime.JevEnabled) await Task.Delay(10, deadline.Token);
                    selection = Task.Run(() => client.PostAsJsonAsync("/api/v1/owner/worlds/select", selectEnvelope));
                    if (cancelWait)
                    {
                        worldDuringLoad = runtime.Society.WorldId;
                        deadline.Cancel();
                    }
                    await Task.Delay(1000, deadline.Token);
                    worldDuringLoad = runtime.Society.WorldId;
                }
                finally
                {
                    release.Set();
                    // Released requests can still write until their handlers finish.
                    await Task.WhenAll(held, load, (Task?)selection ?? Task.CompletedTask);
                }
            });
            if (cancelWait) Assert.IsAssignableFrom<OperationCanceledException>(waitFailure);
            else Assert.Null(waitFailure);
            Assert.True(held.IsCompleted && load.IsCompleted && selection?.IsCompleted == true,
                "All concurrent work must finish before teardown, including after a canceled wait.");
            using var loaded = await load;
            using var selected = await selection!;
            Assert.Equal(worldA, worldDuringLoad);
            Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
            var returnAction = await ApprovedModelLoadAsync(host, client, key, device.DeviceId, "select-world", entryA.Id);
            using var returned = await SendSignedAsync(host, client, key, device.DeviceId,
                "/api/v1/owner/worlds/select", returnAction, OwnerHttpBinding.ManualSavePayload(returnAction));
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            Assert.True(runtime.JevEnabled);
            Assert.Equal("old-model-A", Assert.Single(providers.CaptureRuntimeConfiguration().Assignments!).Model);
            Assert.True(autosave.Capture().Enabled);
            Assert.Equal(1, autosave.Capture().IntervalMinutes);
            using var restarted = new ServerWebApplicationFactory(directory.FullName, gameWorld: true);
            using var restartedClient = restarted.CreateClient();
            Assert.True(restarted.Services.GetRequiredService<WorldRuntime>().JevEnabled);
            Assert.Equal("old-model-A", Assert.Single(restarted.Services.GetRequiredService<ProviderConfigurationStore>().CaptureRuntimeConfiguration().Assignments!).Model);
            Assert.True(restarted.Services.GetRequiredService<WorldAutosaveStore>().Capture().Enabled);
        }
        finally { directory.Delete(recursive: true); }
    }
}
