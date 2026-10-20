using ClankerWorld.Server.Pairing;
using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClankerWorld.Tests;

public sealed class WorldHelperPreflightTests
{
    [Theory]
    [InlineData("off", false, false, true)]
    [InlineData("decisions", false, false, false)]
    [InlineData("decisions", true, false, true)]
    [InlineData("decisions", false, true, true)]
    [InlineData("decisions", true, true, false)]
    [InlineData("jev", false, false, true)]
    [InlineData("jev", false, true, true)]
    [InlineData("jev", true, true, false)]
    public void HelperPreflightUsesTheSelectedKeyWithoutChangingRouting(string provider,
        bool defaultKey, bool namedKey, bool available)
    {
        var directory = Directory.CreateTempSubdirectory("helper-key-preflight-");
        try
        {
            var providers = Providers(directory.FullName);
            if (defaultKey) providers.Configure(new(provider == "jev" ? "routine" : "planning",
                provider == "jev" ? "jev" : "openai", "default-model", "synthetic-default-key", false));
            var slotId = Guid.NewGuid().ToString("N");
            // A slot for another provider is unavailable even with a default key.
            if (namedKey) providers.CreateCredentialSlot(new(slotId, defaultKey ? "anthropic" : provider == "jev" ? "jev" : "openai",
                "Helper key", "synthetic-named-key"));
            var before = providers.CaptureRuntimeConfiguration();
            var helper = new RoutineHelperSettings(provider, provider == "off" ? "" : "helper-model", namedKey ? slotId : null);
            Assert.Equal(available, providers.CanRestoreWorldAssignments([], helper));
            Assert.Equal(before, providers.CaptureRuntimeConfiguration());
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false, "decisions")]
    [InlineData(true, "decisions")]
    [InlineData(false, "jev")]
    [InlineData(true, "jev")]
    public void ArchivedHelperAvailabilityIsRecheckedAfterItsKeyIsDeleted(bool warmUp, string provider)
    {
        var directory = Directory.CreateTempSubdirectory("helper-catalog-preflight-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            var providers = Providers(directory.FullName);
            var slotId = Guid.NewGuid().ToString("N");
            providers.CreateCredentialSlot(new(slotId, provider == "jev" ? "jev" : "openai", "Helper key", "synthetic-helper-key"));
            using var archived = new WorldRuntime("archived-helper");
            archived.Pause();
            archived.SetRoutineHelper(new(provider, "helper-model", slotId));
            using var active = new WorldRuntime("active-helper");
            active.Pause();
            var file = new WorldStateFile(path);
            file.Save(active);
            var autosave = new WorldAutosaveStore(path, active.Society.WorldId);
            var catalog = new WorldCatalogStore(path, active.ExportState(), [], autosave.Capture());
            var entry = catalog.Add("Archived helper", archived.ExportState());
            var selection = new WorldSelectionCoordinator(catalog, active, file, providers, autosave,
                new WorldJevPolicy(), NullLogger<WorldSelectionCoordinator>.Instance, _ => new DeterministicDecisionProvider(),
                new ManualWorldSaveStore(path, mutationGate: providers.WorldMutationGate));
            var archivedPath = Path.Combine(path + ".worlds", entry.Id + ".save");
            var bytes = File.ReadAllBytes(archivedPath);
            if (warmUp) selection.WarmUp(CancellationToken.None);
            Assert.Equal("compatible", selection.List().Worlds.Single(world => world.Id == entry.Id).Compatibility);

            // A different default key must not replace the helper's deleted named key.
            providers.Configure(new(provider == "jev" ? "routine" : "planning",
                provider == "jev" ? "jev" : "openai", "default-model", "synthetic-default-key", false));
            providers.DeleteCredentialSlot(slotId);
            var listed = selection.List().Worlds.Single(world => world.Id == entry.Id);
            Assert.Equal("incompatible", listed.Compatibility);
            Assert.Contains("saved key", listed.CompatibilityReason, StringComparison.Ordinal);
            Assert.Equal(bytes, File.ReadAllBytes(archivedPath));
            Assert.Equal(active.Society.WorldId, catalog.Active().WorldId);

            // Replacing the checkpoint invalidates the cached helper routing too.
            archived.SetRoutineHelper(RoutineHelperSettings.Off);
            File.WriteAllBytes(archivedPath, WorldRuntimeCodec.Encode(archived.ExportState()));
            Assert.Equal("compatible", selection.List().Worlds.Single(world => world.Id == entry.Id).Compatibility);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void StartupRecoverySkipsAnAutosaveWhoseHelperKeyWasDeleted()
    {
        var directory = Directory.CreateTempSubdirectory("helper-recovery-preflight-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            var providers = Providers(directory.FullName);
            using var world = new WorldRuntime("recovery-helper");
            world.Pause();
            world.SetRoutineHelper(RoutineHelperSettings.Off);
            var settings = new WorldAutosaveStore(path, world.Society.WorldId).Capture();
            var catalog = new WorldCatalogStore(path, world.ExportState(), [], settings);
            var saves = new ManualWorldSaveStore(path);
            var good = saves.CreateAutosave(world, [], settings);
            var slotId = Guid.NewGuid().ToString("N");
            providers.CreateCredentialSlot(new(slotId, "openai", "Helper key", "synthetic-helper-key"));
            world.SetRoutineHelper(new("decisions", "helper-model", slotId));
            var unavailable = saves.CreateAutosave(world, [], settings);
            providers.DeleteCredentialSlot(slotId);
            File.WriteAllText(path, "{interrupted checkpoint");
            var originals = Directory.GetFiles(path + ".manual").ToDictionary(file => file, File.ReadAllBytes);
            var recovery = new WorldStartupRecovery(new WorldStateFile(path), saves, providers,
                _ => new DeterministicDecisionProvider(), "recovery-helper", Path.Combine(directory.FullName, "authority.json"),
                NullLogger<WorldStartupRecovery>.Instance);

            Assert.True(recovery.Pending);
            Assert.Equal(good.Id, recovery.Capture().Autosave?.Id);
            Assert.NotEqual(unavailable.Id, recovery.Capture().Autosave?.Id);
            Assert.Equal("{interrupted checkpoint", File.ReadAllText(path));
            foreach (var file in originals) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static ProviderConfigurationStore Providers(string directory) => new(
        Path.Combine(directory, "providers.json"), new("deterministic", null, null, null, null, null, null));
}
