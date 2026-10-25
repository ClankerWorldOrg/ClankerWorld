using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.World;
using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Providers;

namespace ClankerWorld.Tests;

public sealed class AutosaveRotationRecoveryTests
{
    [Theory]
    [InlineData("checkpoint", 1, false)]
    [InlineData("checkpoint", 3, false)]
    [InlineData("checkpoint", 1, true)]
    [InlineData("missing-history", 1, false)]
    [InlineData("corrupt-history", 1, false)]
    [InlineData("identity", 1, false)]
    [InlineData("regeneration", 1, false)]
    public async Task RotationKeepsDamagedCopiesAndCountsOnlyUsablePredecessors(
        string damage, int count, bool preserveNewest)
    {
        var directory = Directory.CreateTempSubdirectory("autosave-rotation-recovery-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var runtime = NormalPathWorld.CreateGenerated("autosave-rotation-recovery",
                _ => new DeterministicDecisionProvider());
            var store = new ManualWorldSaveStore(path);
            var settings = new WorldAutosaveSettings(runtime.Society.WorldId, true, 1, 5, DateTimeOffset.UtcNow, 0);
            var saves = new List<ManualWorldSave>();
            runtime.Resume();
            for (var tick = 0; tick < 5; tick++)
            {
                Assert.True((await runtime.AdvanceOneTickAsync()).Advanced);
                saves.Add(store.CreateAutosave(runtime, [], settings));
            }
            runtime.Pause();
            var newest = saves[^1];
            var target = Path.Combine(path + ".manual", newest.Id + ".save");
            var checkpoint = store.Read(newest.Id);
            if (damage == "checkpoint") File.WriteAllText(target, "damaged checkpoint");
            else if (damage.EndsWith("history", StringComparison.Ordinal))
            {
                var head = new string('c', 64);
                File.WriteAllBytes(target, WorldRuntimeCodec.Encode(checkpoint with { HistoryArchiveHead = head }));
                if (damage == "corrupt-history")
                {
                    Directory.CreateDirectory(path + ".history");
                    File.WriteAllText(Path.Combine(path + ".history", head + ".json"), "damaged history");
                }
            }
            else if (damage == "identity")
            {
                using var other = new WorldRuntime("another-autosave-world");
                File.WriteAllBytes(target, WorldRuntimeCodec.Encode(other.ExportState()));
            }
            else
            {
                var map = checkpoint.Map with
                { GenerationAttempt = (checkpoint.Map.GenerationAttempt + 1) % SmallTemperateMapGenerator.MaximumAttempts };
                map = map with { ManifestDigest = MapManifestCodec.Digest(map) };
                File.WriteAllBytes(target, WorldRuntimeCodec.Encode(checkpoint with { Map = map }));
                Assert.Throws<InvalidDataException>(() =>
                {
                    using var refused = WorldRuntime.Restore(store.Read(newest.Id));
                });
            }
            var evidence = new[] { target, Path.Combine(path + ".manual", newest.Id + ".meta.json") }
                .ToDictionary(file => file, File.ReadAllBytes);
            var retained = saves.Take(4).TakeLast(count).ToArray();
            var retainedBytes = retained.ToDictionary(save => save.Id,
                save => File.ReadAllBytes(Path.Combine(path + ".manual", save.Id + ".save")));
            var liveBytes = WorldRuntimeCodec.Encode(runtime.ExportState());
            var reopened = new ManualWorldSaveStore(path);
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));

            reopened.KeepNewestAutosaves(count, providers, preserveNewest ? newest.Id : null, runtime.Society.WorldId);

            Assert.Equal(retained.Select(save => save.Id).Append(newest.Id).Order(),
                reopened.List(runtime.Society.WorldId).Select(save => save.Id).Order());
            foreach (var (file, bytes) in evidence) Assert.Equal(bytes, File.ReadAllBytes(file));
            foreach (var save in retained)
            {
                var state = reopened.Read(save.Id);
                new WorldStateFile(path).VerifyRequiredHistory(state);
                using var restored = WorldRuntime.Restore(state);
                Assert.Equal(retainedBytes[save.Id], WorldRuntimeCodec.Encode(restored.ExportState()));
            }
            foreach (var old in saves.Take(4 - count))
                Assert.Empty(Directory.GetFiles(path + ".manual", old.Id + ".*"));
            Assert.Equal(liveBytes, WorldRuntimeCodec.Encode(runtime.ExportState()));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void RotationPreservesEveryFileWhenNoCopyCanBeVerified()
    {
        var directory = Directory.CreateTempSubdirectory("autosave-unverified-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var runtime = new WorldRuntime("autosave-unverified");
            runtime.Pause();
            var store = new ManualWorldSaveStore(path);
            var settings = new WorldAutosaveSettings(runtime.Society.WorldId, true, 1, 5, DateTimeOffset.UtcNow, 0);
            for (var index = 0; index < 3; index++)
            {
                var save = store.CreateAutosave(runtime, [], settings);
                File.WriteAllText(Path.Combine(path + ".manual", save.Id + ".save"), "damaged checkpoint");
            }
            var before = Directory.GetFiles(path + ".manual").ToDictionary(file => file, File.ReadAllBytes);
            var providers = new ProviderConfigurationStore(Path.Combine(directory.FullName, "providers.json"),
                new("deterministic", null, null, null, null, null, null));

            store.KeepNewestAutosaves(1, providers, worldId: runtime.Society.WorldId);

            foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));
            Assert.Equal(3, store.List(runtime.Society.WorldId).Count);
        }
        finally { directory.Delete(recursive: true); }
    }
}
