using System.Text.Json;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.World;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ClankerWorld.Tests;

public sealed class WorldSelectionRestartTests
{
    [Theory]
    [InlineData("assignments")]
    [InlineData("autosave")]
    public void InterruptedSelectionRecoveryRetriesSettingsBeforePublishingTheCatalog(string boundary)
    {
        var directory = Directory.CreateTempSubdirectory("world-selection-restart-");
        try
        {
            var path = Path.Combine(directory.FullName, "runtime.json");
            var providerPath = Path.Combine(directory.FullName, "provider-configuration.json");
            using var first = new WorldRuntime("selection-restart-first");
            using var second = new WorldRuntime("selection-restart-second");
            first.Pause();
            second.Pause();
            var file = new WorldStateFile(path);
            file.Save(first);
            var providers = new ProviderConfigurationStore(providerPath,
                new("deterministic", null, null, null, null, null, null));
            providers.Configure(new("personal", "openai", "first-model", "synthetic-selection-key", false, first.Agents[0].AgentId));
            var autosave = new WorldAutosaveStore(path, first.Society.WorldId);
            var firstSettings = autosave.Configure(false, 30, 10);
            var catalog = new WorldCatalogStore(path, first.ExportState(),
                providers.CaptureRuntimeConfiguration().Assignments!, firstSettings);
            var firstEntry = catalog.Active();
            var secondEntry = catalog.Add("Second", second.ExportState());
            var secondAssignments = providers.CaptureRuntimeConfiguration().Assignments!
                .Select(item => item with { AgentId = second.Agents[0].AgentId, Model = "second-model" }).ToArray();
            var secondSettings = firstSettings with { WorldId = second.Society.WorldId, IntervalMinutes = 2, RotationCount = 3 };
            catalog.Select(secondEntry.Id);
            catalog.ArchiveActive(second.ExportState(), secondAssignments, secondSettings);
            catalog.Select(firstEntry.Id);
            // Also exercise build stamping: it must not publish the recovered selection early.
            var indexPath = Path.Combine(path + ".worlds", "catalog.json");
            var index = catalog.Capture();
            File.WriteAllBytes(indexPath, JsonSerializer.SerializeToUtf8Bytes(index with
            {
                Worlds = index.Worlds.Select(item => item.Id == secondEntry.Id
                    ? item with { GameVersion = null, SourceRevision = null } : item).ToArray(),
            }));
            // First interruption: the target checkpoint reached disk before its settings or catalog.
            file.Save(second);
            var checkpoint = File.ReadAllBytes(path);
            var archives = Directory.GetFiles(path + ".worlds", "*.save").ToDictionary(name => name, File.ReadAllBytes);
            var blocked = boundary == "assignments" ? providerPath : path + ".autosave.json";
            using (var baseHost = new ServerWebApplicationFactory(directory.FullName, legacyPace: false))
            using (var failing = baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<WorldAutosaveStore>(services =>
                {
                    var store = new WorldAutosaveStore(path, second.Society.WorldId, allowWorldSwitch: true,
                        mutationGate: services.GetRequiredService<ProviderConfigurationStore>().WorldMutationGate);
                    // Second interruption: fail the actual settings replacement after catalog construction.
                    File.Move(blocked, blocked + ".backup");
                    Directory.CreateDirectory(blocked);
                    return store;
                }))))
            {
                var failure = Record.Exception(() => failing.CreateClient());
                Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString());
            }
            Directory.Delete(blocked);
            File.Move(blocked + ".backup", blocked);

            using (var restarted = new ServerWebApplicationFactory(directory.FullName, legacyPace: false))
            using (var client = restarted.CreateClient())
            {
                Assert.Equal(secondAssignments, restarted.Services.GetRequiredService<ProviderConfigurationStore>()
                    .CaptureRuntimeConfiguration().Assignments);
                Assert.Equal(secondSettings, restarted.Services.GetRequiredService<WorldAutosaveStore>().Capture());
                Assert.Equal(secondEntry.Id, restarted.Services.GetRequiredService<WorldCatalogStore>().Active().Id);
                Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(restarted.Services.GetRequiredService<WorldRuntime>().ExportState()));
                Assert.Equal(secondEntry.Id, WorldCatalogStore.ReadActiveIdentity(path)!.Id);
                foreach (var archive in archives) Assert.Equal(archive.Value, File.ReadAllBytes(archive.Key));
                // A completed recovery must not undo a later owner choice on another restart.
                restarted.Services.GetRequiredService<ProviderConfigurationStore>().Configure(
                    new("personal", "openai", "chosen-after-recovery", null, false, second.Agents[0].AgentId));
                restarted.Services.GetRequiredService<WorldAutosaveStore>().Configure(true, 15, 10);
            }
            using var again = new ServerWebApplicationFactory(directory.FullName, legacyPace: false);
            using var againClient = again.CreateClient();
            Assert.All(again.Services.GetRequiredService<ProviderConfigurationStore>().CaptureRuntimeConfiguration().Assignments!,
                item => Assert.Equal("chosen-after-recovery", item.Model));
            Assert.Equal(15, again.Services.GetRequiredService<WorldAutosaveStore>().Capture().IntervalMinutes);
        }
        finally { directory.Delete(recursive: true); }
    }
}
