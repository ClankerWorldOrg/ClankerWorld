using ClankerWorld.Server.Providers;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClankerWorld.Tests;

public sealed class ManualLoadOwnershipTests
{
    [Theory]
    [InlineData("delete-selected")]
    [InlineData("delete-backup")]
    [InlineData("overwrite-selected")]
    [InlineData("overwrite-backup")]
    [InlineData("rotate")]
    [InlineData("delete-world")]
    [InlineData("save")]
    [InlineData("select-world")]
    [InlineData("create-world")]
    public void InterruptedRollbackKeepsItsSnapshotsAndWorldUntilRestartRecovery(string operation)
    {
        var directory = Directory.CreateTempSubdirectory("manual-load-ownership-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var world = NormalPathWorld.CreateGenerated("manual-load-ownership", _ => new DeterministicDecisionProvider());
            world.Pause();
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));
            var autosave = new WorldAutosaveStore(path, world.Society.WorldId, mutationGate: providers.WorldMutationGate);
            var settings = autosave.Configure(true, 1, 3);
            var file = new WorldStateFile(path);
            file.Save(world);
            var saves = new ManualWorldSaveStore(path, mutationGate: providers.WorldMutationGate);
            var selected = saves.Create("Earlier version", world, [], settings);
            world.SetJevEnabled(false);
            settings = autosave.Configure(false, 30, 10);
            file.Save(world);
            var backup = operation == "rotate" ? saves.CreateAutosave(world, [], settings)
                : saves.CreateLoadRecovery(world, [], settings);
            if (operation == "rotate") _ = saves.CreateAutosave(world, [], settings);
            var expected = WorldRuntimeCodec.Encode(saves.Read(backup.Id));
            var catalog = new WorldCatalogStore(path, world.ExportState(), [], settings, providers.WorldMutationGate);
            using var other = NormalPathWorld.CreateGenerated("manual-load-other-world", _ => new DeterministicDecisionProvider());
            other.Pause();
            other.SetJevEnabled(false);
            var target = catalog.Add("Other world", other.ExportState());
            var selection = new WorldSelectionCoordinator(catalog, world, file, providers, autosave,
                new WorldJevPolicy(), NullLogger<WorldSelectionCoordinator>.Instance, _ => new DeterministicDecisionProvider(), saves);
            var options = new GeographyOptions("manual-load-created-world", WorldSizePreset.Small);
            var preview = operation == "create-world" ? selection.Preview(options) : null;
            saves.BeginLoad(selected.Id, backup.Id);
            void InterruptAfterCheckpoint(string stage)
            {
                if (stage == "checkpoint") throw new LoadOwnershipInterruption();
            }
            Assert.Throws<LoadOwnershipInterruption>(() => saves.ApplyPendingLoad(world, file, providers, autosave, InterruptAfterCheckpoint));
            saves.MarkLoadRollback();
            Assert.Throws<LoadOwnershipInterruption>(() => saves.ApplyPendingLoad(world, file, providers, autosave, InterruptAfterCheckpoint));
            var originals = Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories)
                .ToDictionary(name => name, File.ReadAllBytes);
            var checkpoint = WorldRuntimeCodec.Encode(world.ExportState());
            var id = operation.EndsWith("selected", StringComparison.Ordinal) ? selected.Id : backup.Id;
            Assert.Throws<InvalidOperationException>(() =>
            {
                switch (operation)
                {
                    case "delete-selected" or "delete-backup":
                        var saved = saves.List(world.Society.WorldId).Single(item => item.Id == id);
                        saves.Delete(id, world.Society.WorldId, saved.CreatedUtc); break;
                    case "overwrite-selected" or "overwrite-backup": saves.Overwrite(id, world, [], settings); break;
                    case "rotate": saves.KeepNewestAutosaves(1, worldId: world.Society.WorldId); break;
                    case "delete-world": saves.DeleteWorldSnapshots(world.Society.WorldId); break;
                    case "save": saves.Create("During recovery", world, [], settings); break;
                    case "select-world": selection.Select(target.Id); break;
                    case "create-world":
                        selection.Create("During recovery", options, preview!.Coverage!.Attempt,
                        preview.ManifestDigest, preview.MapLayersDigest!, true); break;
                }
            });
            Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(world.ExportState()));
            Assert.Equal(originals.Keys.Order(), Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories).Order());
            foreach (var original in originals) Assert.Equal(original.Value, File.ReadAllBytes(original.Key));
            saves.RecoverInterruptedLoad(file, providers, _ => new DeterministicDecisionProvider());
            Assert.False(saves.HasPendingLoad);
            Assert.Equal(expected, File.ReadAllBytes(path));
            using var reloaded = file.LoadOrCreate(world.ExportState().WorldSeed);
            Assert.Equal(expected, WorldRuntimeCodec.Encode(reloaded.ExportState()));
            Assert.Equal(settings, new WorldAutosaveStore(path, world.Society.WorldId).Capture());
            Assert.Equal(backup.Id, saves.CurrentPosition(world.Society.WorldId).ContinuedFromId);
            var after = saves.Create("After recovery", reloaded, [], settings);
            saves.Delete(after.Id, reloaded.Society.WorldId, after.CreatedUtc);
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class LoadOwnershipInterruption : Exception { }
}
