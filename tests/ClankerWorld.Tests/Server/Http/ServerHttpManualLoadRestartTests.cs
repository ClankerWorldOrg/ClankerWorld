using System.Net;
using System.Security.Cryptography;
using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClankerWorld.Tests;

public sealed partial class ServerHttpTests
{
    [Theory]
    [InlineData("backup")]
    [InlineData("intent")]
    [InlineData("timeline")]
    [InlineData("checkpoint")]
    [InlineData("assignments")]
    [InlineData("autosave")]
    [InlineData("loaded")]
    [InlineData("complete")]
    [InlineData("rollback-intent")]
    [InlineData("rollback-checkpoint")]
    [InlineData("rollback-assignments")]
    [InlineData("rollback-autosave")]
    [InlineData("rollback-loaded")]
    [InlineData("rollback-complete")]
    [InlineData("failure-checkpoint")]
    [InlineData("failure-assignments")]
    [InlineData("failure-autosave")]
    public async Task InterruptedSameWorldLoadRestoresMatchingModelAndAutosaveSettingsBeforeServing(string boundary)
    {
        var directory = Directory.CreateTempSubdirectory("manual-load-restart-");
        try
        {
            var path = Path.Combine(directory.FullName, "runtime.json");
            using var world = NormalPathWorld.CreateGenerated("manual-load-restart", _ => new DeterministicDecisionProvider());
            world.Pause();
            var agentId = world.Agents[0].AgentId;
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "provider-configuration.json"),
                new("deterministic", null, null, null, null, null, null));
            providers.Configure(new("planning", "openai", "default-model", "synthetic-load-key", false));
            providers.Configure(new("personal", "openai", "saved-model", null, false, agentId));
            var autosave = new WorldAutosaveStore(path, world.Society.WorldId);
            var savedSettings = autosave.Configure(true, 1, 3);
            var file = new WorldStateFile(path);
            file.Save(world);
            var catalog = new WorldCatalogStore(path, world.ExportState(),
                providers.CaptureRuntimeConfiguration().Assignments!, savedSettings);
            var saves = new ManualWorldSaveStore(path);
            var saved = saves.Create("Earlier version", world, providers.CaptureRuntimeConfiguration().Assignments!, savedSettings);
            var expected = File.ReadAllBytes(path);

            world.SetJevEnabled(false);
            providers.Configure(new("personal", "openai", "later-model", null, false, agentId));
            var laterSettings = autosave.Configure(false, 30, 10);
            file.Save(world);
            catalog.ArchiveActive(world.ExportState(), providers.CaptureRuntimeConfiguration().Assignments!, laterSettings);
            var backup = saves.CreateLoadRecovery(world, providers.CaptureRuntimeConfiguration().Assignments!, laterSettings);
            var laterBytes = File.ReadAllBytes(path);
            var originals = Directory.GetFiles(path + ".manual").Where(name => name.EndsWith(".save", StringComparison.Ordinal) ||
                name.EndsWith(".meta.json", StringComparison.Ordinal)).ToDictionary(name => name, File.ReadAllBytes);

