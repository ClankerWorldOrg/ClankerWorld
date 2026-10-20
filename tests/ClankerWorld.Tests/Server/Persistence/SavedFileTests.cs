using ClankerWorld.Server.Saves;
using ClankerWorld.Server.Worlds;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Tests;

public sealed class SavedFileTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedTimelineRefusesSavePublicationWithoutChangingAnyOriginal(bool overwrite)
    {
        var directory = Directory.CreateTempSubdirectory("oversized-timeline-");
        try
        {
            using var world = new WorldRuntime("oversized-timeline");
            world.Pause();
            var store = new ManualWorldSaveStore(Path.Combine(directory.FullName, "world.json"));
            var first = store.Create("First", world, []);
            var timeline = Assert.Single(Directory.GetFiles(directory.FullName, "timeline-*.json", SearchOption.AllDirectories));
            using (var stream = File.OpenWrite(timeline)) stream.SetLength((long)SavedFile.MetadataLimit + 1);
            var before = Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);
            var state = WorldRuntimeCodec.Encode(world.ExportState());
            Assert.Throws<SavedFileLimitException>(() =>
            {
                if (overwrite) store.Overwrite(first.Id, world, []);
                else store.Create("Second", world, []);
            });
            Assert.Equal(before.Keys.Order(), Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories).Order());
            foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(first.Id, Assert.Single(store.List(world.Society.WorldId)).Id);
            Assert.Equal(state, WorldRuntimeCodec.Encode(world.ExportState()));
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedDiagnosticBuildMetadataSurvivesCheckpointPublicationAndRollback(bool recovery)
    {
        var directory = Directory.CreateTempSubdirectory("oversized-build-");
        try
        {
            var path = Path.Combine(directory.FullName, "world.json");
            using var world = new WorldRuntime("oversized-build");
            world.Pause();
            var checkpoint = WorldRuntimeCodec.Encode(world.ExportState());
            var original = recovery ? "{refused checkpoint"u8.ToArray() : checkpoint;
            File.WriteAllBytes(path, original);
            var build = SavedBuild.PathFor(path);
            using (var stream = File.Create(build)) { stream.WriteByte(0xff); stream.SetLength((long)SavedFile.MetadataLimit + 1); }
            var before = File.ReadAllBytes(build);
            var file = new WorldStateFile(path);
            var preserved = recovery ? file.PreserveDamagedCheckpoint(original) : null;
            file.Save(world);
            Assert.Equal(checkpoint, WorldRuntimeCodec.Encode(WorldRuntimeCodec.Decode(File.ReadAllBytes(path))));
            Assert.Equal(before, File.ReadAllBytes(build));
            Assert.Null(SavedBuild.TryRead(path));
            if (preserved is not null)
            {
                file.RestorePreservedCheckpoint(preserved);
                Assert.Equal(original, File.ReadAllBytes(path));
                Assert.Equal(before, File.ReadAllBytes(build));
            }
            Assert.Empty(Directory.GetFiles(directory.FullName, "*.tmp"));
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void BoundedReadPreservesExactBytesAtTheLimit(int length)
    {
        var directory = Directory.CreateTempSubdirectory("bounded-save-");
        try
        {
            var path = Path.Combine(directory.FullName, "data");
            var bytes = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
            File.WriteAllBytes(path, bytes);
            Assert.Equal(bytes, SavedFile.Read(path, 16));
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("checkpoint", SavedFile.CheckpointLimit)]
    [InlineData("catalog", SavedFile.CatalogLimit)]
    [InlineData("metadata", SavedFile.MetadataLimit)]
    [InlineData("history", SavedFile.HistoryLimit)]
    public void OversizedFilesAreRefusedBeforeParsingAndKept(string kind, int limit)
    {
        var directory = Directory.CreateTempSubdirectory("oversized-save-");
        try
        {
            var checkpoint = Path.Combine(directory.FullName, "world.save");
            var history = Path.Combine(checkpoint + ".history", new string('a', 64) + ".json");
            var catalog = Path.Combine(checkpoint + ".worlds", "catalog.json");
            var path = kind switch
            {
                "catalog" => catalog,
                "metadata" => checkpoint + ".autosave.json",
                "history" => history,
                _ => checkpoint,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // A sparse invalid file proves the size gate precedes the JSON reader,
            // without allocating or writing hundreds of megabytes in a test.
            using (var file = File.Create(path)) { file.WriteByte(0xff); file.SetLength((long)limit + 1); }
            var stateFile = new WorldStateFile(checkpoint);
            SavedFileLimitException failure;
            switch (kind)
            {
                case "catalog":
                    failure = Assert.Throws<SavedFileLimitException>(() => WorldCatalogStore.ReadActiveIdentity(checkpoint));
                    break;
                case "metadata":
                    failure = Assert.Throws<SavedFileLimitException>(() => new WorldAutosaveStore(checkpoint, "bounded"));
                    break;
                case "history":
                    using (var world = new WorldRuntime("bounded"))
                        failure = Assert.Throws<SavedFileLimitException>(() => stateFile.VerifyRequiredHistory(
                            world.ExportState() with { HistoryArchiveHead = new string('a', 64) }));
                    break;
                default:
                    failure = Assert.Throws<SavedFileLimitException>(() => stateFile.LoadOrCreate("bounded"));
                    break;
            }
            Assert.Contains("exceeds the supported size", failure.Message);
            Assert.Equal((long)limit + 1, new FileInfo(path).Length);
            using var original = File.OpenRead(path);
            Assert.Equal(0xff, original.ReadByte());
            Assert.Single(Directory.GetFiles(directory.FullName, "*", SearchOption.AllDirectories));
        }
        finally { directory.Delete(true); }
    }
}
