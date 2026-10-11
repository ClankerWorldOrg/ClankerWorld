using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;
using ClankerWorld.Viewer.Control;
using ClankerWorld.Viewer.Observation;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClankerWorld.Simulation.Tests;

public sealed class WorldSelectionFailureTests
{
    [Theory]
    [InlineData(true, "checkpoint")]
    [InlineData(false, "checkpoint")]
    [InlineData(true, "archive")]
    [InlineData(true, "autosave")]
    public void FailedWorldSelectionPreservesExistingWorldsAndAllowsRetry(bool create, string blockedStage)
    {
        var directory = Directory.CreateTempSubdirectory("world-selection-failed-checkpoint-");
        try
        {
            using var world = new PrivateWorldRuntime("existing-selection-world");
            world.Pause();
            world.SetJevEnabled(false);
            var path = Path.Combine(directory.FullName, "world.json");
            var file = new PrivateWorldStateFile(path);
            file.Save(world);
            var original = File.ReadAllBytes(path);
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));
            providers.RestoreWorldAssignments([new(world.Inhabitants[0].InhabitantId,
                PlayerDecisionProviders.PlanningRole, PlayerDecisionProviders.Deterministic)]);
            var assignments = providers.CaptureRuntimeConfiguration().Assignments!;
            var autosave = new WorldAutosaveStore(path, world.Society.WorldId, mutationGate: providers.WorldMutationGate);
            var settings = autosave.Configure(false, 1, 3);
            var catalog = new WorldCatalogStore(path, world.ExportState(), assignments, settings, providers.WorldMutationGate);
            using var other = new PrivateWorldRuntime("existing-inactive-world");
            other.Pause();
            var existing = catalog.Add("Existing inactive world", other.ExportState());
            var existingBytes = File.ReadAllBytes(Path.Combine(path + ".worlds", existing.Id + ".save"));
            var before = catalog.Capture();
            var policy = new WorldJevPolicy();
            policy.Initialize(world.JevEnabled, world.JevPolicyRevision, world.RoutineHelper);
            var routing = policy.Capture();
            var selection = new WorldSelectionCoordinator(catalog, world, file, providers, autosave, policy,
                NullLogger<WorldSelectionCoordinator>.Instance, _ => new DeterministicDecisionProvider());
            var options = new GeographyOptions("failed-creation-retry", WorldSizePreset.Small);
            var preview = create ? selection.Preview(options) : null;
            CatalogWorld SelectTarget() => create
                ? selection.Create("Retried creation", options, preview!.Coverage!.Attempt,
                    preview.ManifestDigest, preview.MapLayersDigest!, true)
                : selection.Select(existing.Id);

            // Obstruct one actual atomic replacement without blocking catalog
            // publication. Autosave failure happens after the target checkpoint
            // and its empty provider assignments have already been committed.
            var blocked = blockedStage switch
            {
                "archive" => Path.Combine(path + ".worlds", before.ActiveId + ".save"),
                "autosave" => path + ".autosave.json",
                _ => path,
            };
            File.Move(blocked, blocked + ".backup");
            Directory.CreateDirectory(blocked);
            var failure = Record.Exception(() => SelectTarget());
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString());
            Assert.Equal(before.ActiveId, catalog.Capture().ActiveId);
            Assert.Equal(original, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(settings, autosave.Capture());
            Assert.Equal(routing, policy.Capture());
            Assert.Equal(assignments, providers.CaptureRuntimeConfiguration().Assignments);
            Assert.Equal(before.Worlds.Select(entry => entry.Id), catalog.Capture().Worlds.Select(entry => entry.Id));
            Assert.Equal(existingBytes, File.ReadAllBytes(Path.Combine(path + ".worlds", existing.Id + ".save")));
            Assert.DoesNotContain(catalog.Capture().Worlds, entry => entry.Name == "Retried creation");

            Directory.Delete(blocked);
            File.Move(blocked + ".backup", blocked);
            Assert.Equal(original, File.ReadAllBytes(path));
            using (var reload = file.LoadOrCreate("existing-selection-world"))
            {
                Assert.Equal(original, PrivateWorldRuntimeCodec.Encode(reload.ExportState()));
                var reopenedCatalog = new WorldCatalogStore(path, reload.ExportState(), [], settings);
                Assert.Equal(before.ActiveId, reopenedCatalog.Capture().ActiveId);
                Assert.Equal(before.Worlds.Select(entry => entry.Id), reopenedCatalog.Capture().Worlds.Select(entry => entry.Id));
            }

            var selected = SelectTarget();
            Assert.Equal(selected.Id, catalog.Capture().ActiveId);
            Assert.Equal(selected.WorldId, world.Society.WorldId);
            Assert.True(world.Society.IsPaused);
            Assert.Equal(before.Worlds.Count + (create ? 1 : 0), catalog.Capture().Worlds.Count);
            var saved = File.ReadAllBytes(path);
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(world.ExportState()));
            using var finalReload = new PrivateWorldStateFile(path, allowDifferentSavedSeed: true).LoadOrCreate("existing-selection-world");
            Assert.Equal(saved, PrivateWorldRuntimeCodec.Encode(finalReload.ExportState()));
        }
        finally { directory.Delete(recursive: true); }
    }
}