            var rollback = boundary.StartsWith("rollback-", StringComparison.Ordinal);
            var failure = boundary.StartsWith("failure-", StringComparison.Ordinal);
            if (failure)
            {
                using var failingHost = new ServerWebApplicationFactory(directory.FullName, legacyPace: false);
                using var failingClient = failingHost.CreateClient();
                var stopping = failingHost.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
                var failingSaves = failingHost.Services.GetRequiredService<ManualWorldSaveStore>();
                using var failingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var failingDevice = await StartAndActivateAsync(failingHost, failingClient, failingKey);
                var action = await ApprovedModelLoadAsync(failingHost, failingClient, failingKey,
                    failingDevice.DeviceId, "load", saved.Id);
                var blocked = boundary switch
                {
                    "failure-checkpoint" => path,
                    "failure-assignments" => Path.Combine(directory.FullName, "provider-configuration.json"),
                    _ => path + ".autosave.json",
                };
                File.Move(blocked, blocked + ".backup");
                Directory.CreateDirectory(blocked);
                var error = await Record.ExceptionAsync(async () =>
                {
                    using var response = await SendSignedAsync(failingHost, failingClient, failingKey, failingDevice.DeviceId,
                        "/api/v1/owner/saves/load", action, OwnerHttpBinding.ManualSavePayload(action));
                    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                });
                Assert.True(error is null or IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException,
                    error?.ToString());
                Assert.True(stopping.IsCancellationRequested);
                Assert.True(failingSaves.HasPendingLoad);
                Directory.Delete(blocked);
                File.Move(blocked + ".backup", blocked);
            }
            else if (boundary != "backup")
            {
                saves.BeginLoad(saved.Id, backup.Id);
                if (rollback)
                {
                    Assert.Throws<InterruptedLoadException>(() => saves.ApplyPendingLoad(world, file, providers, autosave,
                        stage => { if (stage == "checkpoint") throw new InterruptedLoadException(); }));
                    saves.MarkLoadRollback();
                }
                if (boundary is not ("intent" or "rollback-intent"))
                {
                    var cutoff = rollback ? boundary["rollback-".Length..] : boundary;
                    Assert.Throws<InterruptedLoadException>(() => saves.ApplyPendingLoad(world, file, providers, autosave,
                        stage => { if (stage == cutoff) throw new InterruptedLoadException(); }));
                }
            }

            using var restarted = new ServerWebApplicationFactory(directory.FullName, legacyPace: false);
            using var client = restarted.CreateClient();
            var restoredProviders = restarted.Services.GetRequiredService<ProviderConfigurationStore>();
            var restoredAssignments = restoredProviders.CaptureRuntimeConfiguration().Assignments!;
            Assert.Equal(2, restoredAssignments.Count);
            var previous = boundary == "backup" || rollback || failure;
            var expectedModel = previous ? "later-model" : "saved-model";
            Assert.All(restoredAssignments, assignment => Assert.Equal(expectedModel, assignment.Model));
            var expectedSettings = previous ? laterSettings : savedSettings;
            Assert.Equal(expectedSettings, restarted.Services.GetRequiredService<WorldAutosaveStore>().Capture());
            var restored = restarted.Services.GetRequiredService<WorldRuntime>();
            Assert.Equal(previous ? laterBytes : expected, WorldRuntimeCodec.Encode(restored.ExportState()));
            Assert.Equal(previous ? backup.Id : saved.Id, restarted.Services.GetRequiredService<ManualWorldSaveStore>()
                .CurrentPosition(restored.Society.WorldId).ContinuedFromId);
            foreach (var original in originals) Assert.Equal(original.Value, File.ReadAllBytes(original.Key));
            Assert.NotEqual(saved.Id, backup.Id);
            Assert.False(File.Exists(Path.Combine(path + ".manual", "pending-load.json")));
            Assert.Equal(restored.RoutineHelper, restarted.Services.GetRequiredService<WorldJevPolicy>().Capture().Helper);

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var device = await StartAndActivateAsync(restarted, client, key);
            using var resumed = await SendSignedAsync(restarted, client, key, device.DeviceId,
                "/api/v1/owner/control/resume", new OwnerControlAction("resume"), OwnerHttpBinding.EmptyPayload("resume"));
            Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
            Assert.All(restoredProviders.CaptureRuntimeConfiguration().Assignments!, assignment => Assert.Equal(expectedModel, assignment.Model));
            Assert.Equal(expectedSettings, restarted.Services.GetRequiredService<WorldAutosaveStore>().Capture());

            // Completed intents cannot undo later owner configuration on another restart.
            restoredProviders.Configure(new("personal", "openai", "chosen-after-recovery", null, false, agentId));
            using var again = new ServerWebApplicationFactory(directory.FullName, legacyPace: false);
            using var againClient = again.CreateClient();
            Assert.All(again.Services.GetRequiredService<ProviderConfigurationStore>().CaptureRuntimeConfiguration().Assignments!,
                assignment => Assert.Equal("chosen-after-recovery", assignment.Model));
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class InterruptedLoadException : Exception { }
}
